using GameFramework;
using LOP.Event.Entity;
using MessagePipe;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System;
using VContainer;

namespace LOP
{
    [DIMonoBehaviour]
    public class LOPEntityManager : MonoBehaviour, IEntityManager
    {
        [Inject]
        private ISessionManager sessionManager;

        [Inject]
        private GameFramework.World.EntityRegistry entityRegistry;

        [Inject]
        private IEntityFactory entityFactory;

        [Inject]
        private IEntityCreationDataFactory entityCreationDataFactory;

        [Inject]
        private IPublisher<EntityCreated> entityCreatedPublisher;

        // id→뷰 앵커 인덱스. World EntityRegistry(id→데이터 진실원본)와 별개 축.
        private Dictionary<string, LOPActor> entityMap = new Dictionary<string, LOPActor>();
        private Dictionary<string, string> userEntityMap = new Dictionary<string, string>();

        private int entityIdCounter = 1;

        private HashSet<string> entitiesToDestroy = new HashSet<string>();

        public IEntity GetEntity(string entityId)
        {
            return entityMap[entityId];
        }

        public T GetEntity<T>(string entityId) where T : IEntity
        {
            return (T)(object)entityMap[entityId];
        }

        public bool TryGetEntity(string entityId, out IEntity entity)
        {
            if (entityMap.TryGetValue(entityId, out var value) == false)
            {
                entity = null;
                return false;
            }

            entity = value;
            return true;
        }

        public bool TryGetEntity<T>(string entityId, out T entity) where T : IEntity
        {
            if (entityMap.TryGetValue(entityId, out var value) == false)
            {
                entity = default;
                return false;
            }

            entity = (T)(object)value;
            return true;
        }

        public IEnumerable<IEntity> GetEntities()
        {
            return entityMap.Values.Cast<IEntity>().ToList();
        }

        public IEnumerable<T> GetEntities<T>() where T : IEntity
        {
            return entityMap.Values.Cast<T>().ToList();
        }

        public TEntity CreateEntity<TEntity, TCreationData>(TCreationData creationData)
            where TEntity : IEntity
            where TCreationData : struct, IEntityCreationData
        {
            var entity = entityFactory.CreateEntity<TEntity, TCreationData>(creationData);

            var actor = (LOPActor)(object)entity;
            entityMap[actor.entityId] = actor;

            entityCreatedPublisher.Publish(new EntityCreated(actor));

            if (creationData is CharacterCreationData characterCreationData
                && string.IsNullOrEmpty(characterCreationData.userId) == false)
            {
                userEntityMap[characterCreationData.userId] = entity.entityId;
            }

            return entity;
        }

        public void DeleteEntityById(string entityId)
        {
            entitiesToDestroy.Add(entityId);
        }

        public void DestroyMarkedEntities()
        {
            foreach (var entityId in entitiesToDestroy)
            {
                LOPActor lopActor = GetEntity<LOPActor>(entityId);
                string ownerId = GetUserIdByEntityId(entityId);   // capture before registry.Remove (reads Ownership)

                foreach (var cleanup in lopActor.transform.GetComponentsInChildren<ICleanup>(true))
                {
                    cleanup.Cleanup();
                }

                // --- World Core (병렬·정리) — 마이그레이션 Slice 2: Unregister from World ---
                if (entityRegistry.Remove(entityId))
                {
                    Debug.Log($"[World] Unregistered entity {entityId}");
                }
                // --- end World Core slice 2 ---

                Destroy(lopActor.gameObject);

                entityMap.Remove(entityId);

                if (ownerId != null)
                {
                    userEntityMap.Remove(ownerId);
                }

                //  Send EntityDespawnToC
                foreach (var session in sessionManager.GetAllSessions().DefaultIfEmpty())
                {
                    session.Send(new EntityDespawnToC
                    {
                        EntityId = entityId,
                    });
                }
            }

            entitiesToDestroy.Clear();
        }

        public void UpdateEntities()
        {
        }

        public string GetUserIdByEntityId(string entityId)
        {
            return entityRegistry.Get(entityId)?.Get<GameFramework.World.Ownership>()?.OwnerId;
        }

        public TEntity GetEntityByUserId<TEntity>(string userId) where TEntity : IEntity
        {
            string entityId = userEntityMap[userId];

            return GetEntity<TEntity>(entityId);
        }

        public string GenerateEntityId()
        {
            return entityIdCounter++.ToString();
        }

        public EntitySnap[] GetAllEntitySnaps()
        {
            var entitySnapList = new List<EntitySnap>();

            foreach (var entity in GetEntities<LOPActor>().OrEmpty())
            {
                var worldEntity = entityRegistry.Get(entity.entityId);
                GameFramework.World.Health health = worldEntity?.Get<GameFramework.World.Health>();
                var snap = new EntitySnap
                {
                    EntityId = entity.entityId,
                    Position = MapperConfig.mapper.Map<ProtoVector3>(GameFramework.World.EntityMotionExtensions.GetPosition(worldEntity)),
                    Rotation = MapperConfig.mapper.Map<ProtoVector3>(GameFramework.World.EntityMotionExtensions.GetRotation(worldEntity)),
                    Velocity = MapperConfig.mapper.Map<ProtoVector3>(GameFramework.World.EntityMotionExtensions.GetVelocity(worldEntity)),
                    MaxHP = health?.Max ?? 0,
                    CurrentHP = health?.Current ?? 0,
                };

                var contributions = worldEntity?.Get<MotionContributions>();
                if (contributions != null)
                {
                    foreach (var c in contributions.Items)
                    {
                        snap.MotionContributions.Add(new ProtoMotionContribution
                        {
                            Horizontal = new ProtoVector3 { X = c.Horizontal.X, Y = c.Horizontal.Y, Z = c.Horizontal.Z },
                            Mode = (int)c.Mode,
                            Priority = c.Priority,
                            StartTick = c.StartTick,
                            EndTick = c.EndTick,
                            DecayPerTick = c.DecayPerTick,
                        });
                    }
                }

                entitySnapList.Add(snap);
            }

            return entitySnapList.ToArray();
        }

        public EntityCreationData[] GetAllEntityCreationDatas()
        {
            var entityCreationDataList = new List<EntityCreationData>();

            foreach (var entity in GetEntities<LOPActor>().OrEmpty())
            {
                EntityCreationData entityCreationData = entityCreationDataFactory.Create(entity);

                entityCreationDataList.Add(entityCreationData);
            }

            return entityCreationDataList.ToArray();
        }
    }
}
