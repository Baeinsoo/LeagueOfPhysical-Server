using System.Collections.Generic;
using System.Reflection;
using LOP.Event.Entity;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;
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

        private sealed class SpeedStubCreator : ICharacterCreator
        {
            private readonly GameFramework.World.EntityRegistry registry;
            public SpeedStubCreator(GameFramework.World.EntityRegistry registry) { this.registry = registry; }
            public void Create(CharacterCreationData data)
            {
                var e = new GameFramework.World.Entity(data.entityId);
                var stats = new GameFramework.World.Stats();
                stats.BaseStats[(int)GameFramework.World.EntityStatType.MoveSpeed] = 3f;
                e.Add(stats);
                registry.Add(e);
            }
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
            public LOP.TriGrab.TriGrabMatch Match = new LOP.TriGrab.TriGrabMatch(1);
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
                new TriGrabBotFill(() => fillSwitch, LOP.TriGrab.TriGrabConfig.MinPlayersWithBots));
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
            Assert.AreSame(container.Resolve<LOP.TriGrab.TriGrabMatch>(), container.Resolve<LOP.TriGrab.TriGrabMatch>());

            var systems = (System.Collections.IDictionary)typeof(GameFramework.Runner.RunnerBase)
                .GetField("_tickSystems", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(runner);
            var begin = (System.Collections.IList)systems[typeof(LOP.Event.LOPRunner.Update.Begin)];
            var end = (System.Collections.IList)systems[typeof(LOP.Event.LOPRunner.Update.End)];
            Assert.Contains(container.Resolve<TriGrabBotSystem>(), begin, "봇 입력은 Begin — 빠지면 봇이 영영 안 움직인다");
            Assert.Contains(container.Resolve<TriGrabMatchSystem>(), end);
            Assert.AreSame(presence, container.Resolve<TriGrabMatchSystem>().Presence, "나간 사람을 알 수 있게 주입된다");
            Assert.Contains(container.Resolve<TriGrabMoveGateSystem>(), begin, "이동 막기는 Begin — 입력 확정 앞");
            Assert.Less(begin.IndexOf(container.Resolve<TriGrabBotSystem>()), begin.IndexOf(container.Resolve<TriGrabMoveGateSystem>()),
                "봇 입력이 들어간 뒤에 막는다");
            Assert.Contains(container.Resolve<TriGrabStateBroadcastSystem>(), end);
            Assert.Less(end.IndexOf(container.Resolve<TriGrabMatchSystem>()), end.IndexOf(container.Resolve<TriGrabStateBroadcastSystem>()),
                "판 진행이 방송보다 먼저");
        }

        // ---- 봇 채우기 · 스폰 ----

        [Test]
        public void 봇_채우기는_최소_4명까지_스위치는_8까지()
        {
            var fill = new TriGrabBotFill(() => 0, LOP.TriGrab.TriGrabConfig.MinPlayersWithBots);
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
        public void Placements_ReportedToResult()
        {
            var f = BuildRule(0, "u1");
            var ids = f.Match.State.PlayerOrder;
            f.Match.State.Players[ids[0]].Score = 3;
            f.Match.State.Players[ids[1]].Score = 5;
            f.Match.State.Players[ids[2]].Score = 3;
            f.Match.State.Players[ids[3]].Score = -1;
            var outcome = f.Rule.ResolveOutcome();
            Assert.IsTrue(outcome.practice, "사람 하나면 연습");
            Assert.AreEqual(4, outcome.placements.Count);
            var byUser = new Dictionary<string, int>();
            foreach (var p in outcome.placements) byUser[p.userId] = p.placement;
            Assert.AreEqual(1, byUser[MountedArcheryBots.UserIdFor(1)], "두 번째 몸(봇 1)이 5점");
            Assert.AreEqual(2, byUser["u1"]);
            Assert.AreEqual(2, byUser[MountedArcheryBots.UserIdFor(2)], "같은 점수는 공동");
            Assert.AreEqual(4, byUser[MountedArcheryBots.UserIdFor(3)], "공동 2위 다음은 4위");
            Assert.IsFalse(TriGrabRuleSystem.IsPractice(2));
        }

        [Test]
        public void 끝은_Over가_되고_여운_150틱이_지난_뒤()
        {
            Assert.IsFalse(TriGrabRuleSystem.MatchOver(-1, 9999), "Over가 아직");
            Assert.IsFalse(TriGrabRuleSystem.MatchOver(1000, 1000 + LOP.TriGrab.TriGrabConfig.EndGraceTicks - 1));
            Assert.IsTrue(TriGrabRuleSystem.MatchOver(1000, 1000 + LOP.TriGrab.TriGrabConfig.EndGraceTicks));

            var f = BuildRule(0, "u1");
            f.Clock.LastTick = 5000;
            Assert.IsFalse(f.Rule.IsMatchOver);
            f.Clock.OverSinceTick = 4000;
            Assert.IsTrue(f.Rule.IsMatchOver);
            Assert.Greater(f.Rule.MatchDurationTicks, LOP.TriGrab.TriGrabConfig.MatchCapTicks, "러너 상한은 엔진 상한보다 길다");
            long fullRounds = LOP.TriGrab.TriGrabConfig.TotalRounds * (LOP.TriGrab.TriGrabConfig.AskTicks + LOP.TriGrab.TriGrabConfig.LiveLimitTicks + LOP.TriGrab.TriGrabConfig.ResolvedTicks);
            Assert.Greater(LOP.TriGrab.TriGrabConfig.MatchCapTicks, fullRounds, "모든 라운드가 시간 상한까지 가도 엔진 상한 안");
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
            Assert.AreEqual(LOP.TriGrab.TriGrabPhase.Waiting, f.Match.State.Phase, "GameplayStartTick 전");

            world.GameplayStartTick = 20;
            Step(system, 11, 20 + LOP.TriGrab.TriGrabConfig.AskTicks + 2);
            Assert.AreEqual(LOP.TriGrab.TriGrabPhase.Live, f.Match.State.Phase);

            string id = f.Match.State.PlayerOrder[0];
            long now = 20 + LOP.TriGrab.TriGrabConfig.AskTicks + 3;
            Assert.AreEqual(0, f.Match.State.Hooks.Count);
            f.Registry.Get(id).Get<InputBuffer>().Current = new InputCommand { Fire = true, AimYaw = 90f };
            system.Tick(now, 0.02f);
            Assert.AreEqual(1, f.Match.State.Hooks.Count, "Fire가 켜진 틱에 갈고리가 나간다");
            Assert.AreEqual(id, f.Match.State.Hooks[0].Owner);
            Assert.AreEqual(1f, f.Match.State.Hooks[0].Dir.X, 0.01f, "yaw 90도 = +x");

            f.Registry.Get(id).Get<InputBuffer>().Current = new InputCommand { Fire = false };
            system.Tick(now + 1, 0.02f);
            Assert.AreEqual(1, f.Match.State.Hooks.Count, "Fire가 꺼지면 더 안 나간다");
        }

        [Test]
        public void FireInput_StartsHook()
        {
            var f = BuildRule(0, "u1");
            var world = new FakeWorld(f.Registry, new GameFramework.World.WorldEventBuffer()) { GameplayStartTick = 0 };
            var system = new TriGrabMatchSystem(f.Match, f.Registry, world, f.Clock);
            Step(system, 0, LOP.TriGrab.TriGrabConfig.AskTicks + 2);
            string id = f.Match.State.PlayerOrder[0];
            long now = LOP.TriGrab.TriGrabConfig.AskTicks + 3;
            f.Registry.Get(id).Get<InputBuffer>().Current = new InputCommand { Fire = true, AimYaw = 0f };
            system.Tick(now, 0.02f);
            var hook = f.Match.HookOf(id);
            Assert.IsNotNull(hook);
            var body = f.Registry.Get(id).Get<GameFramework.World.Transform>().Position;
            Assert.AreEqual(body.X, hook.Origin.X, 0.01f, "몸 자리에서 나간다");
            Assert.AreEqual(body.Z, hook.Origin.Y, 0.01f);
            Assert.IsNull(f.Match.HookOf(f.Match.State.PlayerOrder[1]), "쏘지 않은 사람은 손이 없다");
        }

        [Test]
        public void HookOut_ZeroesMovement()
        {
            var f = BuildRule(0, "u1");
            var world = new FakeWorld(f.Registry, new GameFramework.World.WorldEventBuffer()) { GameplayStartTick = 0 };
            var system = new TriGrabMatchSystem(f.Match, f.Registry, world, f.Clock);
            var gate = new TriGrabMoveGateSystem(f.Registry, f.Match);
            var inputs = new InputBufferSystem();
            Step(system, 0, LOP.TriGrab.TriGrabConfig.AskTicks + 2);
            string a = f.Match.State.PlayerOrder[0], b = f.Match.State.PlayerOrder[1];
            long now = LOP.TriGrab.TriGrabConfig.AskTicks + 3;

            f.Registry.Get(a).Get<InputBuffer>().Current = new InputCommand { Fire = true };
            system.Tick(now, 0.02f);
            Assert.IsNotNull(f.Match.HookOf(a), "전제: 손이 나갔다");

            long next = now + 1;
            inputs.Enqueue(f.Registry.Get(a).Get<InputBuffer>(), next, new InputCommand { SequenceNumber = 1, Horizontal = 1f, Vertical = 1f });
            inputs.Enqueue(f.Registry.Get(b).Get<InputBuffer>(), next, new InputCommand { SequenceNumber = 1, Horizontal = 1f, Vertical = 1f });
            gate.Tick(next, 0.02f);
            var ca = inputs.Consume(f.Registry.Get(a).Get<InputBuffer>(), next);
            var cb = inputs.Consume(f.Registry.Get(b).Get<InputBuffer>(), next);
            Assert.AreEqual(0f, ca.Horizontal); Assert.AreEqual(0f, ca.Vertical);
            Assert.AreEqual(1f, cb.Horizontal, "양성 대조 — 손이 없는 사람은 그대로");
            Assert.AreEqual(1f, cb.Vertical);
        }

        [Test]
        public void Stunned_ZeroesMovementAndKeepsFire()
        {
            var f = BuildRule(0, "u1");
            var gate = new TriGrabMoveGateSystem(f.Registry, f.Match);
            var inputs = new InputBufferSystem();
            string a = f.Match.State.PlayerOrder[0];
            f.Match.State.Players[a].StunUntilTick = 100;
            var buffer = f.Registry.Get(a).Get<InputBuffer>();
            inputs.Enqueue(buffer, 50, new InputCommand { SequenceNumber = 1, Horizontal = 1f, Fire = true, AimYaw = 30f });
            gate.Tick(50, 0.02f);
            var c = inputs.Consume(buffer, 50);
            Assert.AreEqual(0f, c.Horizontal);
            Assert.IsTrue(c.Fire);
            Assert.AreEqual(30f, c.AimYaw);

            inputs.Enqueue(buffer, 100, new InputCommand { SequenceNumber = 2, Horizontal = 1f });
            gate.Tick(100, 0.02f);
            Assert.AreEqual(1f, inputs.Consume(buffer, 100).Horizontal, "기절이 끝난 틱엔 걷는다");
        }

        [Test]
        public void CharacterCreator_SetsTriGrabWalkSpeed()
        {
            var registry = new GameFramework.World.EntityRegistry();
            var inner = new SpeedStubCreator(registry);
            var creator = new TriGrabCharacterCreator(inner, registry);
            creator.Create(new CharacterCreationData { entityId = "e1", userId = "u" });
            var stats = registry.Get("e1").Get<GameFramework.World.Stats>();
            Assert.AreEqual(7f, stats.BaseStats[(int)GameFramework.World.EntityStatType.MoveSpeed]);
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
            long overTick = -1;
            for (long t = 1; t <= LOP.TriGrab.TriGrabConfig.MatchCapTicks; t++)
            {
                system.Tick(t, 0.02f);
                if (f.Match.State.Phase == LOP.TriGrab.TriGrabPhase.Over) { overTick = t; break; }
            }
            Assert.Greater(overTick, 0, "엔진 상한 안에 Over가 된다");
            Assert.AreEqual(overTick, f.Clock.OverSinceTick);
            Assert.AreEqual(overTick, f.Clock.LastTick);
        }

        // ---- 방송 ----

        [Test]
        public void 방송은_판본이_바뀔_때만_한_번씩_다시_연결된_세션은_다시_받는다()
        {
            var match = new LOP.TriGrab.TriGrabMatch(1);
            var s = new FakeSession { sessionId = "s1" };
            var sessions = new FakeSessions(); sessions.All.Add(s);
            var system = new TriGrabStateBroadcastSystem(match, sessions);
            int States() { int n = 0; foreach (var m in s.Sent) if (m is TriGrabRingStateToC) n++; return n; }

            system.Tick(1, 0.02f);
            Assert.AreEqual(0, s.Sent.Count, "판본 0은 안 보낸다");

            match.State.Version++;
            system.Tick(2, 0.02f);
            system.Tick(3, 0.02f);
            Assert.AreEqual(1, States());

            match.State.Version++;
            system.Tick(4, 0.02f);
            Assert.AreEqual(2, States());

            s.isConnected = false;
            system.Tick(5, 0.02f);
            s.isConnected = true;
            system.Tick(6, 0.02f);
            Assert.AreEqual(3, States(), "끊겼다 돌아오면 같은 판본도 다시");
        }

        private sealed class ReliableSession : GameFramework.ISession
        {
            public string sessionId { get; set; } = "s";
            public string userId { get; set; }
            public bool isConnected { get; set; } = true;
            public readonly List<(GameFramework.IMessage msg, bool reliable)> Sent = new List<(GameFramework.IMessage, bool)>();
            public void Send<T>(T message, bool reliable = true) where T : GameFramework.IMessage => Sent.Add((message, reliable));
            public GameFramework.IMessage Receive() => null;
            public void Dispose() { }
        }

        [Test]
        public void Broadcast_SendsCardsEveryTwoTicksDuringLive()
        {
            var f = BuildRule(0, "u1");
            var world = new FakeWorld(f.Registry, new GameFramework.World.WorldEventBuffer()) { GameplayStartTick = 0 };
            var matchSystem = new TriGrabMatchSystem(f.Match, f.Registry, world, f.Clock);
            var session = new ReliableSession();
            var sessions = new FakeSessions(); sessions.All.Add(session);
            var broadcast = new TriGrabStateBroadcastSystem(f.Match, sessions);

            long t = 0;
            for (; f.Match.State.Phase != LOP.TriGrab.TriGrabPhase.Live && t < 500; t++)
            {
                matchSystem.Tick(t, 0.02f);
                broadcast.Tick(t, 0.02f);
            }
            Assert.AreEqual(LOP.TriGrab.TriGrabPhase.Live, f.Match.State.Phase);
            foreach (var (m, _) in session.Sent) Assert.IsNotInstanceOf<TriGrabCardsToC>(m, "Live 전엔 카드 자리를 안 보낸다");
            var stateMsg = session.Sent.Find(x => x.msg is TriGrabRingStateToC);
            Assert.IsTrue(stateMsg.reliable, "상태는 신뢰 전송");

            session.Sent.Clear();
            long start = t;
            for (; t < start + 10; t++)
            {
                matchSystem.Tick(t, 0.02f);
                broadcast.Tick(t, 0.02f);
            }
            var cards = session.Sent.FindAll(x => x.msg is TriGrabCardsToC);
            Assert.AreEqual(5, cards.Count, "10틱 동안 짝수 틱마다");
            foreach (var (m, reliable) in cards)
            {
                Assert.IsFalse(reliable, "카드 자리는 비신뢰");
                Assert.AreEqual(0, ((TriGrabCardsToC)m).Tick % 2);
            }
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
            for (; t <= LOP.TriGrab.TriGrabConfig.AskTicks + 5; t++) matchSystem.Tick(t, 0.02f);
            Assert.AreEqual(LOP.TriGrab.TriGrabPhase.Live, f.Match.State.Phase);

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
