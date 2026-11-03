using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 클라가 예측하는 "내 몸이 남의 탄에 맞음"(KnockbackWorld Detection)이 서버 KnockbackHitSystem과 같은 틱에 같은 값을 내는지
    /// 같은 입력으로 나란히 돌려 본다(§3.3.2). 그리고 서버 월드에서는 그 예측이 돌지 않는지.
    /// </summary>
    public class KnockbackOwnHitParityTests
    {
        const float Dt = 0.02f;
        const long FireTick = 10;

        static readonly KnockbackConfig C = new KnockbackConfig(120f, 6.2f, 14f, 17f, 1.5f, 0f, 5f, 11f,
            17f, 13f, 0.28f, 0.6f, 3, 0.95f, 0.26f, 1.2f, 12, 7f, 0.12f, 0.2f, 0.18f, 0.006f, 1.6f, 0.8f, 5f,
            new List<MountedArcheryZoneStageConfig>());

        sealed class NeverHit : GameFramework.Physics.ICollisionQuery
        {
            public GameFramework.Physics.CollisionHit CapsuleCast(Vector3 p1, Vector3 p2, float radius, Vector3 direction, float distance, int layerMask)
                => GameFramework.Physics.CollisionHit.None;
            public GameFramework.Physics.CollisionHit Raycast(Vector3 origin, Vector3 direction, float distance, int layerMask)
                => GameFramework.Physics.CollisionHit.None;
            public GameFramework.Physics.CollisionHit[] OverlapSphere(Vector3 center, float radius, int layerMask)
                => System.Array.Empty<GameFramework.Physics.CollisionHit>();
        }

        sealed class NoopMotionBridge : GameFramework.World.IMotionBridge
        {
            public void SyncTransforms() { }
            public System.Numerics.Vector3 Depenetrate(GameFramework.World.Entity entity) => System.Numerics.Vector3.Zero;
            public void Separate(GameFramework.World.Entity entity) { }
            public void PushMotion(GameFramework.World.Entity entity) { }
        }

        sealed class FakeSink : GameFramework.World.IEventSink
        {
            public readonly List<GameFramework.World.WorldEvent> Got = new List<GameFramework.World.WorldEvent>();
            public void Emit(IReadOnlyList<GameFramework.World.WorldEvent> events) => Got.AddRange(events);
        }

        static GameFramework.World.Entity Body(string id, float x, float z, bool simulated)
        {
            var e = new GameFramework.World.Entity(id);
            e.Add(new GameFramework.World.Transform { Position = new System.Numerics.Vector3(x, 0f, z) });
            e.Add(new GameFramework.World.Velocity());
            e.Add(new GameFramework.World.CapsuleShape(BodySizes.CharacterRadius, BodySizes.CharacterHeight));
            e.Add(new GameFramework.World.GroundState { IsGrounded = true });
            e.Add(new InputBuffer());
            if (simulated)
            {
                e.Add(new GameFramework.World.Simulated());
            }
            e.Add(new KnockbackGun(C.AmmoMax));
            e.Add(new KnockbackBody());
            e.Add(new EntityKind(EntityType.Character));
            return e;
        }

        static KnockbackWorld World(GameFramework.World.EntityRegistry registry, KnockbackOwnHitPrediction prediction)
        {
            var floor = C.CreateFloor();
            return new KnockbackWorld(registry, new GameFramework.World.WorldEventBuffer(),
                C.CreateMoveSystem(floor, new NeverHit(), ~0),
                new NoopMotionBridge(), floor,
                new KnockbackFireSystem(C),
                new BodyCollisionSystem(BodySizes.CharacterRadius, BodySizes.CharacterHeight, C.BodyRestitution, new Vector3(1, 0, 1)),
                C, null, null, prediction)
            { GameplayStartTick = 0 };
        }

        sealed class Server
        {
            public readonly GameFramework.World.EntityRegistry Registry = new GameFramework.World.EntityRegistry();
            public readonly KnockbackMatchState State = new KnockbackMatchState();
            public readonly FakeSink Sink = new FakeSink();
            public readonly KnockbackWorld World;
            public readonly KnockbackHitSystem Hits;

            //  서버 스코프처럼 예측 설정 없이 만든다. 서버에서는 모든 몸이 시뮬 대상이다.
            public Server(float meX, float meZ)
            {
                Registry.Add(Body("foe", 0f, 0f, simulated: true));
                Registry.Add(Body("me", meX, meZ, simulated: true));
                State.Players.Add("foe");
                State.Players.Add("me");
                World = KnockbackOwnHitParityTests.World(Registry, null);
                Hits = new KnockbackHitSystem(World, Registry, State, C, Sink, new KnockbackPillars(KnockbackPillarLayout.None, C.CreateFloor()), Dt);
            }

            public void Tick(long t)
            {
                World.Tick(t, Dt);
                Hits.Tick(t, Dt);
            }
        }

        sealed class Client
        {
            public readonly GameFramework.World.EntityRegistry Registry = new GameFramework.World.EntityRegistry();
            public readonly KnockbackWorld World;

            //  클라처럼 내 몸만 시뮬하고, 남은 시뮬하지 않는다.
            public Client(float meX, float meZ)
            {
                Registry.Add(Body("foe", 0f, 0f, simulated: false));
                Registry.Add(Body("me", meX, meZ, simulated: true));
                World = KnockbackOwnHitParityTests.World(Registry, new KnockbackOwnHitPrediction(_ => 0));
            }

            public void Tick(long t)
            {
                World.Tick(t, Dt);
                World.SaveState(t);
            }
        }

        static void SetInput(GameFramework.World.EntityRegistry registry, long t)
        {
            //  내 몸은 탄 쪽으로 걸어 들어간다 — 맞을 때 달리던 속도가 일부 남는 것까지 같아야 한다.
            registry.Get("me").Get<InputBuffer>().Current = new InputCommand { Horizontal = -1f, Vertical = 0f };
            registry.Get("foe").Get<InputBuffer>().Current = null;
        }

        static void AssertParity(bool bomb, float meZ)
        {
            var server = new Server(7f, meZ);
            var client = new Client(7f, meZ);
            var shot = new KnockbackShot("foe", FireTick, Vector2.zero, Vector2.right, bomb);
            server.World.Shots.Add(shot);
            client.World.IngestRemoteShot(shot);

            long serverHit = -1, clientHit = -1;
            for (long t = 1; t <= 60; t++)
            {
                SetInput(server.Registry, t);
                SetInput(client.Registry, t);
                server.Tick(t);
                client.Tick(t);

                var s = server.Registry.Get("me");
                var c = client.Registry.Get("me");
                if (serverHit < 0 && s.Get<KnockbackBody>().KnockbackEndTick != long.MinValue) serverHit = t;
                if (clientHit < 0 && c.Get<KnockbackBody>().KnockbackEndTick != long.MinValue) clientHit = t;

                Assert.AreEqual(s.Get<KnockbackBody>().DamagePercent, c.Get<KnockbackBody>().DamagePercent, $"위험도 t={t}");
                Assert.AreEqual(s.Get<KnockbackBody>().KnockbackEndTick, c.Get<KnockbackBody>().KnockbackEndTick, $"날림 끝 틱 t={t}");
                var sv = s.Get<GameFramework.World.Velocity>().Linear;
                var cv = c.Get<GameFramework.World.Velocity>().Linear;
                Assert.AreEqual(sv.X, cv.X, 1e-5f, $"속도 x t={t}");
                Assert.AreEqual(sv.Z, cv.Z, 1e-5f, $"속도 z t={t}");
                var sp = s.Get<GameFramework.World.Transform>().Position;
                var cp = c.Get<GameFramework.World.Transform>().Position;
                Assert.AreEqual(sp.X, cp.X, 1e-5f, $"자리 x t={t}");
                Assert.AreEqual(sp.Z, cp.Z, 1e-5f, $"자리 z t={t}");
            }

            Assert.Greater(serverHit, FireTick, "서버에서 실제로 맞아야 시험이 의미 있다");
            Assert.AreEqual(serverHit, clientHit, "같은 틱에 날기 시작");
            Assert.AreEqual(12, client.Registry.Get("me").Get<KnockbackBody>().DamagePercent);
        }

        [Test]
        public void 보통_탄_맞음_예측이_서버와_같은_틱_같은_값()
        {
            AssertParity(bomb: false, meZ: 0f);
        }

        [Test]
        public void 폭탄_직격_예측이_서버와_같은_틱_같은_값()
        {
            //  몸 중심을 탄 길에서 비켜 두어 "터진 자리 → 몸" 방향이 탄 방향과 다르게 한다.
            AssertParity(bomb: true, meZ: 0.4f);
        }

        [Test]
        public void 서버_월드는_맞음을_예측하지_않는다()
        {
            var server = new Server(7f, 0f);
            server.World.Shots.Add(new KnockbackShot("foe", FireTick, Vector2.zero, Vector2.right));
            var me = server.Registry.Get("me").Get<KnockbackBody>();

            for (long t = 1; t <= 60; t++)
            {
                SetInput(server.Registry, t);
                server.World.Tick(t, Dt);
                int beforeRule = me.DamagePercent;
                server.Hits.Tick(t, Dt);
                if (me.DamagePercent > 0)
                {
                    Assert.AreEqual(0, beforeRule, "월드 틱이 먼저 맞음을 넣으면 안 된다 — 서버 판정만 넣는다");
                    break;
                }
            }

            Assert.AreEqual(12, me.DamagePercent);
            Assert.AreEqual(1, server.Sink.Got.Count, "맞음 사건과 막타 크레딧은 서버 판정이 낸다");
            Assert.AreEqual("me", ((KnockbackHitEvent)server.Sink.Got[0]).victimId);
        }
    }
}
