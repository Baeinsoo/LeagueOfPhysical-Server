using System.Collections.Generic;
using NUnit.Framework;

namespace LOP.Tests
{
    public class KnockbackHitSystemTests
    {
        const float Dt = 0.02f;

        static readonly KnockbackConfig C = new KnockbackConfig(120f, 6f, 10f, 12f, 2f, 0.5f, 5f, 11f, 30f, 18f, 0.3f, 0.55f, 3, 0.95f, 0.26f, 1.2f, 12, 6f, 0.2f, 0.5f, 0.3f, 0.1f, 0.2f, 0.8f, 8f, new List<MountedArcheryZoneStageConfig>());

        sealed class FakeSink : GameFramework.World.IEventSink
        {
            public readonly List<GameFramework.World.WorldEvent> Got = new List<GameFramework.World.WorldEvent>();
            public void Emit(IReadOnlyList<GameFramework.World.WorldEvent> events) => Got.AddRange(events);
        }

        GameFramework.World.EntityRegistry registry;
        KnockbackMatchState state;
        KnockbackWorld world;
        FakeSink sink;
        KnockbackHitSystem system;

        [SetUp]
        public void SetUp()
        {
            registry = new GameFramework.World.EntityRegistry();
            state = new KnockbackMatchState();
            sink = new FakeSink();
            world = new KnockbackWorld(registry, new GameFramework.World.WorldEventBuffer(), null, null,
                C.CreateFloor(), null, null, C) { GameplayStartTick = 0 };
            system = new KnockbackHitSystem(world, registry, state, C, sink, Dt);
            AddBody("a", 0f, 0f, 0f);
            AddBody("b", 3f, 0f, 0f);
            world.Shots.Add(new KnockbackShot("a", 10, new UnityEngine.Vector2(0f, 0f), new UnityEngine.Vector2(1f, 0f)));
        }

        void AddBody(string id, float x, float y, float z)
        {
            var e = new GameFramework.World.Entity(id);
            e.Add(new GameFramework.World.Transform { Position = new System.Numerics.Vector3(x, y, z) });
            e.Add(new GameFramework.World.Velocity());
            e.Add(new KnockbackBody());
            registry.Add(e);
            state.Players.Add(id);
        }

        [Test]
        public void 맞으면_피해와_넉백()
        {
            int version = state.Version;
            system.Tick(14, Dt);

            var body = registry.Get("b").Get<KnockbackBody>();
            Assert.AreEqual(12, body.DamagePercent);
            Assert.AreEqual(8.4f, registry.Get("b").Get<GameFramework.World.Velocity>().Linear.X, 1e-3f);
            Assert.AreEqual(0, world.Shots.Shots.Count);
            Assert.AreEqual(1, sink.Got.Count);
            var hit = (KnockbackHitEvent)sink.Got[0];
            Assert.AreEqual(("a", 10L, "b"), (hit.shooterId, hit.fireTick, hit.victimId));
            Assert.Greater(state.Version, version);

            //  탄이 목록에서 빠졌으니 다음 틱에 또 맞지 않는다
            system.Tick(15, Dt);
            Assert.AreEqual(12, body.DamagePercent);
            Assert.AreEqual(1, sink.Got.Count);
        }

        [Test]
        public void 떨어지는_몸은_안_맞는다()
        {
            registry.Get("b").Get<GameFramework.World.Transform>().Position = new System.Numerics.Vector3(3f, -1f, 0f);
            system.Tick(14, Dt);
            Assert.AreEqual(0, registry.Get("b").Get<KnockbackBody>().DamagePercent);
            Assert.AreEqual(1, world.Shots.Shots.Count);
            Assert.AreEqual(0, sink.Got.Count);
        }

        [Test]
        public void 탈락한_몸은_안_맞는다()
        {
            state.Eliminate("b", 5);
            system.Tick(14, Dt);
            Assert.AreEqual(0, registry.Get("b").Get<KnockbackBody>().DamagePercent);
        }
    }
}
