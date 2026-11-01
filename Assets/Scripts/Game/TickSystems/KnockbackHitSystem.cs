using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 넉백 맞음 판정(서버 End 페이즈, 맨 앞). 서 있는 몸만 표적이다 — 떨어지는 몸은 이미 졌다.
    /// 같은 틱 결과를 모두 적용한 뒤 끝낸다. 서버는 End 전에 월드 사건 버퍼를 비우므로 맞음 사건은 여기서 직접 내보낸다.
    /// </summary>
    public sealed class KnockbackHitSystem : GameFramework.Runner.ITickSystem
    {
        //  이보다 낮으면 바닥 밖으로 빠져 떨어지는 중이다.
        private const float StandingMinY = -0.05f;

        private readonly KnockbackWorld world;
        private readonly GameFramework.World.EntityRegistry registry;
        private readonly KnockbackMatchState state;
        private readonly KnockbackConfig config;
        private readonly GameFramework.World.IEventSink eventSink;
        private readonly float tickInterval;
        private readonly KnockbackPillars pillars;
        private readonly List<UnityEngine.Vector2> standingPillars = new List<UnityEngine.Vector2>();
        private readonly List<(string id, Vector2 position)> targets = new List<(string, Vector2)>();
        private readonly List<(string id, Vector2 position)> shotTargets = new List<(string, Vector2)>();
        private readonly List<GameFramework.World.WorldEvent> events = new List<GameFramework.World.WorldEvent>();

        public KnockbackHitSystem(KnockbackWorld world, GameFramework.World.EntityRegistry registry,
                                  KnockbackMatchState state, KnockbackConfig config,
                                  GameFramework.World.IEventSink eventSink, KnockbackPillars pillars,
                                  float tickInterval = 0.02f)
        {
            this.world = world;
            this.registry = registry;
            this.state = state;
            this.config = config;
            this.eventSink = eventSink;
            this.pillars = pillars;
            this.tickInterval = tickInterval;
        }

        //  탄마다 쏜 사람과 같은 팀인 몸을 표적에서 빼 호출한다 — 탄이 아군을 지나 뒤의 상대를 맞히게.
        //  Find는 탄 하나씩 불러도 결과 순서가 탄 목록 순서 그대로다.
        private List<(KnockbackShot shot, string victimId)> FindHits(long tick)
        {
            var shots = world.Shots.Shots;
            var hits = new List<(KnockbackShot, string)>();
            var single = new KnockbackShot[1];
            for (int i = 0; i < shots.Count; i++)
            {
                state.Team.TryGetValue(shots[i].ShooterId, out int shooterTeam);
                shotTargets.Clear();
                foreach (var target in targets)
                {
                    state.Team.TryGetValue(target.id, out int targetTeam);
                    if (!KnockbackTeams.SameTeam(shooterTeam, targetTeam))
                    {
                        shotTargets.Add(target);
                    }
                }
                single[0] = shots[i];
                hits.AddRange(KnockbackHits.Find(single, shotTargets, standingPillars, pillars.Radius, tick, config, tickInterval));
            }
            return hits;
        }

        public void Tick(long tick, float deltaTime)
        {
            if (tick < world.GameplayStartTick || world.Shots.Shots.Count == 0)
            {
                return;
            }

            targets.Clear();
            foreach (var id in state.Players)
            {
                if (state.EliminatedTick.ContainsKey(id) || registry.TryGet(id, out var entity) == false)
                {
                    continue;
                }
                var t = entity.Get<GameFramework.World.Transform>();
                if (t == null || t.Position.Y < StandingMinY)
                {
                    continue;
                }
                targets.Add((id, new Vector2(t.Position.X, t.Position.Z)));
            }

            pillars.CollectStanding(tick, standingPillars);
            var hits = FindHits(tick);
            if (hits.Count == 0)
            {
                return;
            }

            events.Clear();
            foreach (var (shot, victimId) in hits)
            {
                var victim = registry.Get(victimId);
                var body = victim.Get<KnockbackBody>();
                var velocity = victim.Get<GameFramework.World.Velocity>();
                if (body == null || velocity == null)
                {
                    continue;
                }
                KnockbackRules.ApplyHit(body, velocity, shot.Direction, tick, config, tickInterval);
                world.RemoveShot(shot.ShooterId, shot.FireTick);
                events.Add(new KnockbackHitEvent(shot.ShooterId, shot.FireTick, victimId));
            }

            state.MarkChanged();
            if (events.Count > 0)
            {
                eventSink.Emit(events);
            }
        }
    }
}
