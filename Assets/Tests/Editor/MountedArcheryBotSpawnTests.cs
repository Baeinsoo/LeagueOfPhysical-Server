using System;
using System.Collections.Generic;
using GameFramework;
using LOP.Event.Entity;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class MountedArcheryBotSpawnTests
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

        private sealed class RecordingSession : ISession
        {
            public readonly List<object> Sent = new List<object>();
            public RecordingSession(string userId) { this.userId = userId; }
            public string sessionId => "s-" + userId;
            public string userId { get; }
            public bool isConnected => true;
            public void Send<T>(T message, bool reliable = true) where T : IMessage => Sent.Add(message);
            public IMessage Receive() => null;
            public void Dispose() { }
        }

        private sealed class Fixture
        {
            public GameFramework.World.EntityRegistry Registry = new GameFramework.World.EntityRegistry();
            public MountedArcheryMatchState State = new MountedArcheryMatchState();
            public EntitySpawner Spawner;
            public MountedArcheryRuleSystem Rule;
        }

        private static MountedArcheryConfig Config() => new MountedArcheryConfig(150f, 10f, 30f, 2.5f,
            new MountSettings(12f, 20f, 25f, 180f, 60f, 90f, 0.5f, 0.6f, 3f), 100, 34, 0.5f, 100f, 30f, null, 4f, 90f, 0.6f, 0.5f, 15f, 2.5f, 0.2f, 0.4f, 60f);

        private static Fixture Build(int fillTo, params string[] humans)
        {
            var f = new Fixture();
            f.Spawner = new EntitySpawner(
                sessionManager: null,
                entityRegistry: f.Registry,
                characterCreator: new MountedArcheryPlayerCreator(f.Registry),
                itemCreator: null,
                coinCreator: null,
                entityCreatedPublisher: new FakePublisher<EntityCreated>(),
                entityDestroyedPublisher: new FakePublisher<EntityDestroyed>());
            var store = new FakeRoomDataStore { match = new Match { playerList = humans } };
            f.Rule = new MountedArcheryRuleSystem(store, f.Spawner, Config(), f.State, () => fillTo);
            f.Rule.Initialize();
            return f;
        }

        private static int BotCount(Fixture f)
        {
            int n = 0;
            foreach (var p in f.State.Players.Values)
            {
                if (MountedArcheryBots.IsBot(p.UserId)) n++;
            }
            return n;
        }

        [Test]
        public void 스위치_8_사람_1이면_봇_7()
        {
            var f = Build(8, "u1");
            Assert.AreEqual(8, f.State.Players.Count);
            Assert.AreEqual(7, BotCount(f));
        }

        [Test]
        public void 사람_8이면_봇_0()
        {
            var f = Build(8, "u1", "u2", "u3", "u4", "u5", "u6", "u7", "u8");
            Assert.AreEqual(0, BotCount(f));
            Assert.AreEqual(8, f.State.Players.Count);
        }

        [Test]
        public void 스위치_꺼지면_봇_0()
        {
            var f = Build(0, "u1");
            Assert.AreEqual(0, BotCount(f));
            Assert.AreEqual(1, f.State.Players.Count);
        }

        [Test]
        public void 목표_인원은_8을_넘지_않는다()
        {
            var f = Build(999, "u1");
            Assert.AreEqual(7, BotCount(f));
            Assert.AreEqual(8, f.State.Players.Count);
        }

        [Test]
        public void 봇_이름과_아이디는_번호로_짓는다()
        {
            var f = Build(3, "u1");
            var names = new List<string>();
            foreach (var p in f.State.Players.Values)
            {
                if (MountedArcheryBots.IsBot(p.UserId)) names.Add(p.UserId + "/" + p.DisplayName);
            }
            CollectionAssert.AreEquivalent(new[] { "bot-1/봇 1", "bot-2/봇 2" }, names);
            Assert.IsFalse(MountedArcheryBots.IsBot("u1"));
            Assert.IsFalse(MountedArcheryBots.IsBot(null));
        }

        [Test]
        public void 봇은_사람_다음_출발_자리()
        {
            //  맵 표식이 없으면 둘레 등간격 폴백 — 사람과 봇이 한 줄 번호를 이어 쓴다.
            //  봇이 0번부터 다시 세면 사람 자리와 겹쳐 말끼리 박힌다.
            var f = Build(3, "u1");
            var positions = new List<Vector3>();
            foreach (var e in f.Registry.All)
            {
                positions.Add(GameFramework.World.EntityMotionExtensions.GetPosition(e));
            }
            Assert.AreEqual(3, positions.Count);
            Assert.AreEqual(3, new HashSet<Vector3>(positions).Count, "세 자리는 서로 달라야 한다");
        }

        [Test]
        public void 봇은_InputBuffer를_가진다()
        {
            var f = Build(2, "u1");
            string botEntityId = null;
            foreach (var kv in f.State.Players)
            {
                if (MountedArcheryBots.IsBot(kv.Value.UserId)) botEntityId = kv.Key;
            }
            var entity = f.Registry.Get(botEntityId);
            Assert.IsNotNull(entity.Get<InputBuffer>());
            Assert.IsTrue(new InputBufferSystem().Enqueue(entity.Get<InputBuffer>(), 5, new InputCommand()));
            Assert.AreEqual("bot-1", entity.Get<GameFramework.World.Ownership>().OwnerId);
        }

        [Test]
        public void 봇은_세션_없이도_스냅샷에_실린다()
        {
            var f = Build(2, "u1");
            var manager = new SessionManager();
            var human = new RecordingSession("u1");
            manager.AddSession(human);

            new EntitySnapshotBroadcastSystem(f.Registry, manager).Tick(1, 0.02f);

            var ids = new HashSet<string>();
            foreach (var msg in human.Sent)
            {
                if (msg is EntitySnapsToC snaps)
                {
                    foreach (var s in snaps.EntitySnaps) ids.Add(s.EntityId);
                }
            }
            Assert.AreEqual(2, ids.Count);
        }

        [Test]
        public void 입력_타이밍_피드백은_세션_없는_봇을_건너뛴다()
        {
            //  세션 조회가 없는 키에 예외를 던진다 — 봇 입력에 도착 기록이 쌓이면 이 시스템이 터진다.
            var f = Build(2, "u1");
            foreach (var e in f.Registry.All)
            {
                e.Get<InputBuffer>().TimingTracker.RecordArrival(1);
            }
            var manager = new SessionManager();
            var human = new RecordingSession("u1");
            manager.AddSession(human);

            Assert.DoesNotThrow(() => new InputTimingFeedbackSystem(f.Registry, manager).Tick(0, 0.02f));
            int feedback = 0;
            foreach (var msg in human.Sent) if (msg is InputTimingToC) feedback++;
            Assert.AreEqual(1, feedback, "사람 몫 하나만 나간다");
        }

        [Test]
        public void 사람이_나가도_봇은_남는다()
        {
            var f = Build(3, "u1");
            string humanEntity = null;
            foreach (var kv in f.State.Players)
            {
                if (kv.Value.UserId == "u1") humanEntity = kv.Key;
            }
            f.State.Eliminate(humanEntity, "", 10);

            Assert.AreEqual(2, f.State.AliveCount);
            Assert.IsFalse(f.Rule.IsMatchOver);
            Assert.IsFalse(f.Rule.IsResultSettled("bot-1"));
            Assert.IsTrue(f.Rule.IsResultSettled("u1"));
        }
    }
}
