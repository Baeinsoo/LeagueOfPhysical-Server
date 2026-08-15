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

                //  매 틱 도는 자리라 "없다"를 그냥 찍으면 초당 수백 줄이 되어 다른 로그를 전부 밀어낸다
                //  (Flappy의 새로 실제로 겪었다 — 96초에 8만 줄, 시작 로그가 회전으로 날아갔다).
                //  마스터데이터로 스탯을 받는 몸만 이 값들을 갖는다. 새는 체력·마나 개념이 없어
                //  없는 게 정상이고, 그건 알릴 일이 아니다.
                bool expectsStats = worldEntity?.Has<MasterDataRef>() ?? false;

                GameFramework.World.Health health = worldEntity?.Get<GameFramework.World.Health>();
                if (expectsStats && health == null)
                {
                    Debug.LogWarning($"[World] UserEntitySnap: Health not found for entity {entityId}");
                }

                UserEntitySnapToC entitySnapsToC = new UserEntitySnapToC();
                entitySnapsToC.CurrentHP = health?.Current ?? 0;
                entitySnapsToC.MaxHP = health?.Max ?? 0;
                GameFramework.World.Mana mana = worldEntity?.Get<GameFramework.World.Mana>();
                if (expectsStats && mana == null)
                {
                    Debug.LogWarning($"[World] UserEntitySnap: Mana not found for entity {entityId}");
                }
                entitySnapsToC.CurrentMP = mana?.Current ?? 0;
                entitySnapsToC.MaxMP = mana?.Max ?? 0;
                GameFramework.World.Level level = worldEntity?.Get<GameFramework.World.Level>();
                if (expectsStats && level == null)
                {
                    Debug.LogWarning($"[World] UserEntitySnap: Level not found for entity {entityId}");
                }
                entitySnapsToC.CurrentExp = level?.Exp ?? 0;
                entitySnapsToC.Level = level?.Value ?? 0;
                GameFramework.World.Stats stats = worldEntity?.Get<GameFramework.World.Stats>();
                if (expectsStats && stats == null)
                {
                    Debug.LogWarning($"[World] UserEntitySnap: Stats not found for entity {entityId}");
                }
                entitySnapsToC.StatPoints = stats?.UnspentPoints ?? 0;

                session.Send(entitySnapsToC);
            }
        }
    }
}
