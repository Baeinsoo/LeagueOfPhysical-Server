using System.Collections.Generic;
using GameFramework;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 넉백 총 상태(남은 탄·충전 시계·연사 대기)가 스냅에 실린다. 안 실리면 클라 예측이 서버와 한 번 갈린 뒤
    /// (입력 유실 등) 탄이 가득 찰 때까지 탄 칸 표시와 실제 발사가 어긋난다.
    /// </summary>
    public class KnockbackGunSnapTests
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
        public void 총_상태를_스냅에_싣는다()
        {
            var registry = new GameFramework.World.EntityRegistry();
            var body = Body("body-1");
            body.Add(new KnockbackGun(3) { Ammo = 1, NextReloadTick = 140, CooldownEndTick = 113 });
            registry.Add(body);

            var snap = SnapOf(registry, "body-1");
            Assert.AreEqual(1, snap.KnockbackAmmo);
            Assert.AreEqual(140, snap.KnockbackNextReloadTick);
            Assert.AreEqual(113, snap.KnockbackCooldownEndTick);
        }

        [Test]
        public void 가득이면_충전_시계는_long_MaxValue_그대로()
        {
            var registry = new GameFramework.World.EntityRegistry();
            var body = Body("body-1");
            body.Add(new KnockbackGun(3));
            registry.Add(body);
            registry.Add(Body("rock"));

            Assert.AreEqual(3, SnapOf(registry, "body-1").KnockbackAmmo);
            Assert.AreEqual(long.MaxValue, SnapOf(registry, "body-1").KnockbackNextReloadTick);
            Assert.AreEqual(0, SnapOf(registry, "rock").KnockbackNextReloadTick, "총이 없는 몸은 0 — 다른 모드 스냅을 키우지 않는다");
        }
    }
}
