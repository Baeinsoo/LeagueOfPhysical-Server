using UnityEngine;
using GameFramework;

namespace LOP
{
    /// <summary>구 LOPRunner.EndUpdate(세션별 UserEntitySnap 전송 부분) 이동. HP/MP/Level/Exp/StatPoints를 World 코어에서 읽어 세션별로 보낸다.</summary>
    public class UserEntitySnapshotSystem : GameFramework.Runner.ITickSystem
    {
        private readonly ISessionManager sessionManager;
        private readonly EntitySpawner entitySpawner;
        private readonly GameFramework.World.EntityRegistry entityRegistry;

        public UserEntitySnapshotSystem(ISessionManager sessionManager, EntitySpawner entitySpawner, GameFramework.World.EntityRegistry entityRegistry)
        {
            this.sessionManager = sessionManager;
            this.entitySpawner = entitySpawner;
            this.entityRegistry = entityRegistry;
        }

        public void Tick(long tick, float deltaTime)
        {
            foreach (var session in sessionManager.GetAllSessions())
            {
                string entityId = entitySpawner.GetEntityIdByUserId(session.userId);

                // HP/MP/Level/Exp/StatPoints 모두 World 코어에서 읽는다.
                GameFramework.World.Entity worldEntity = entityRegistry.Get(entityId);
                GameFramework.World.Health health = worldEntity?.Get<GameFramework.World.Health>();
                if (health == null)
                {
                    Debug.LogWarning($"[World] UserEntitySnap: Health not found for entity {entityId}");
                }

                UserEntitySnapToC entitySnapsToC = new UserEntitySnapToC();
                entitySnapsToC.CurrentHP = health?.Current ?? 0;
                entitySnapsToC.MaxHP = health?.Max ?? 0;
                GameFramework.World.Mana mana = worldEntity?.Get<GameFramework.World.Mana>();
                if (mana == null)
                {
                    Debug.LogWarning($"[World] UserEntitySnap: Mana not found for entity {entityId}");
                }
                entitySnapsToC.CurrentMP = mana?.Current ?? 0;
                entitySnapsToC.MaxMP = mana?.Max ?? 0;
                GameFramework.World.Level level = worldEntity?.Get<GameFramework.World.Level>();
                if (level == null)
                {
                    Debug.LogWarning($"[World] UserEntitySnap: Level not found for entity {entityId}");
                }
                entitySnapsToC.CurrentExp = level?.Exp ?? 0;
                entitySnapsToC.Level = level?.Value ?? 0;
                GameFramework.World.Stats stats = worldEntity?.Get<GameFramework.World.Stats>();
                if (stats == null)
                {
                    Debug.LogWarning($"[World] UserEntitySnap: Stats not found for entity {entityId}");
                }
                entitySnapsToC.StatPoints = stats?.UnspentPoints ?? 0;

                session.Send(entitySnapsToC);
            }
        }
    }
}
