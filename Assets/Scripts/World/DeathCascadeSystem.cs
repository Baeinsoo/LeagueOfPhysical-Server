using GameFramework;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// resolve 단계(egress 전)에서 죽음의 결과를 직접 처리하는 서버 cascade 시스템.
    /// WorldEventBuffer의 DeathEvent를 읽어 victim despawn + 경험치 구슬 스폰.
    /// (구 WorldEventReactor의 EventBus → LOPGame.HandleDeath 왕복을 대체.)
    /// </summary>
    public class DeathCascadeSystem
    {
        private readonly LOPEntityManager _entityManager;
        private readonly ISessionManager _sessionManager;
        private readonly IEntityCreationDataFactory _entityCreationDataFactory;

        public DeathCascadeSystem(
            LOPEntityManager entityManager,
            ISessionManager sessionManager,
            IEntityCreationDataFactory entityCreationDataFactory)
        {
            _entityManager = entityManager;
            _sessionManager = sessionManager;
            _entityCreationDataFactory = entityCreationDataFactory;
        }

        public void Resolve(IReadOnlyList<GameFramework.World.WorldEvent> events)
        {
            foreach (var e in events)
            {
                if (e is GameFramework.World.DeathEvent death)
                {
                    ResolveDeath(death);
                }
            }
        }

        private void ResolveDeath(GameFramework.World.DeathEvent death)
        {
            LOPEntity victim = _entityManager.GetEntity<LOPEntity>(death.victimId);
            if (victim == null)
            {
                Debug.LogWarning($"[World] DeathCascade: victim {death.victimId} not found");
                return;
            }
            Vector3 position = victim.position;

            _entityManager.DeleteEntityById(death.victimId);
            SpawnExpMarble(position);
        }

        private void SpawnExpMarble(Vector3 position)
        {
            string visualId = "Assets/Art/Items/ExpMarble/ExpMarble.prefab";
            string itemCode = "exp_marble";

            ItemCreationData data = new ItemCreationData
            {
                entityId = _entityManager.GenerateEntityId(),
                visualId = visualId,
                itemCode = itemCode,
                position = position,
                rotation = Vector3.zero,
                velocity = Vector3.zero,
            };

            LOPEntity entity = _entityManager.CreateEntity<LOPEntity, ItemCreationData>(data);

            EntitySpawnToC entitySpawnToC = new EntitySpawnToC
            {
                EntityCreationData = _entityCreationDataFactory.Create(entity),
            };

            foreach (var session in _sessionManager.GetAllSessions().OrEmpty())
            {
                session.Send(entitySpawnToC);
            }
        }
    }
}
