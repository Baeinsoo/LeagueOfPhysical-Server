using System.Collections.Generic;
using System.Reflection;
using LOP.Event.Entity;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;
using VContainer;

namespace LOP.Tests
{
    /// <summary>삼색 그랩 서버 — 스코프 배선, 룰(스폰·봇 채우기·등수·끝), 판 진행, 와이어 왕복, 봇 입력.</summary>
    public class TriGrabServerTests
    {
        private sealed class FakePublisher<T> : IPublisher<T> { public void Publish(T message) { } }

        private sealed class FakeRoomDataStore : IRoomDataStore
        {
            public Room room { get; set; }
            public Match match { get; set; }
            public MatchOutcome outcome { get; set; }
            public IReadOnlyDictionary<string, PlayerLookDto> looks { get; set; }
            public void Clear() { }
        }

        private sealed class Seed : IMatchSeed { public ulong Value => 7UL; }

        private sealed class FakeWorld : GameFramework.World.WorldBase
        {
            public FakeWorld(GameFramework.World.EntityRegistry registry, GameFramework.World.WorldEventBuffer buffer)
                : base(registry, buffer) { }
        }

        //  몸 하나를 월드에 세우는 가짜 — 위치·소유자·입력 버퍼만 있으면 판 진행과 봇이 쓴다.
        private sealed class FakeCreator : ICharacterCreator
        {
            private readonly GameFramework.World.EntityRegistry registry;
            public readonly List<CharacterCreationData> Created = new List<CharacterCreationData>();
            public FakeCreator(GameFramework.World.EntityRegistry registry) { this.registry = registry; }
            public void Create(CharacterCreationData data)
            {
                Created.Add(data);
                var e = new GameFramework.World.Entity(data.entityId);
                e.Add(new GameFramework.World.Transform { Position = new System.Numerics.Vector3(data.position.x, 0, data.position.z) });
                e.Add(new GameFramework.World.Ownership(data.userId));
                e.Add(new InputBuffer());
                registry.Add(e);
            }
        }

        private sealed class FakeSession : GameFramework.ISession
        {
            public string sessionId { get; set; }
            public string userId { get; set; }
            public bool isConnected { get; set; } = true;
            public readonly List<GameFramework.IMessage> Sent = new List<GameFramework.IMessage>();
            public void Send<T>(T message, bool reliable = true) where T : GameFramework.IMessage => Sent.Add(message);
            public GameFramework.IMessage Receive() => null;
            public void Dispose() { }
        }

        private sealed class FakeSessions : GameFramework.ISessionManager
        {
            public readonly List<GameFramework.ISession> All = new List<GameFramework.ISession>();
            public IReadOnlyCollection<GameFramework.ISession> GetAllSessions() => All;
            public void AddSession(GameFramework.ISession session) => All.Add(session);
            public void RemoveSession(GameFramework.ISession session) => All.Remove(session);
            public GameFramework.ISession GetSessionById(string sessionId) => throw new System.NotImplementedException();
            public GameFramework.ISession GetSessionByUserId(string userId) => throw new System.NotImplementedException();
            public T GetSessionById<T>(string sessionId) where T : GameFramework.ISession => throw new System.NotImplementedException();
            public T GetSessionByUserId<T>(string userId) where T : GameFramework.ISession => throw new System.NotImplementedException();
            public bool TryGetSessionById(string sessionId, out GameFramework.ISession session) => throw new System.NotImplementedException();
            public bool TryGetSessionByUserId<T>(string userId, out T session) where T : GameFramework.ISession => throw new System.NotImplementedException();
            public void RemoveSessionById(string sessionId) => throw new System.NotImplementedException();
            public void RemoveSessionByUserId(string userId) => throw new System.NotImplementedException();
        }

        private readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned) if (go != null) Object.DestroyImmediate(go);
            spawned.Clear();
        }

        private sealed class Fixture
        {
            public GameFramework.World.EntityRegistry Registry = new GameFramework.World.EntityRegistry();
            public FakeCreator Creator;
            public TriGrabMatch Match = new TriGrabMatch(1);
            public TriGrabRunClock Clock = new TriGrabRunClock();
            public TriGrabRuleSystem Rule;
        }

        private static Fixture BuildRule(int fillSwitch, params string[] humans)
        {
            var f = new Fixture();
            f.Creator = new FakeCreator(f.Registry);
            var spawner = new EntitySpawner(null, f.Registry, f.Creator, null, null,
                new FakePublisher<EntityCreated>(), new FakePublisher<EntityDestroyed>());
            var store = new FakeRoomDataStore { match = new Match { playerList = humans } };
            f.Rule = new TriGrabRuleSystem(store, spawner, f.Match, f.Clock,
                new TriGrabBotFill(() => fillSwitch, TriGrabConfig.MinPlayersWithBots));
            f.Rule.Initialize();
            return f;
        }

        // ---- 스코프 배선 ----

        [Test]
        public void 스코프_등록만으로_룰_판진행_방송_봇이_만들어지고_페이즈에_물린다()
        {
            var go = new GameObject("tri-grab-scope-test");
            spawned.Add(go);
            var runner = go.AddComponent<LOPRunner>();

            var registry = new GameFramework.World.EntityRegistry();
            var builder = new ContainerBuilder();
            builder.RegisterInstance(registry);
            builder.Register<GameFramework.World.WorldEventBuffer>(Lifetime.Singleton);
            builder.RegisterInstance<IRoomDataStore>(new FakeRoomDataStore { match = new Match { playerList = new[] { "a" } } });
            builder.RegisterInstance<IMatchSeed>(new Seed());
            builder.RegisterInstance<GameFramework.ISessionManager>(new FakeSessions());
            builder.Register<GameFramework.World.IWorld>(
                c => new FakeWorld(c.Resolve<GameFramework.World.EntityRegistry>(), c.Resolve<GameFramework.World.WorldEventBuffer>()),
                Lifetime.Singleton);
            builder.RegisterInstance<ICharacterCreator>(new FakeCreator(registry));
            builder.RegisterInstance(new EntitySpawner(null, registry, new FakeCreator(registry), null, null,
                new FakePublisher<EntityCreated>(), new FakePublisher<EntityDestroyed>()));
            builder.Register<InputBufferSystem>(Lifetime.Singleton);
            var presence = new PlayerPresence();   // 실제로는 방 스코프(RoomLifetimeScope)가 등록한다
            builder.RegisterInstance(presence);

            TriGrabLifetimeScope.RegisterGame(builder, runner);
            var container = builder.Build();

            Assert.IsInstanceOf<TriGrabRuleSystem>(container.Resolve<IGameRuleSystem>());
            Assert.AreSame(container.Resolve<TriGrabMatch>(), container.Resolve<TriGrabMatch>());

            var systems = (System.Collections.IDictionary)typeof(GameFramework.Runner.RunnerBase)
                .GetField("_tickSystems", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(runner);
            var begin = (System.Collections.IList)systems[typeof(LOP.Event.LOPRunner.Update.Begin)];
            var end = (System.Collections.IList)systems[typeof(LOP.Event.LOPRunner.Update.End)];
            Assert.Contains(container.Resolve<TriGrabBotSystem>(), begin, "봇 입력은 Begin — 빠지면 봇이 영영 안 움직인다");
            Assert.Contains(container.Resolve<TriGrabMatchSystem>(), end);
            Assert.AreSame(presence, container.Resolve<TriGrabMatchSystem>().Presence, "나간 사람을 알 수 있게 주입된다");
            Assert.Contains(container.Resolve<TriGrabStateBroadcastSystem>(), end);
            Assert.Less(end.IndexOf(container.Resolve<TriGrabMatchSystem>()), end.IndexOf(container.Resolve<TriGrabStateBroadcastSystem>()),
                "판 진행이 방송보다 먼저");
        }

        // ---- 봇 채우기 · 스폰 ----

        [Test]
        public void 봇_채우기는_최소_4명까지_스위치는_8까지()
        {
            var fill = new TriGrabBotFill(() => 0, TriGrabConfig.MinPlayersWithBots);
            Assert.AreEqual(3, fill.BotsToAdd(1));
            Assert.AreEqual(0, fill.BotsToAdd(4));
            Assert.AreEqual(0, fill.BotsToAdd(6), "사람이 더 많으면 안 채운다");
            Assert.AreEqual(7, new TriGrabBotFill(() => 8, 4).BotsToAdd(1));
            Assert.AreEqual(7, new TriGrabBotFill(() => 20, 4).BotsToAdd(1), "8명을 넘기지 않는다");
        }

        [Test]
        public void 사람_하나면_봇_셋이_채워져_모두_판에_등록된다()
        {
            var f = BuildRule(0, "u1");
            Assert.AreEqual(4, f.Creator.Created.Count);
            Assert.AreEqual(4, f.Match.State.PlayerOrder.Count);
            int bots = 0;
            foreach (var c in f.Creator.Created) if (MountedArcheryBots.IsBot(c.userId)) bots++;
            Assert.AreEqual(3, bots);
            Assert.AreEqual("u1", f.Creator.Created[0].userId, "사람이 먼저");
            Assert.AreEqual(f.Creator.Created[0].entityId, f.Match.State.PlayerOrder[0], "등록 순서 = 스폰 순서");
            Assert.Greater(f.Match.State.Version, 0, "서 있는 모습을 판 시작 전에 보낸다");
        }

        [Test]
        public void 스폰_자리는_맵_자리가_없으면_반지름_7점8_고리다()
        {
            var f = BuildRule(0, "u1");
            foreach (var c in f.Creator.Created)
            {
                Assert.AreEqual(7.8f, new Vector2(c.position.x, c.position.z).magnitude, 0.01f);
            }
        }

        // ---- 등수 · 끝 ----

        [Test]
        public void 점수_내림차순_등수_같은_점수는_공동이고_다음은_건너뛴다()
        {
            var o = TriGrabRuleSystem.PlacementsByScore(new List<(string, int)> { ("a", 2), ("b", 5), ("c", 2), ("d", 0) });
            int Rank(string u) { foreach (var p in o.placements) if (p.userId == u) return p.placement; return -1; }
            Assert.AreEqual(1, Rank("b"));
            Assert.AreEqual(2, Rank("a"));
            Assert.AreEqual(2, Rank("c"));
            Assert.AreEqual(4, Rank("d"));
        }

        [Test]
        public void 결과는_엔진_누적_점수로_매기고_사람_하나면_연습이다()
        {
            var f = BuildRule(0, "u1");
            var ids = f.Match.State.PlayerOrder;
            // 누적 점수(5라운드 합). 이번 라운드 점수(RoundPoints)는 등수에 안 쓴다.
            f.Match.State.Players[ids[0]].Score = 27;
            f.Match.State.Players[ids[1]].Score = 38;
            f.Match.State.Players[ids[2]].Score = 27;
            f.Match.State.Players[ids[3]].Score = 9;
            f.Match.State.Players[ids[3]].RoundPoints = 10;
            var outcome = f.Rule.ResolveOutcome();
            Assert.IsTrue(outcome.practice);
            Assert.AreEqual(4, outcome.placements.Count);
            var byUser = new Dictionary<string, int>();
            foreach (var p in outcome.placements) byUser[p.userId] = p.placement;
            Assert.AreEqual(1, byUser[MountedArcheryBots.UserIdFor(1)], "두 번째 몸(봇 1)이 38점");
            Assert.AreEqual(2, byUser["u1"]);
            Assert.AreEqual(2, byUser[MountedArcheryBots.UserIdFor(2)], "같은 누적 점수는 공동");
            Assert.AreEqual(4, byUser[MountedArcheryBots.UserIdFor(3)], "공동 2위 다음은 4위");
            Assert.IsFalse(TriGrabRuleSystem.IsPractice(2));
        }

        [Test]
        public void 끝은_Over가_되고_여운_150틱이_지난_뒤()
        {
            Assert.IsFalse(TriGrabRuleSystem.MatchOver(-1, 9999), "Over가 아직");
            Assert.IsFalse(TriGrabRuleSystem.MatchOver(1000, 1000 + TriGrabConfig.EndGraceTicks - 1));
            Assert.IsTrue(TriGrabRuleSystem.MatchOver(1000, 1000 + TriGrabConfig.EndGraceTicks));

            var f = BuildRule(0, "u1");
            f.Clock.LastTick = 5000;
            Assert.IsFalse(f.Rule.IsMatchOver);
            f.Clock.OverSinceTick = 4000;
            Assert.IsTrue(f.Rule.IsMatchOver);
            Assert.Greater(f.Rule.MatchDurationTicks, TriGrabConfig.MatchCapTicks, "러너 상한은 엔진 상한보다 길다");
            long fiveFullRounds = TriGrabConfig.TotalRounds * (TriGrabConfig.PrepTicks + TriGrabConfig.RoundLimitTicks + TriGrabConfig.ResolvedTicks);
            Assert.Greater(TriGrabConfig.MatchCapTicks, fiveFullRounds, "다섯 라운드가 모두 시간 상한까지 가도 엔진 상한 안");
        }

        // ---- 판 진행 ----

        private static void Step(TriGrabMatchSystem system, long from, long to)
        {
            for (long t = from; t <= to; t++) system.Tick(t, 0.02f);
        }

        [Test]
        public void 시작_틱_전에는_판이_안_열리고_Live에_Fire_입력이_갈고리를_쏜다()
        {
            var f = BuildRule(0, "u1");
            var world = new FakeWorld(f.Registry, new GameFramework.World.WorldEventBuffer());
            var system = new TriGrabMatchSystem(f.Match, f.Registry, world, f.Clock);

            Step(system, 0, 10);
            Assert.AreEqual(TriGrabPhase.Waiting, f.Match.State.Phase, "GameplayStartTick 전");

            world.GameplayStartTick = 20;
            Step(system, 11, 20 + TriGrabConfig.PrepTicks + 2);
            Assert.AreEqual(TriGrabPhase.Live, f.Match.State.Phase);

            string id = f.Match.State.PlayerOrder[0];
            long now = 20 + TriGrabConfig.PrepTicks + 3;
            Assert.AreEqual(0, f.Match.State.Hooks.Count);
            f.Registry.Get(id).Get<InputBuffer>().Current = new InputCommand { Fire = true, AimYaw = 90f };
            system.Tick(now, 0.02f);
            Assert.AreEqual(1, f.Match.State.Hooks.Count, "Fire가 켜진 틱에 갈고리가 나간다");
            Assert.AreEqual(id, f.Match.State.Hooks[0].OwnerId);
            Assert.AreEqual(1f, f.Match.State.Hooks[0].Dir.x, 0.01f, "yaw 90도 = +x");

            f.Registry.Get(id).Get<InputBuffer>().Current = new InputCommand { Fire = false };
            system.Tick(now + 1, 0.02f);
            Assert.AreEqual(1, f.Match.State.Hooks.Count, "Fire가 꺼지면 더 안 나간다");
        }

        [Test]
        public void 나간_사람은_판_엔진에서_비활성이고_봇은_늘_활성()
        {
            var f = BuildRule(0, "u1");
            var world = new FakeWorld(f.Registry, new GameFramework.World.WorldEventBuffer()) { GameplayStartTick = 0 };
            var presence = new PlayerPresence();
            presence.Begin(new[] { "u1" });
            presence.MarkJoined("u1");
            presence.MarkMatchStarted();
            var system = new TriGrabMatchSystem(f.Match, f.Registry, world, f.Clock) { Presence = presence };
            string human = f.Match.State.PlayerOrder[0];

            system.Tick(0, 0.02f);
            Assert.IsFalse(f.Match.State.Inactive.Contains(human), "양성 대조 — 들어와 있으면 활성");
            presence.MarkLeft("u1");
            system.Tick(1, 0.02f);
            Assert.IsTrue(f.Match.State.Inactive.Contains(human), "나가면 비활성");
            Assert.AreEqual(1, f.Match.State.Inactive.Count, "봇은 늘 활성");
            presence.MarkJoined("u1");
            system.Tick(2, 0.02f);
            Assert.IsFalse(f.Match.State.Inactive.Contains(human), "돌아오면 다시 활성");
        }

        [Test]
        public void 판_시계는_Over를_처음_본_틱을_적고_엔진이_Over가_된다()
        {
            var f = BuildRule(0, "u1");
            var world = new FakeWorld(f.Registry, new GameFramework.World.WorldEventBuffer()) { GameplayStartTick = 0 };
            var system = new TriGrabMatchSystem(f.Match, f.Registry, world, f.Clock);
            system.Tick(0, 0.02f);
            Assert.AreEqual(-1, f.Clock.OverSinceTick);
            system.Tick(TriGrabConfig.MatchCapTicks, 0.02f);
            Assert.AreEqual(TriGrabPhase.Over, f.Match.State.Phase);
            Assert.AreEqual(TriGrabConfig.MatchCapTicks, f.Clock.OverSinceTick);
            Assert.AreEqual(TriGrabConfig.MatchCapTicks, f.Clock.LastTick);
        }

        // ---- 와이어 ----

        [Test]
        public void ToWire_3차_모양과_주문_확장을_싣는다()
        {
            var state = new TriGrabMatchState
            {
                Phase = TriGrabPhase.Live, Round = 5, Version = 3,
                Rule = new TriGrabRule(TriGrabRuleType.Ink, (int)TriGrabColor.Red, (int)TriGrabShape.Square, true, (int)TriGrabColor.Blue),
            };
            state.Slots[2].Card = new TriGrabCard { Id = 4, Word = 4, Ink = 1, Shape = (int)TriGrabShape.Triangle };
            var w = TriGrabStateBroadcastSystem.ToWire(state);
            Assert.AreEqual((int)TriGrabShape.Square + 1, w.RuleShapePlus1);
            Assert.IsTrue(w.RuleNegate);
            Assert.AreEqual((int)TriGrabColor.Blue + 1, w.RuleDecoyInkPlus1);
            Assert.AreEqual((int)TriGrabShape.Triangle, w.Slots[2].Card.Shape);
            Assert.AreEqual(4, w.Slots[2].Card.Word, "모양 이름 말 번호(3~5)도 그대로");

            var plain = TriGrabStateBroadcastSystem.ToWire(new TriGrabMatchState { Rule = new TriGrabRule(TriGrabRuleType.Shape, 1), Version = 1 });
            Assert.AreEqual(0, plain.RuleShapePlus1, "거르기 없음 = 0");
            Assert.AreEqual(0, plain.RuleDecoyInkPlus1, "속임 없음 = 0");
            Assert.IsFalse(plain.RuleNegate);
        }

        [Test]
        public void ToWire_왕복_모든_필드가_상태와_같다()
        {
            var state = new TriGrabMatchState
            {
                Phase = TriGrabPhase.Live, PhaseEndTick = 900, Round = 3,
                Rule = new TriGrabRule(TriGrabRuleType.Word, (int)TriGrabColor.Blue),
                Clock = new TriGrabRoundClock { BaseAngle0 = 1.5f, Omega = 0.6f, LiveStartTick = 400, FreezeTick = long.MaxValue },
                RoundWinnerId = "p2", MatchWinnerId = "", Version = 9,
                RoundLimitTick = 1400,
            };
            state.Slots[0].Card = new TriGrabCard { Id = 7, Word = 1, Ink = 2, Cycles = true, PeriodTicks = 30, T0Tick = 400 };
            state.Slots[0].EmptyUntilTick = 0;
            state.Slots[3].EmptyUntilTick = 555;   // 빈 슬롯
            state.Hooks.Add(new TriGrabHook
            {
                OwnerId = "p1", FireTick = 410, Origin = new Vector2(1, 2), Dir = new Vector2(0, 1),
                TurnTick = 428, TurnLength = 9f, Result = TriGrabHookResult.Correct, EndTick = 470,
                Carried = new TriGrabCard { Id = 8, Word = 0, Ink = 0, Cycles = false, PeriodTicks = 0, T0Tick = 400 },
            });
            state.Hooks.Add(new TriGrabHook { OwnerId = "p2", FireTick = 420, Dir = new Vector2(1, 0), Result = TriGrabHookResult.None });
            foreach (var id in new[] { "p2", "p1" })
            {
                state.PlayerOrder.Add(id);
                state.Players[id] = new TriGrabPlayerScore
                {
                    Score = id == "p1" ? 2 : 4, Mistakes = 1, ReloadUntilTick = 77, BestRtMs = 480,
                    RoundPlace = id == "p1" ? 0 : 2, RoundPoints = id == "p1" ? 0 : 9,
                };
            }

            var w = TriGrabStateBroadcastSystem.ToWire(state);
            Assert.AreEqual(9, w.Version);
            Assert.AreEqual((int)TriGrabPhase.Live, w.Phase);
            Assert.AreEqual(900L, w.PhaseEndTick);
            Assert.AreEqual(3, w.Round);
            Assert.AreEqual((int)TriGrabRuleType.Word, w.RuleType);
            Assert.AreEqual((int)TriGrabColor.Blue, w.RuleColor);
            Assert.AreEqual(1.5f, w.BaseAngle0);
            Assert.AreEqual(0.6f, w.Omega);
            Assert.AreEqual(400L, w.LiveStartTick);
            Assert.AreEqual(long.MaxValue, w.FreezeTick);
            Assert.AreEqual("p2", w.RoundWinnerId);
            Assert.AreEqual("", w.MatchWinnerId);
            Assert.AreEqual(TriGrabConfig.TotalRounds, w.TotalRounds);
            Assert.AreEqual(5, w.TotalRounds);
            Assert.AreEqual(1400L, w.RoundLimitTick);
            Assert.AreEqual(0, w.TargetScore, "목표 점수는 2차부터 안 쓴다");

            Assert.AreEqual(TriGrabConfig.Slots, w.Slots.Count);
            Assert.AreEqual(7, w.Slots[0].Card.Id);
            Assert.AreEqual(1, w.Slots[0].Card.Word);
            Assert.AreEqual(2, w.Slots[0].Card.Ink);
            Assert.IsTrue(w.Slots[0].Card.Cycles);
            Assert.AreEqual(30, w.Slots[0].Card.PeriodTicks);
            Assert.AreEqual(400L, w.Slots[0].Card.T0Tick);
            Assert.IsNull(w.Slots[3].Card, "빈 슬롯 = 카드 없음");
            Assert.AreEqual(555L, w.Slots[3].EmptyUntilTick);

            Assert.AreEqual(2, w.Hooks.Count);
            var h = w.Hooks[0];
            Assert.AreEqual("p1", h.OwnerId);
            Assert.AreEqual(410L, h.FireTick);
            Assert.AreEqual(1f, h.OriginX); Assert.AreEqual(2f, h.OriginZ);
            Assert.AreEqual(0f, h.DirX); Assert.AreEqual(1f, h.DirZ);
            Assert.AreEqual(428L, h.TurnTick);
            Assert.AreEqual(9f, h.TurnLength);
            Assert.AreEqual((int)TriGrabHookResult.Correct, h.Result);
            Assert.AreEqual(470L, h.EndTick);
            Assert.AreEqual(8, h.Carried.Id);
            Assert.IsNull(w.Hooks[1].Carried);

            Assert.AreEqual(2, w.Players.Count);
            Assert.AreEqual("p2", w.Players[0].EntityId, "등록 순서 그대로");
            Assert.AreEqual(4, w.Players[0].Score);
            Assert.AreEqual(1, w.Players[0].Mistakes);
            Assert.AreEqual(77L, w.Players[0].ReloadUntilTick);
            Assert.AreEqual(480, w.Players[0].BestRtMs);
            Assert.AreEqual(2, w.Players[0].RoundPlace);
            Assert.AreEqual(9, w.Players[0].RoundPoints);
            Assert.AreEqual(2, w.Players[1].Score);
            Assert.AreEqual(0, w.Players[1].RoundPlace, "아직 못 냄");
            Assert.AreEqual(0, w.Players[1].RoundPoints);
        }

        [Test]
        public void 방송은_판본이_바뀔_때만_한_번씩_다시_연결된_세션은_다시_받는다()
        {
            var match = new TriGrabMatch(1);
            var s = new FakeSession { sessionId = "s1" };
            var sessions = new FakeSessions(); sessions.All.Add(s);
            var system = new TriGrabStateBroadcastSystem(match, sessions);

            system.Tick(1, 0.02f);
            Assert.AreEqual(0, s.Sent.Count, "판본 0은 안 보낸다");

            match.State.Version++;
            system.Tick(2, 0.02f);
            system.Tick(3, 0.02f);
            Assert.AreEqual(1, s.Sent.Count);

            match.State.Version++;
            system.Tick(4, 0.02f);
            Assert.AreEqual(2, s.Sent.Count);

            s.isConnected = false;
            system.Tick(5, 0.02f);
            s.isConnected = true;
            system.Tick(6, 0.02f);
            Assert.AreEqual(3, s.Sent.Count, "끊겼다 돌아오면 같은 판본도 다시");
        }

        // ---- 봇 ----

        [Test]
        public void 봇은_Live에서_입력_버퍼에_이번_틱_입력을_넣고_사람은_안_건드린다()
        {
            var f = BuildRule(0, "u1");
            var world = new FakeWorld(f.Registry, new GameFramework.World.WorldEventBuffer()) { GameplayStartTick = 0 };
            var matchSystem = new TriGrabMatchSystem(f.Match, f.Registry, world, f.Clock);
            var bots = new TriGrabBotSystem(f.Registry, f.Match, new InputBufferSystem(), new Seed(), MountedArcheryBots.IsBot);

            long t = 0;
            for (; t <= TriGrabConfig.PrepTicks + 5; t++) matchSystem.Tick(t, 0.02f);
            Assert.AreEqual(TriGrabPhase.Live, f.Match.State.Phase);

            bots.Tick(t, 0.02f);
            var order = f.Match.State.PlayerOrder;
            Assert.AreEqual(0, f.Registry.Get(order[0]).Get<InputBuffer>().Commands.Count, "사람 몸에는 안 넣는다");
            for (int i = 1; i < order.Count; i++)
            {
                var buffer = f.Registry.Get(order[i]).Get<InputBuffer>();
                Assert.AreEqual(1, buffer.Commands.Count, "봇 몸마다 이번 틱 입력 하나");
                Assert.IsTrue(buffer.Commands.ContainsKey(t));
                Assert.AreEqual(1L, buffer.Commands[t].SequenceNumber);
            }
            bots.Tick(t + 1, 0.02f);
            Assert.AreEqual(2L, f.Registry.Get(order[1]).Get<InputBuffer>().Commands[t + 1].SequenceNumber, "차례 번호가 하나씩 오른다");
        }
    }
}
