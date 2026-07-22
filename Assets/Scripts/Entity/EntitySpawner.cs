using GameFramework;
using LOP.Event.Entity;
using MessagePipe;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 서버 데이터 수명(출생·사망) 조율 — 데이터만 만진다. <see cref="LOPActor"/>/<see cref="ActorRegistry"/> 미참조.
    /// 서버 부가: entityId 발급, userId↔entityId 매핑(스폰 수명 결합), despawn 와이어 송신.
    /// </summary>
    public class EntitySpawner
    {
        private readonly ISessionManager sessionManager;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly CharacterCreator characterCreator;
        private readonly ItemCreator itemCreator;
        private readonly IPublisher<EntityCreated> entityCreatedPublisher;
        private readonly IPublisher<EntityDestroyed> entityDestroyedPublisher;

        private readonly Dictionary<string, string> userEntityMap = new Dictionary<string, string>();
        private readonly HashSet<string> entitiesToDestroy = new HashSet<string>();
        private int entityIdCounter = 1;

        public EntitySpawner(
            ISessionManager sessionManager,
            GameFramework.World.EntityRegistry entityRegistry,
            CharacterCreator characterCreator,
            ItemCreator itemCreator,
            IPublisher<EntityCreated> entityCreatedPublisher,
            IPublisher<EntityDestroyed> entityDestroyedPublisher)
        {
            this.sessionManager = sessionManager;
            this.entityRegistry = entityRegistry;
            this.characterCreator = characterCreator;
            this.itemCreator = itemCreator;
            this.entityCreatedPublisher = entityCreatedPublisher;
            this.entityDestroyedPublisher = entityDestroyedPublisher;
        }

        public string GenerateEntityId()
        {
            return (entityIdCounter++).ToString();
        }

        // userId→entityId. 순수 로직 사이트가 LOPActor를 거치지 않고 id만 얻는다.
        public string GetEntityIdByUserId(string userId)
        {
            return userEntityMap[userId];
        }

        public void Spawn(CharacterCreationData creationData)
        {
            characterCreator.Create(creationData);
            entityCreatedPublisher.Publish(new EntityCreated(creationData.entityId));

            if (string.IsNullOrEmpty(creationData.userId) == false)
            {
                userEntityMap[creationData.userId] = creationData.entityId;
            }
        }

        public void Spawn(ItemCreationData creationData)
        {
            itemCreator.Create(creationData);
            entityCreatedPublisher.Publish(new EntityCreated(creationData.entityId));
        }

        public void Despawn(string entityId)
        {
            entitiesToDestroy.Add(entityId);
        }

        // LOPRunner가 틱 끝에 호출. registry 제거 + "죽었다" 방송(EntityBinder가 actor 파괴) + userMap 정리 + despawn 와이어.
        public void FlushDespawns()
        {
            foreach (var entityId in entitiesToDestroy)
            {
                // Ownership은 registry.Remove 전에 읽는다(제거 후엔 사라짐).
                string ownerId = entityRegistry.Get(entityId)?.Get<GameFramework.World.Ownership>()?.OwnerId;

                if (entityRegistry.Remove(entityId))
                {
                    Debug.Log($"[World] Unregistered entity {entityId}");
                }

                entityDestroyedPublisher.Publish(new EntityDestroyed(entityId));

                if (ownerId != null)
                {
                    userEntityMap.Remove(ownerId);
                }

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
    }
}
