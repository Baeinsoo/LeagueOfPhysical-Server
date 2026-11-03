using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VContainer;

namespace LOP.Tests
{
    /// <summary>
    /// 클라가 예측하는 "내 몸이 남의 탄에 맞음"(KnockbackWorld Detection)이 서버 KnockbackHitSystem과 같은 틱에 같은 값을 내는지
    /// 같은 입력으로 나란히 돌려 본다(§3.3.2) — 처음부터 안 탄, 늦게 알아 되감은 탄, 기둥이 있는 판.
    /// 그리고 실제 서버 스코프 배선의 월드는 그 예측을 하지 않는지.
    /// </summary>
    public class KnockbackOwnHitParityTests
    {
        const float Dt = 0.02f;
        const long FireTick = 10;
        const long Now = 45;

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

        sealed class FixedSeed : IMatchSeed
        {
            public ulong Value => 1;
        }

        readonly List<System.IDisposable> disposables = new List<System.IDisposable>();

        [TearDown]
        public void DisposeAll()
        {
            foreach (var d in disposables) d.Dispose();
            disposables.Clear();
        }

        static GameFramework.World.Entity Body(string id, Vector2 at, bool simulated)
        {
            var e = new GameFramework.World.Entity(id);
            e.Add(new GameFramework.World.Transform { Position = new System.Numerics.Vector3(at.x, 0f, at.y) });
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

        KnockbackWorld World(GameFramework.World.EntityRegistry registry, KnockbackPillars pillars, KnockbackOwnHitPrediction prediction)
        {
            var floor = C.CreateFloor();
            KnockbackPillarColliders colliders = null;
            if (pillars != null)
            {
                colliders = new KnockbackPillarColliders(pillars);
                disposables.Add(colliders);
            }
            return new KnockbackWorld(registry, new GameFramework.World.WorldEventBuffer(),
                C.CreateMoveSystem(floor, new NeverHit(), ~0),
                new NoopMotionBridge(), floor,
                new KnockbackFireSystem(C),
                new BodyCollisionSystem(BodySizes.CharacterRadius, BodySizes.CharacterHeight, C.BodyRestitution, new Vector3(1, 0, 1)),
                C, pillars, colliders, prediction)
            { GameplayStartTick = 0 };
        }

        /// <summary>판 하나의 조건: 내 몸 자리·걷는 방향, 탄 방향, 기둥 배치.</summary>
        sealed class Setup
        {
            public Vector2 MeAt = new Vector2(7f, 0f);
            public Vector2 Walk = new Vector2(-1f, 0f);
            public Vector2 ShotDirection = Vector2.right;
            public float MeZOffset;
            public bool Bomb;
            public KnockbackPillarLayout Pillars;
        }

        sealed class Server
        {
            public readonly GameFramework.World.EntityRegistry Registry = new GameFramework.World.EntityRegistry();
            public readonly KnockbackMatchState State = new KnockbackMatchState();
            public readonly FakeSink Sink = new FakeSink();
            public KnockbackWorld World;
            public KnockbackHitSystem Hits;

            public void Tick(long t)
            {
                World.Tick(t, Dt);
                Hits.Tick(t, Dt);
            }
        }

        //  서버처럼: 모든 몸이 시뮬 대상, 예측 설정 없음, 맞음은 End의 KnockbackHitSystem.
        Server MakeServer(Setup s)
        {
            var server = new Server();
            server.Registry.Add(Body("foe", Vector2.zero, simulated: true));
            server.Registry.Add(Body("me", s.MeAt, simulated: true));
            server.State.Players.Add("foe");
            server.State.Players.Add("me");
            var floor = C.CreateFloor();
            var pillars = s.Pillars != null ? new KnockbackPillars(s.Pillars, floor) : null;
            server.World = World(server.Registry, pillars, null);
            server.Hits = new KnockbackHitSystem(server.World, server.Registry, server.State, C, server.Sink,
                pillars ?? new KnockbackPillars(KnockbackPillarLayout.None, floor), Dt);
            return server;
        }

        //  클라처럼: 내 몸만 시뮬, 남은 시뮬하지 않음, 예측 켬.
        (KnockbackWorld world, GameFramework.World.EntityRegistry registry) MakeClient(Setup s)
        {
            var registry = new GameFramework.World.EntityRegistry();
            registry.Add(Body("foe", Vector2.zero, simulated: false));
            registry.Add(Body("me", s.MeAt, simulated: true));
            var pillars = s.Pillars != null ? new KnockbackPillars(s.Pillars, C.CreateFloor()) : null;
            return (World(registry, pillars, new KnockbackOwnHitPrediction(_ => 0)), registry);
        }

        static void SetInput(GameFramework.World.EntityRegistry registry, Setup s)
        {
            registry.Get("me").Get<InputBuffer>().Current = new InputCommand { Horizontal = s.Walk.x, Vertical = s.Walk.y };
            registry.Get("foe").Get<InputBuffer>().Current = null;
        }

        static KnockbackShot ShotOf(Setup s) => new KnockbackShot("foe", FireTick, new Vector2(0f, s.MeZOffset), s.ShotDirection, s.Bomb);

        struct MeState
        {
            public int Damage;
            public long End;
            public System.Numerics.Vector3 Position;
            public System.Numerics.Vector3 Velocity;
        }

        static MeState Read(GameFramework.World.EntityRegistry registry)
        {
            var me = registry.Get("me");
            return new MeState
            {
                Damage = me.Get<KnockbackBody>().DamagePercent,
                End = me.Get<KnockbackBody>().KnockbackEndTick,
                Position = me.Get<GameFramework.World.Transform>().Position,
                Velocity = me.Get<GameFramework.World.Velocity>().Linear,
            };
        }

        static void AssertSame(MeState s, MeState c, string label)
        {
            Assert.AreEqual(s.Damage, c.Damage, $"위험도 {label}");
            Assert.AreEqual(s.End, c.End, $"날림 끝 틱 {label}");
            Assert.AreEqual(s.Velocity.X, c.Velocity.X, 1e-5f, $"속도 x {label}");
            Assert.AreEqual(s.Velocity.Z, c.Velocity.Z, 1e-5f, $"속도 z {label}");
            Assert.AreEqual(s.Position.X, c.Position.X, 1e-5f, $"자리 x {label}");
            Assert.AreEqual(s.Position.Z, c.Position.Z, 1e-5f, $"자리 z {label}");
        }

        //  서버와 클라를 틱마다 나란히 돌려 비교하고, 서버의 틱별 값을 돌려준다.
        Dictionary<long, MeState> RunSideBySide(Setup s, bool expectHit)
        {
            var server = MakeServer(s);
            var (client, registry) = MakeClient(s);
            server.World.Shots.Add(ShotOf(s));
            client.IngestRemoteShot(ShotOf(s));

            var history = new Dictionary<long, MeState>();
            long serverHit = -1, clientHit = -1;
            for (long t = 1; t <= Now; t++)
            {
                SetInput(server.Registry, s);
                SetInput(registry, s);
                server.Tick(t);
                client.Tick(t, Dt);
                client.SaveState(t);

                var a = Read(server.Registry);
                var b = Read(registry);
                history[t] = a;
                if (serverHit < 0 && a.End != long.MinValue) serverHit = t;
                if (clientHit < 0 && b.End != long.MinValue) clientHit = t;
                AssertSame(a, b, $"t={t}");
            }

            if (expectHit)
            {
                Assert.Greater(serverHit, FireTick, "서버에서 실제로 맞아야 시험이 의미 있다");
                Assert.AreEqual(serverHit, clientHit, "같은 틱에 날기 시작");
                Assert.AreEqual(12, Read(registry).Damage);
            }
            else
            {
                Assert.AreEqual(-1, serverHit, "서버도 안 맞는다");
            }
            return history;
        }

        [Test]
        public void 보통_탄_맞음_예측이_서버와_같은_틱_같은_값()
        {
            RunSideBySide(new Setup(), expectHit: true);
        }

        [Test]
        public void 폭탄_직격_예측이_서버와_같은_틱_같은_값()
        {
            //  탄 길을 몸 중심에서 비켜 두어 "터진 자리 → 몸" 방향이 탄 방향과 다르게 한다.
            RunSideBySide(new Setup { Bomb = true, MeZOffset = -0.4f }, expectHit: true);
        }

        [Test]
        public void 늦게_알아_되감은_예측이_서버와_같은_값()
        {
            var s = new Setup();
            var serverHistory = RunSideBySide(s, expectHit: true);

            //  클라가 탄 사실을 맞은 틱이 지난 뒤에야 안다(클라가 서버보다 앞서 달려 늘 이렇다).
            var (late, registry) = MakeClient(s);
            for (long t = 1; t <= Now; t++)
            {
                SetInput(registry, s);
                late.Tick(t, Dt);
                late.SaveState(t);
            }
            Assert.IsTrue(late.IngestRemoteShot(ShotOf(s), long.MinValue, out long? replayFrom));
            Assert.IsNotNull(replayFrom);
            Assert.IsTrue(late.LoadState(replayFrom.Value));
            for (long t = replayFrom.Value + 1; t <= Now; t++)
            {
                SetInput(registry, s);
                late.Tick(t, Dt);
                late.SaveState(t);
                AssertSame(serverHistory[t], Read(registry), $"재생 t={t}");
            }
        }

        [Test]
        public void 기둥이_있는_판에서도_서버와_같다()
        {
            var layout = new KnockbackPillarLayout(1, 3f, 0.9f, 3f);
            var dir = new KnockbackPillars(layout, C.CreateFloor()).Center(0).normalized;

            //  기둥 바로 뒤에 선 몸 — 서버도 클라도 안 맞는다.
            RunSideBySide(new Setup { Pillars = layout, MeAt = dir * 6f, Walk = Vector2.zero, ShotDirection = dir }, expectHit: false);

            //  기둥이 있지만 탄 길에서 벗어난 몸 — 둘 다 같은 틱에 맞는다.
            var side = new Vector2(-dir.y, dir.x);
            RunSideBySide(new Setup { Pillars = layout, MeAt = side * 6f, Walk = Vector2.zero, ShotDirection = side }, expectHit: true);
        }

        [Test]
        public void 서버_스코프_배선의_월드는_맞음을_예측하지_않는다()
        {
            //  실제 서버 스코프의 월드 등록(KnockbackLifetimeScope.RegisterWorld)을 그대로 지난다.
            var builder = new ContainerBuilder();
            builder.RegisterInstance(C);
            builder.RegisterInstance<IMatchSeed>(new FixedSeed());
            builder.RegisterInstance(new GameFramework.World.EntityRegistry());
            builder.RegisterInstance(new GameFramework.World.WorldEventBuffer());
            builder.RegisterInstance<GameFramework.World.IMotionBridge>(new NoopMotionBridge());
            builder.RegisterInstance<GameFramework.Physics.ICollisionQuery>(new NeverHit());
            KnockbackLifetimeScope.RegisterWorld(builder);

            using (var container = builder.Build())
            {
                var world = container.Resolve<KnockbackWorld>();
                world.GameplayStartTick = 0;
                var registry = world.EntityRegistry;
                registry.Add(Body("foe", Vector2.zero, simulated: true));
                registry.Add(Body("me", new Vector2(7f, 0f), simulated: true));
                var state = new KnockbackMatchState();
                state.Players.Add("foe");
                state.Players.Add("me");
                var sink = new FakeSink();
                var hits = new KnockbackHitSystem(world, registry, state, C, sink, container.Resolve<KnockbackPillars>(), Dt);
                world.Shots.Add(new KnockbackShot("foe", FireTick, Vector2.zero, Vector2.right));
                var s = new Setup();

                for (long t = 1; t <= Now && sink.Got.Count == 0; t++)
                {
                    SetInput(registry, s);
                    world.Tick(t, Dt);
                    Assert.AreEqual(0, registry.Get("me").Get<KnockbackBody>().DamagePercent, "월드 틱이 먼저 맞음을 넣으면 안 된다");
                    hits.Tick(t, Dt);
                }

                Assert.AreEqual(12, registry.Get("me").Get<KnockbackBody>().DamagePercent);
                Assert.AreEqual(1, sink.Got.Count, "맞음 사건과 막타 크레딧은 서버 판정이 낸다");
                Assert.AreEqual("me", ((KnockbackHitEvent)sink.Got[0]).victimId);
            }
        }
    }
}
