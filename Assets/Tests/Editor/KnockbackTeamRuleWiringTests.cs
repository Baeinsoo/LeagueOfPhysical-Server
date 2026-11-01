using System.Collections.Generic;
using LOP.Event.Entity;
using MessagePipe;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>팀전 룰을 실제 Initialize·IsMatchOver·ResolveOutcome으로 거쳐 잰다(순수 함수만 재면 배선 실수를 못 잡는다).</summary>
    public class KnockbackTeamRuleWiringTests
    {
        private sealed class FakePublisher<T> : IPublisher<T>
        {
            public void Publish(T message) { }
        }

        private sealed class FakeRoomDataStore : IRoomDataStore
        {
            public Room room { get; set; }
            public Match match { get; set; }
            public MatchOutcome outcome { get; set; }
            public IReadOnlyDictionary<string, PlayerLookDto> looks { get; set; }
            public void Clear() { }
        }

        private sealed class Fixture
        {
            public GameFramework.World.EntityRegistry Registry = new GameFramework.World.EntityRegistry();
            public KnockbackMatchState State = new KnockbackMatchState();
            public KnockbackRuleSystem Rule;

            /// <summary>팀 번호의 몸 id들(자리 순서).</summary>
            public List<string> TeamBodies(int team)
            {
                var ids = new List<string>();
                foreach (var id in State.Players)
                {
                    if (State.Team[id] == team) ids.Add(id);
                }
                return ids;
            }

            public void Fall(string entityId, long tick)
            {
                State.Eliminate(entityId, tick);
                if (State.LastTick < tick) State.LastTick = tick;
            }
        }

        private static KnockbackConfig Config() => new KnockbackConfig(120f, 6f, 10f, 12f, 2f, 0.5f, 5f, 11f, 30f, 18f, 0.3f, 0.55f, 3, 0.95f, 0.26f, 1.2f, 1, 6f, 0.5f, 0.5f, 0.3f, 0.1f, 0.2f, 0.8f, 5f, new List<MountedArcheryZoneStageConfig>());

        private static Fixture Build(int teamCount, params string[] humans)
        {
            var f = new Fixture();
            var config = Config();
            var spawner = new EntitySpawner(
                sessionManager: null,
                entityRegistry: f.Registry,
                characterCreator: new KnockbackPlayerCreator(f.Registry, config),
                itemCreator: null,
                coinCreator: null,
                entityCreatedPublisher: new FakePublisher<EntityCreated>(),
                entityDestroyedPublisher: new FakePublisher<EntityDestroyed>());
            var store = new FakeRoomDataStore { match = new Match { playerList = humans } };
            f.Rule = new KnockbackRuleSystem(store, spawner, f.State, config, null, new KnockbackMatchMode(teamCount));
            f.Rule.Initialize();
            return f;
        }

        private static int PlacementOf(MatchOutcome o, string user)
        {
            foreach (var p in o.placements) if (p.userId == user) return p.placement;
            return -1;
        }

        private static readonly string[] None = new string[0];

        //  자리 순서대로 팀 1·2를 번갈아 받는다: u1·u3 = 팀 1, u2·u4 = 팀 2.
        [Test]
        public void 팀전_진_팀이_다_떨어진_뒤_이긴_팀이_떨어져도_이긴_팀이_1위()
        {
            var f = Build(2, "u1", "u2", "u3", "u4");
            foreach (var id in f.TeamBodies(2)) f.Fall(id, 100);
            //  끝나기를 기다리는 사이 이긴 팀도 연쇄 충돌·바닥 무너짐으로 떨어진다.
            foreach (var id in f.TeamBodies(1)) f.Fall(id, 110);
            f.State.LastTick = 400;

            Assert.IsTrue(f.Rule.IsMatchOver);
            var o = f.Rule.ResolveOutcome();
            Assert.AreEqual(1, PlacementOf(o, "u1"));
            Assert.AreEqual(1, PlacementOf(o, "u3"));
            Assert.AreEqual(2, PlacementOf(o, "u2"));
            Assert.AreEqual(2, PlacementOf(o, "u4"));
        }

        [Test]
        public void 팀전_동시에_다_떨어지면_전원_1위()
        {
            var f = Build(2, "u1", "u2", "u3", "u4");
            foreach (var id in f.TeamBodies(2)) f.Fall(id, 100);
            foreach (var id in f.TeamBodies(1)) f.Fall(id, 100);
            f.State.LastTick = 400;

            Assert.IsTrue(f.Rule.IsMatchOver);
            var o = f.Rule.ResolveOutcome();
            foreach (var u in new[] { "u1", "u2", "u3", "u4" }) Assert.AreEqual(1, PlacementOf(o, u));
        }

        [Test]
        public void 팀전_같은_틱에_이긴_팀이_일부만_떨어지면_그_팀이_이긴다()
        {
            var f = Build(2, "u1", "u2", "u3", "u4");
            foreach (var id in f.TeamBodies(2)) f.Fall(id, 100);
            f.Fall(f.TeamBodies(1)[0], 100);
            f.State.LastTick = 400;

            var o = f.Rule.ResolveOutcome();
            Assert.AreEqual(1, PlacementOf(o, "u1"));
            Assert.AreEqual(1, PlacementOf(o, "u3"));
            Assert.AreEqual(2, PlacementOf(o, "u2"));
        }

        [Test]
        public void 팀전은_한_팀이_남아야_끝난다()
        {
            var f = Build(2, "u1", "u2", "u3", "u4");
            f.Fall(f.TeamBodies(1)[0], 100);
            f.Fall(f.TeamBodies(2)[0], 100);
            f.State.LastTick = 1000;
            //  두 팀에 한 명씩 남았다 — 개인전 규칙(한 명 남음)이면 아직이지만, "둘 남음"이라 여기선 끝나지 않아야 한다.
            Assert.IsFalse(f.Rule.IsMatchOver);

            f.Fall(f.TeamBodies(2)[1], 1100);
            f.State.LastTick = 1100 + DodgeRuleSystem.EndGraceTicks - 1;
            Assert.IsFalse(f.Rule.IsMatchOver);
            f.State.LastTick = 1100 + DodgeRuleSystem.EndGraceTicks;
            Assert.IsTrue(f.Rule.IsMatchOver);
        }

        [Test]
        public void 팀전_4대4_결과는_1위_4명_2위_4명으로_보고된다()
        {
            var f = Build(2, "u1", "u2", "u3", "u4", "u5", "u6", "u7", "u8");
            foreach (var id in f.TeamBodies(2)) f.Fall(id, 100);
            f.State.LastTick = 400;

            var o = MatchOutcomeFinalizer.Finalize(f.Rule.ResolveOutcome(), None, None, f.Rule.IsResultSettled, 400, 0, 0.02);
            int first = 0, second = 0;
            foreach (var p in o.placements)
            {
                if (p.placement == 1) first++;
                else if (p.placement == 2) second++;
            }
            Assert.AreEqual(4, first);
            Assert.AreEqual(4, second);
        }

        [Test]
        public void 룰을_시작하면_팀이_실린_첫_상태가_바로_나간다()
        {
            var f = Build(2, "u1", "u2");
            //  판본 0이면 방송 시스템이 첫 원 공개(8초)까지 아무것도 안 보내 클라가 팀을 모른다.
            Assert.Greater(f.State.Version, 0);
            var wire = KnockbackStateBroadcastSystem.ToWire(f.State, f.Registry);
            Assert.AreEqual(2, wire.TeamCount);
            Assert.AreEqual(1, wire.Players[0].Team);
            Assert.AreEqual(2, wire.Players[1].Team);
        }

        [Test]
        public void 개인전은_탈락_역순_그대로()
        {
            var f = Build(0, "u1", "u2", "u3");
            f.Fall(f.State.Players[0], 100);
            f.Fall(f.State.Players[1], 200);
            f.State.LastTick = 400;

            var o = MatchOutcomeFinalizer.Finalize(f.Rule.ResolveOutcome(), None, None, f.Rule.IsResultSettled, 400, 0, 0.02);
            Assert.AreEqual(1, PlacementOf(o, "u3"));
            Assert.AreEqual(2, PlacementOf(o, "u2"));
            Assert.AreEqual(3, PlacementOf(o, "u1"));
        }
    }
}
