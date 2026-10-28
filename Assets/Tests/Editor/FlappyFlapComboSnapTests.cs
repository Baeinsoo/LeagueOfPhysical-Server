using System.Collections.Generic;
using GameFramework;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 연속 날갯짓 수(spec 2026-10-27 §4)가 스냅에 실리고, 서버가 만드는 새가 그 칸을 갖는다.
    /// 안 실리면 클라는 늘 0을 보고 스냅마다 되돌리고, 칸이 없으면 서버가 안 세서 고비마다 게이지가 갈린다.
    /// </summary>
    public class FlappyFlapComboSnapTests
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

        private static FlappyConfig Config()
            => new FlappyConfig(forwardSpeed: 11f, flapImpulse: 23f, gravity: 70f, maxFallSpeed: 30f,
                                bodyRadius: 0.45f, bodyHeight: 0.9f, restitution: 0.35f,
                                stunTime: 0.8f, invulnTime: 0.6f,
                                dashMult: 2f, dashDuration: 0.2f, dashChargeBase: 0.13f, dashChargeDive: 1.2f);

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
        public void 연속_날갯짓_수를_스냅에_싣는다()
        {
            var registry = new GameFramework.World.EntityRegistry();
            var bird = Body("bird-1");
            bird.Add(new FlappyFlapCombo { Count = 37 });
            registry.Add(bird);
            registry.Add(Body("rock"));

            Assert.AreEqual(37, SnapOf(registry, "bird-1").FlapCombo);
            Assert.AreEqual(0, SnapOf(registry, "rock").FlapCombo, "연속 칸이 없는 몸은 0");
        }

        [Test]
        public void 새_만드는_곳이_연속_칸을_붙인다()
        {
            var registry = new GameFramework.World.EntityRegistry();

            new FlappyBirdCreator(registry, Config()).Create(new CharacterCreationData
            {
                userId = "u1",
                entityId = "e1",
                visualId = "",
                characterCode = "",
            });

            Assert.IsNotNull(registry.Get("e1").Get<FlappyFlapCombo>());
        }
    }
}
