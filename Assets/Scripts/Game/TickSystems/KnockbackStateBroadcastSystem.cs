using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// 넉백 상태 방송(서버 End 페이즈, 맨 마지막). 판본이 바뀌면 모든 세션에 한 번씩,
    /// 새로 붙은 세션에도 한 번 보낸다. MountedArcheryStateBroadcastSystem과 같은 모양이다.
    /// </summary>
    public sealed class KnockbackStateBroadcastSystem : GameFramework.Runner.ITickSystem
    {
        private readonly KnockbackMatchState state;
        private readonly GameFramework.World.EntityRegistry registry;
        private readonly GameFramework.ISessionManager sessionManager;
        private readonly HashSet<string> receivedSessionIds = new HashSet<string>();
        private int sentVersion;

        public KnockbackStateBroadcastSystem(KnockbackMatchState state, GameFramework.World.EntityRegistry registry,
                                             GameFramework.ISessionManager sessionManager)
        {
            this.state = state;
            this.registry = registry;
            this.sessionManager = sessionManager;
        }

        public void Tick(long tick, float deltaTime)
        {
            if (state.Version == 0)
            {
                return;
            }
            if (state.Version != sentVersion)
            {
                sentVersion = state.Version;
                receivedSessionIds.Clear();
            }

            KnockbackStateToC message = null;
            foreach (var session in sessionManager.GetAllSessions())
            {
                if (session.isConnected == false)
                {
                    receivedSessionIds.Remove(session.sessionId);
                    continue;
                }
                if (receivedSessionIds.Contains(session.sessionId))
                {
                    continue;
                }
                message ??= ToWire(state, registry);
                session.Send(message);
                receivedSessionIds.Add(session.sessionId);
            }
        }

        public static KnockbackStateToC ToWire(KnockbackMatchState state, GameFramework.World.EntityRegistry registry)
        {
            var message = new KnockbackStateToC { Version = state.Version };
            foreach (var id in state.Players)
            {
                int damage = 0;
                if (registry.TryGet(id, out var entity))
                {
                    damage = entity.Get<KnockbackBody>()?.DamagePercent ?? 0;
                }
                message.Players.Add(new KnockbackPlayerWire
                {
                    EntityId = id,
                    EliminatedTick = state.EliminatedTick.TryGetValue(id, out var t) ? t : -1,
                    DamagePercent = damage,
                });
            }
            foreach (var s in state.ZoneStages)
            {
                message.ZoneStages.Add(new MountedZoneStageWire
                {
                    Index = s.index,
                    RevealTick = s.revealTick,
                    ShrinkStartTick = s.shrinkStartTick,
                    ShrinkEndTick = s.shrinkEndTick,
                    FromX = s.fromCenter.x,
                    FromZ = s.fromCenter.y,
                    FromRadius = s.fromRadius,
                    ToX = s.toCenter.x,
                    ToZ = s.toCenter.y,
                    ToRadius = s.toRadius,
                    DamagePerSecond = s.damagePerSecond,
                });
            }
            return message;
        }
    }
}
