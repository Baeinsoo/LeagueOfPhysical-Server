using GameFramework.World;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class MountedArcheryZoneSystemTests
    {
        const float Dt = 0.02f;
        const long FirstReveal = 1500;   // 30초 / 0.02

        static readonly MountedArcheryConfig Config = new MountedArcheryConfig(
            150f, 40f, 80f, 2.5f,
            new MountSettings(13f, 9f, 16f, 172f, 92f, 100f, 0.35f, 0.5f),
            100, 34, 0.5f, 100f, 30f,
            new[]
            {
                new MountedArcheryZoneStageConfig(10f, 23f, 60f, 4f),
                new MountedArcheryZoneStageConfig(10f, 23f, 30f, 7f),
            },
            4f, 90f, 0.6f, 0.5f);

        sealed class Fx
        {
            public readonly EntityRegistry Registry = new EntityRegistry();
            public readonly MountedArcheryMatchState State = new MountedArcheryMatchState();
            public readonly System.Collections.Generic.List<string> Despawned = new System.Collections.Generic.List<string>();
            public readonly MountedArcheryZoneSystem Zone;

            public Fx(long startTick = 0)
            {
                var world = new StubWorld { GameplayStartTick = startTick };
                Zone = new MountedArcheryZoneSystem(State, Config, Registry, new HealthSystem(), world,
                    new System.Random(1), id => Despawned.Add(id), Dt);
            }

            public Entity Rider(string id, float x, float z, int hp = 100)
            {
                var e = new Entity(id);
                e.Add(new GameFramework.World.Transform { Position = new System.Numerics.Vector3(x, 0f, z) });
                e.Add(new Health(hp));
                Registry.Add(e);
                State.Add(id, "u-" + id, id);
                return e;
            }

            public void Run(long from, long toInclusive)
            {
                for (long t = from; t <= toInclusive; t++) Zone.Tick(t, Dt);
            }
        }

        // 시스템이 보는 것은 GameplayStartTick뿐이다.
        sealed class StubWorld : IWorld
        {
            public long GameplayStartTick { get; set; }
            public EntityRegistry EntityRegistry => null;
            public WorldEventBuffer EventBuffer => null;
            public void Tick(long tick, float deltaTime) { }
            public void SaveState(long tick) { }
            public bool LoadState(long tick) => false;
            public long? FirstSavedTick => null;
            public long? LatestSavedTick => null;
            public bool TryGetSavedMotion(long tick, string entityId, out GameFramework.Netcode.EntitySnapshot snapshot)
            {
                snapshot = default;
                return false;
            }
        }

        [Test]
        public void 출발_30초에_1단계가_공개된다()
        {
            var fx = new Fx(startTick: 10);
            fx.Run(10, 10 + FirstReveal - 1);
            Assert.AreEqual(0, fx.State.ZoneStages.Count);

            int before = fx.State.Version;
            fx.Zone.Tick(10 + FirstReveal, Dt);

            Assert.AreEqual(1, fx.State.ZoneStages.Count);
            var s = fx.State.ZoneStages[0];
            Assert.AreEqual(10 + FirstReveal, s.revealTick);
            Assert.AreEqual(10 + FirstReveal + 500, s.shrinkStartTick);
            Assert.AreEqual(10 + FirstReveal + 500 + 1150, s.shrinkEndTick);
            Assert.AreEqual(100f, s.fromRadius, 1e-4f);
            Assert.AreEqual(60f, s.toRadius, 1e-4f);
            Assert.Greater(fx.State.Version, before);
            //  새 원은 지금 원 안에 통째로 들어간다
            Assert.LessOrEqual(s.toCenter.magnitude, 100f - 60f + 1e-3f);

            //  2단계는 1단계 축소가 끝나는 틱에 공개된다
            fx.Run(10 + FirstReveal + 1, s.shrinkEndTick);
            Assert.AreEqual(2, fx.State.ZoneStages.Count);
            Assert.AreEqual(s.shrinkEndTick, fx.State.ZoneStages[1].revealTick);
            Assert.AreEqual(s.toRadius, fx.State.ZoneStages[1].fromRadius, 1e-4f);
        }

        [Test]
        public void 원_밖_피해는_소수를_모아_깎는다()
        {
            var fx = new Fx();
            var e = fx.Rider("a", 150f, 0f);
            var health = e.Get<Health>();

            fx.Run(0, FirstReveal + 11);   // 12틱: 0.96
            Assert.AreEqual(100, health.Current);
            fx.Zone.Tick(FirstReveal + 12, Dt);   // 13틱째: 1.04
            Assert.AreEqual(99, health.Current);

            fx.Run(FirstReveal + 13, FirstReveal + 49);   // 50틱
            Assert.AreEqual(96, health.Current);
        }

        [Test]
        public void 원_안이면_안_깎는다()
        {
            var fx = new Fx();
            var e = fx.Rider("a", 50f, 0f);
            fx.Run(0, FirstReveal + 100);
            Assert.AreEqual(100, e.Get<Health>().Current);
        }

        [Test]
        public void 원_밖에서_쓰러지면_처치자가_없다()
        {
            var fx = new Fx();
            var e = fx.Rider("a", 150f, 0f, hp: 1);
            fx.Rider("b", 0f, 0f);

            fx.Run(0, FirstReveal + 12);

            Assert.AreEqual(0, e.Get<Health>().Current);
            Assert.IsFalse(fx.State.Players["a"].Alive);
            Assert.AreEqual("", fx.State.Players["a"].KillerEntityId);
            Assert.AreEqual(FirstReveal + 12, fx.State.Players["a"].EliminatedTick);
            CollectionAssert.AreEqual(new[] { "a" }, fx.Despawned);
            Assert.IsTrue(fx.State.Players["b"].Alive);
        }

        [Test]
        public void 출발_전엔_원이_안_움직인다()
        {
            var fx = new Fx(startTick: long.MaxValue);
            var e = fx.Rider("a", 150f, 0f);
            fx.Run(0, FirstReveal * 2);
            Assert.AreEqual(0, fx.State.ZoneStages.Count);
            Assert.AreEqual(100, e.Get<Health>().Current);
        }
    }
}
