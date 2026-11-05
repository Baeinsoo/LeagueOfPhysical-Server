using System;
using System.Collections.Generic;
using GameFramework.World;
using LOP.Event.Entity;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 카드 러시 룰(서버) — 레인 출발, 완주 여운, 2분 상한, 순위(완주 순 → 칸·도착 틱), 결과 지표, 마커 검사.
    /// </summary>
    public class CardRushRuleSystemTests
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

        private sealed class Seed : IMatchSeed { public ulong Value => 7UL; }

        private static CardRushConfig Config()
            => new CardRushConfig(matchDurationSeconds: 120f, minLanes: 4, trackLengthM: 140f,
                                  obstacleGapMinM: 11f, obstacleGapMaxM: 19f, barricadeShare: 0.5f,
                                  zoneGapMinM: 28f, zoneGapMaxM: 48f, barricadeWaitSeconds: 1.2f,
                                  rollKnockM: 1.39f, rollStunSeconds: 0.7f, zoneKnockM: 2.77f, zoneStunSeconds: 1f,
                                  botReactMinSeconds: 0.2f, botReactMaxSeconds: 0.5f, botReactExtraSeconds: 0.05f,
                                  botHesitateChance: 0.1f, botFreeDashChance: 0.5f);

        private sealed class Fixture
        {
            public EntityRegistry Registry;
            public FinishTrackingSystem Finish;
            public CardRushMapRulesField Field;
            public CardRushWorld World;
            public EntitySpawner Spawner;
            public CardRushRuleSystem Rule;

            public Entity BodyOf(string userId) => Registry.Get(Spawner.GetEntityIdByUserId(userId));
        }

        private GameObject marker;

        [TearDown]
        public void TearDown()
        {
            if (marker != null)
            {
                UnityEngine.Object.DestroyImmediate(marker);
                marker = null;
            }
        }

        //  기본은 마커 있음 + 규칙 들어옴(맵 로드 뒤의 정상 상태). 진행 방향을 +x로 둬 회전이 0이 아니게 한다.
        private Fixture Build(string[] players, bool withRules = true)
        {
            var registry = new EntityRegistry();
            var field = new CardRushMapRulesField();
            if (withRules)
            {
                marker = new GameObject("card-rush-rules-test");
                marker.AddComponent<CardRushMapRules>();
                field.Set(CardRushTheme.StoneBridge, 40, 2.2f, 2.6f,
                          new Vector3(10f, 0f, 5f), Vector3.right);
            }
            var world = new CardRushWorld(registry, new WorldEventBuffer(), field, new Seed(), Config());
            var spawner = new EntitySpawner(
                sessionManager: null,
                entityRegistry: registry,
                characterCreator: new CardRushRunnerCreator(registry, field),
                itemCreator: null,
                coinCreator: null,
                entityCreatedPublisher: new FakePublisher<EntityCreated>(),
                entityDestroyedPublisher: new FakePublisher<EntityDestroyed>());
            var finish = new FinishTrackingSystem(registry);
            var rule = new CardRushRuleSystem(
                new FakeRoomDataStore { match = new Match { playerList = players } },
                spawner, registry, finish, world, Config());
            return new Fixture { Registry = registry, Finish = finish, Field = field, World = world, Spawner = spawner, Rule = rule };
        }

        private static int PlacementOf(MatchOutcome outcome, string userId)
        {
            foreach (var p in outcome.placements) if (p.userId == userId) return p.placement;
            return -1;
        }

        private static MatchPlacement RowOf(MatchOutcome outcome, string userId)
        {
            foreach (var p in outcome.placements) if (p.userId == userId) return p;
            return null;
        }

        [Test]
        public void 레인은_사람_다음_봇_순서()
        {
            var f = Build(new[] { "a", "b", "c" });
            f.Rule.Initialize();

            string[] users = { "a", "b", "c" };
            for (int i = 0; i < users.Length; i++)
            {
                var body = f.BodyOf(users[i]);
                Assert.IsNotNull(body, users[i]);
                var expected = f.Field.LaneOrigin(i);
                var runner = body.Get<CardRushRunner>();
                Assert.AreEqual(expected, runner.LaneOrigin, $"{users[i]}의 레인 출발점은 LaneOrigin({i})");
                var p = body.Get<GameFramework.World.Transform>().Position;
                Assert.AreEqual(expected.x, p.X, 1e-4f);
                Assert.AreEqual(expected.z, p.Z, 1e-4f);
                //  진행 방향(+x)을 본다 — 회전한 앞이 +x.
                var rotation = body.Get<GameFramework.World.Transform>().Rotation;
                var facing = new Quaternion(rotation.X, rotation.Y, rotation.Z, rotation.W) * Vector3.forward;
                Assert.AreEqual(1f, facing.x, 1e-3f, "진행 방향을 본다");
            }
        }

        //  서버 EntityBinder는 스폰된 모든 몸에 PhysX 대리 몸을 세운다(PhysicsBodyFactory.Create). 모양·물리 설정이 없으면
        //  첫 스폰에서 throw → 방 초기화가 끝나지 않아 7777이 안 열린다(Task 14 실제 판에서 확인).
        [Test]
        public void 서버_몸에도_물리_대리_몸을_세울_수_있다()
        {
            var f = Build(new[] { "a", "b" });
            f.Rule.Initialize();

            foreach (var user in new[] { "a", "b" })
            {
                var body = f.BodyOf(user);
                var root = new GameObject("card-rush-proxy-test");
                try
                {
                    PhysicsBody proxy = null;
                    Assert.DoesNotThrow(() => proxy = PhysicsBodyFactory.Create(root, body), user);
                    Assert.IsTrue(proxy.IsKinematic, $"{user}: 대리 몸은 아무것도 밀지 않는다(키네마틱)");
                    Assert.IsTrue(body.Get<PhysicsConfig>().IsTrigger, $"{user}: 트리거 — 선수끼리 안 부딪힌다");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        [Test]
        public void 모두_완주하고_150틱_뒤_끝()
        {
            var f = Build(new[] { "a", "b" });
            f.Rule.Initialize();
            f.BodyOf("a").Get<FinishState>().FinishedTick = 990;
            f.Finish.Tick(990, 0.02f);
            Assert.IsFalse(f.Rule.IsMatchOver, "한 명만 들어왔다");

            f.BodyOf("b").Get<FinishState>().FinishedTick = 1000;
            f.Finish.Tick(1000, 0.02f);
            Assert.IsFalse(f.Rule.IsMatchOver, "모두 들어온 그 틱엔 안 끝난다");

            f.Finish.Tick(1149, 0.02f);
            Assert.IsFalse(f.Rule.IsMatchOver, "여운 1틱 전");

            f.Finish.Tick(1150, 0.02f);
            Assert.IsTrue(f.Rule.IsMatchOver, "1000 + 150");
        }

        //  최종 리뷰 I4: 로딩 중 앱이 죽어 한 번도 못 들어온 사람의 몸은 칸 0에 서 있다. 그 몸을 기다리면
        //  나머지가 다 들어와도 2분 상한까지 관전 화면에 묶인다 — 출발 뒤엔 세지 않는다.
        [Test]
        public void 안_들어온_사람은_기다리지_않는다()
        {
            var players = new[] { "a", "b", "c", "absent" };
            var f = Build(players);
            var presence = new PlayerPresence();
            presence.Begin(players);
            presence.MarkJoined("a");
            presence.MarkJoined("b");
            presence.MarkJoined("c");
            presence.MarkMatchStarted();
            f.Rule.Presence = presence;
            f.Rule.Initialize();

            foreach (var user in new[] { "a", "b", "c" })
            {
                f.BodyOf(user).Get<FinishState>().FinishedTick = 1000;
            }
            //  러너는 매 틱 묻는다 — 조건이 처음 참이 된 틱부터 여운을 센다.
            f.Finish.Tick(1000, 0.02f);
            Assert.IsFalse(f.Rule.IsMatchOver, "여운 전");
            f.Finish.Tick(1150, 0.02f);

            Assert.IsTrue(f.Rule.IsMatchOver, "들어온 셋이 다 완주했다 — 여운 150틱 뒤 끝난다");
        }

        //  출발 전엔 아직 로딩 중일 수 있다 — 그때 빼면 먼저 온 사람만으로 "전원 완주"가 된다.
        [Test]
        public void 출발_전엔_안_들어온_사람도_기다린다()
        {
            var players = new[] { "a", "late" };
            var f = Build(players);
            var presence = new PlayerPresence();
            presence.Begin(players);
            presence.MarkJoined("a");
            f.Rule.Presence = presence;
            f.Rule.Initialize();

            f.BodyOf("a").Get<FinishState>().FinishedTick = 1000;
            f.Finish.Tick(1000, 0.02f);
            Assert.IsFalse(f.Rule.IsMatchOver);
            f.Finish.Tick(1150, 0.02f);

            Assert.IsFalse(f.Rule.IsMatchOver);
        }

        [Test]
        public void 상한은_6000틱()
        {
            var f = Build(new string[0], withRules: false);
            Assert.AreEqual(6000L, f.Rule.MatchDurationTicks);
        }

        [Test]
        public void 못_한_사람은_간_거리_큰_순()
        {
            var f = Build(new[] { "behind", "ahead", "middle" });
            f.Rule.Initialize();
            //  먼저 스폰된 "behind"가 앞에 남지 않게 — 1 mm 차이도 갈라야 한다. 장애물 번호는 안 본다(거리만).
            f.BodyOf("behind").Get<CardRushRunner>().State = new CardRushRunnerState { PosMm = 30_000, ObstacleIndex = 9 };
            f.BodyOf("ahead").Get<CardRushRunner>().State = new CardRushRunnerState { PosMm = 42_001 };
            f.BodyOf("middle").Get<CardRushRunner>().State = new CardRushRunnerState { PosMm = 42_000 };
            f.Finish.Tick(7500, 0.02f);

            var outcome = f.Rule.ResolveOutcome();
            Assert.AreEqual(1, PlacementOf(outcome, "ahead"));
            Assert.AreEqual(2, PlacementOf(outcome, "middle"));
            Assert.AreEqual(3, PlacementOf(outcome, "behind"));
        }

        [Test]
        public void 같은_틱_완주는_공동()
        {
            var f = Build(new[] { "a", "b", "c" });
            f.Rule.Initialize();
            f.BodyOf("a").Get<FinishState>().FinishedTick = 1000;
            f.BodyOf("b").Get<FinishState>().FinishedTick = 1000;
            f.BodyOf("c").Get<FinishState>().FinishedTick = 1001;
            f.Finish.Tick(1001, 0.02f);

            var outcome = f.Rule.ResolveOutcome();
            Assert.AreEqual(1, PlacementOf(outcome, "a"));
            Assert.AreEqual(1, PlacementOf(outcome, "b"));
            Assert.AreEqual(3, PlacementOf(outcome, "c"), "공동 순위는 다음 등수를 건너뛴다(1,1,3)");
            Assert.IsTrue(f.Rule.IsResultSettled("a"));
        }

        [Test]
        public void 통계_키()
        {
            var f = Build(new[] { "done", "running", "gone" });
            f.Rule.Initialize();
            var done = f.BodyOf("done");
            done.Get<CardRushRunner>().State = new CardRushRunnerState { Step = 40, MoveStartTick = 900, BestStreak = 12, Misses = 3 };
            done.Get<FinishState>().FinishedTick = 900;
            f.BodyOf("running").Get<CardRushRunner>().State = new CardRushRunnerState { Step = 20, MoveStartTick = 800, BestStreak = 5, Misses = 7 };
            f.Registry.Remove(f.Spawner.GetEntityIdByUserId("gone"));
            f.Finish.Tick(1000, 0.02f);

            var outcome = f.Rule.ResolveOutcome();
            foreach (var user in new[] { "done", "running", "gone" })
            {
                var row = RowOf(outcome, user);
                Assert.IsNotNull(row?.stats, user);
                Assert.IsTrue(row.stats.ContainsKey(CardRushStatKeys.StreakBest), user);
                Assert.IsTrue(row.stats.ContainsKey(CardRushStatKeys.Misses), user);
                Assert.IsTrue(row.stats.ContainsKey(RaceStatKeys.Finished), user);
            }
            Assert.AreEqual("cardStreakBest", CardRushStatKeys.StreakBest);
            Assert.AreEqual("cardMisses", CardRushStatKeys.Misses);
            Assert.AreEqual(12, RowOf(outcome, "done").stats[CardRushStatKeys.StreakBest]);
            Assert.AreEqual(3, RowOf(outcome, "done").stats[CardRushStatKeys.Misses]);
            Assert.AreEqual(1, RowOf(outcome, "done").stats[RaceStatKeys.Finished]);
            Assert.AreEqual(5, RowOf(outcome, "running").stats[CardRushStatKeys.StreakBest]);
            Assert.AreEqual(7, RowOf(outcome, "running").stats[CardRushStatKeys.Misses]);
            Assert.AreEqual(0, RowOf(outcome, "running").stats[RaceStatKeys.Finished]);
            Assert.AreEqual(0, RowOf(outcome, "gone").stats[CardRushStatKeys.StreakBest], "몸이 없으면 0");
            Assert.AreEqual(3, PlacementOf(outcome, "gone"), "나간 사람은 맨 뒤");
        }

        [Test]
        public void 마커가_없으면_Initialize가_말해_준다()
        {
            var f = Build(new[] { "a" }, withRules: false);
            var e = Assert.Throws<InvalidOperationException>(() => f.Rule.Initialize());
            StringAssert.Contains("CardRushMapRules", e.Message);
        }

        //  규칙 값은 들어왔는데 씬에 마커가 둘이면(맵 씬 두 장이 겹쳐 올라온 경우) 어느 쪽 규칙인지 모른다.
        [Test]
        public void 마커가_둘이면_Initialize가_말해_준다()
        {
            var f = Build(new[] { "a" });
            var second = new GameObject("card-rush-rules-test-2");
            try
            {
                second.AddComponent<CardRushMapRules>();
                var e = Assert.Throws<InvalidOperationException>(() => f.Rule.Initialize());
                StringAssert.Contains("CardRushMapRules", e.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(second);
            }
        }
    }
}
