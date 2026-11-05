using System.Collections.Generic;
using GameFramework;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 카드 러시 선수 상태 16칸이 스냅에 실린다. 하나라도 안 실리면 클라가 내 선수 예측을 서버 값으로 못 맞춰
    /// 한 번 갈린 그 칸이 끝까지 어긋난다. 선수가 아닌 몸은 0(다른 모드 스냅을 키우지 않게).
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
            //  칸마다 서로 다른 값 — 두 칸을 바꿔 싣거나 한 칸을 빠뜨리면 걸린다.
            body.Add(new CardRushRunner
            {
                State = new CardRushRunnerState
                {
                    PosMm = 12345, UseCount = 17, SpinStartTick = 1001, DashStartTick = 1002, BlockEndTick = 1003,
                    StunEndTick = 1004, ObstacleIndex = 5, ObstacleTick = 1006, ZoneIndex = 3, ZoneTick = 1008,
                    ObstacleEvent = (int)CardRushObstacleEvent.Tumbled, ObstacleEventTick = 1010,
                    ZoneEvent = (int)CardRushZoneEvent.Held, ZoneEventTick = 1012, FinishTick = 1013, CleanStreak = 6,
                    BestCleanStreak = 9, Hits = 4,
                },
            });
            registry.Add(body);

            var snap = SnapOf(registry, "runner-1");
            Assert.AreEqual(12345, snap.CardPosMm);
            Assert.AreEqual(17, snap.CardUseCount);
            Assert.AreEqual(1001L, snap.CardSpinStartTick);
            Assert.AreEqual(1002L, snap.CardDashStartTick);
            Assert.AreEqual(1003L, snap.CardBlockEndTick);
            Assert.AreEqual(1004L, snap.CardStunEndTick);
            Assert.AreEqual(5, snap.CardObstacleIndex);
            Assert.AreEqual(1006L, snap.CardObstacleTick);
            Assert.AreEqual(3, snap.CardZoneIndex);
            Assert.AreEqual(1008L, snap.CardZoneTick);
            Assert.AreEqual((int)CardRushObstacleEvent.Tumbled, snap.CardObstacleEvent);
            Assert.AreEqual(1010L, snap.CardObstacleEventTick);
            Assert.AreEqual((int)CardRushZoneEvent.Held, snap.CardZoneEvent);
            Assert.AreEqual(1012L, snap.CardZoneEventTick);
            Assert.AreEqual(1013L, snap.CardFinishTick);
            Assert.AreEqual(6, snap.CardCleanStreak);
        }

        [Test]
        public void 다른_모드_몸은_0()
        {
            var registry = new GameFramework.World.EntityRegistry();
            registry.Add(Body("rock"));

            //  카드 칸 16개 전부 — 하나라도 다른 모드 몸에 값이 새면 걸린다.
            var snap = SnapOf(registry, "rock");
            Assert.AreEqual(0, snap.CardPosMm);
            Assert.AreEqual(0, snap.CardUseCount);
            Assert.AreEqual(0L, snap.CardSpinStartTick);
            Assert.AreEqual(0L, snap.CardDashStartTick);
            Assert.AreEqual(0L, snap.CardBlockEndTick);
            Assert.AreEqual(0L, snap.CardStunEndTick);
            Assert.AreEqual(0, snap.CardObstacleIndex);
            Assert.AreEqual(0L, snap.CardObstacleTick);
            Assert.AreEqual(0, snap.CardZoneIndex);
            Assert.AreEqual(0L, snap.CardZoneTick);
            Assert.AreEqual(0, snap.CardObstacleEvent);
            Assert.AreEqual(0L, snap.CardObstacleEventTick);
            Assert.AreEqual(0, snap.CardZoneEvent);
            Assert.AreEqual(0L, snap.CardZoneEventTick);
            Assert.AreEqual(0L, snap.CardFinishTick);
            Assert.AreEqual(0, snap.CardCleanStreak);
        }
    }
}
