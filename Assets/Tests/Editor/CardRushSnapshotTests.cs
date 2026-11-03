using System.Collections.Generic;
using GameFramework;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 카드 러시 선수 상태 다섯 칸이 스냅에 실린다. 안 실리면 클라가 내 선수 예측을 서버 값으로 못 맞춰
    /// 한 번 갈린 칸·멈춤이 끝까지 어긋난다. 선수가 아닌 몸은 0(다른 모드 스냅을 키우지 않게).
    /// </summary>
    public class CardRushSnapshotTests
    {
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

        private static GameFramework.World.Entity Body(string id)
        {
            var entity = new GameFramework.World.Entity(id);
            entity.Add(new GameFramework.World.Transform());
            entity.Add(new GameFramework.World.Velocity());
            return entity;
        }

        private static EntitySnap SnapOf(GameFramework.World.EntityRegistry registry, string entityId)
        {
            var manager = new SessionManager();
            var session = new RecordingSession("u1");
            manager.AddSession(session);
            new EntitySnapshotBroadcastSystem(registry, manager).Tick(1, 0.02f);
            foreach (var message in session.Sent)
            {
                if (message is EntitySnapsToC chunk)
                {
                    foreach (var snap in chunk.EntitySnaps)
                    {
                        if (snap.EntityId == entityId)
                        {
                            return snap;
                        }
                    }
                }
            }
            return null;
        }

        [Test]
        public void 카드_칸이_스냅에_실린다()
        {
            var registry = new GameFramework.World.EntityRegistry();
            var body = Body("runner-1");
            body.Add(new CardRushRunner
            {
                State = new CardRushRunnerState { Step = 7, Attempt = 2, MoveStartTick = 1234, StunEndTick = 1300, SpinStartTick = 4321, Streak = 0, BestStreak = 9, Misses = 4 },
            });
            registry.Add(body);

            var snap = SnapOf(registry, "runner-1");
            Assert.AreEqual(7, snap.CardStep);
            Assert.AreEqual(2, snap.CardAttempt);
            Assert.AreEqual(1234L, snap.CardMoveStartTick);
            Assert.AreEqual(1300L, snap.CardStunEndTick);
            Assert.AreEqual(4321L, snap.CardSpinStartTick);
            Assert.AreEqual(0, snap.CardStreak);
        }

        [Test]
        public void 이어_맞힌_수도_실린다()
        {
            var registry = new GameFramework.World.EntityRegistry();
            var body = Body("runner-1");
            body.Add(new CardRushRunner { State = new CardRushRunnerState { Step = 3, Streak = 3 } });
            registry.Add(body);

            Assert.AreEqual(3, SnapOf(registry, "runner-1").CardStreak);
        }

        [Test]
        public void 다른_모드_몸은_0()
        {
            var registry = new GameFramework.World.EntityRegistry();
            registry.Add(Body("rock"));

            var snap = SnapOf(registry, "rock");
            Assert.AreEqual(0, snap.CardStep);
            Assert.AreEqual(0, snap.CardAttempt);
            Assert.AreEqual(0L, snap.CardMoveStartTick);
            Assert.AreEqual(0L, snap.CardStunEndTick);
            Assert.AreEqual(0L, snap.CardSpinStartTick);
            Assert.AreEqual(0, snap.CardStreak);
        }
    }
}
