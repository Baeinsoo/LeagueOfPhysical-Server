using System.Collections.Generic;
using LOP.Event.Entity;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>판이 시작될 때 드럼통이 씨앗에서 정해진 자리에 엔티티로 서고, 클라가 알아볼 표(종류·visual_id)를 갖는지.</summary>
    public class KnockbackBarrelSpawnTests
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

        private sealed class FixedSeed : IMatchSeed
        {
            public ulong Value { get; }
            public FixedSeed(ulong value) { Value = value; }
        }

        private static KnockbackConfig Config(int barrels)
            => new KnockbackConfig(120f, 6f, 10f, 12f, 2f, 0.5f, 5f, 11f, 30f, 18f, 0.3f, 0.55f, 3, 0.95f, 0.26f, 1.2f, 1, 6f, 0.5f, 0.5f, 0.3f, 0.1f, 0.2f, 0.8f, 5f,
                                   new List<MountedArcheryZoneStageConfig>(), new KnockbackPillarLayout(4, 9.5f, 0.9f, 3f),
                                   barrels: new KnockbackBarrelLayout(barrels, 0.55f, 9f, 2.5f, 0.6f, 6f, 1f));

        private static GameFramework.World.EntityRegistry Spawn(int barrels, ulong seed)
        {
            var registry = new GameFramework.World.EntityRegistry();
            var config = Config(barrels);
            var floor = config.CreateFloor();
            var spawner = new EntitySpawner(
                sessionManager: null,
                entityRegistry: registry,
                characterCreator: new KnockbackPlayerCreator(registry, config),
                itemCreator: null,
                coinCreator: null,
                entityCreatedPublisher: new FakePublisher<EntityCreated>(),
                entityDestroyedPublisher: new FakePublisher<EntityDestroyed>());
            var matchSeed = new FixedSeed(seed);
            var store = new FakeRoomDataStore { match = new Match { playerList = new[] { "u1", "u2" } } };
            var rule = new KnockbackRuleSystem(store, spawner, new KnockbackMatchState(), config, null, null,
                                               new KnockbackPillars(config.Pillars, floor, matchSeed), matchSeed);
            rule.Initialize();
            return registry;
        }

        private static List<GameFramework.World.Entity> Barrels(GameFramework.World.EntityRegistry registry)
        {
            var list = new List<GameFramework.World.Entity>();
            foreach (var e in registry.All) if (e.Has<KnockbackBarrel>()) list.Add(e);
            list.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return list;
        }

        [Test]
        public void 설정한_개수만큼_드럼통_엔티티가_서고_클라가_알아볼_표가_있다()
        {
            var registry = Spawn(4, 12345UL);
            var barrels = Barrels(registry);

            Assert.AreEqual(4, barrels.Count);
            foreach (var b in barrels)
            {
                Assert.AreEqual(EntityType.Barrel, b.Get<EntityKind>().Kind);
                Assert.AreEqual(KnockbackBarrel.VisualId, b.Get<Appearance>().VisualId);
                Assert.IsNull(b.Get<GameFramework.World.PhysicsConfig>(), "유니티 몸이 없어야 걸어도 벽이 아니다");
                Assert.AreEqual(0f, b.Get<GameFramework.World.Velocity>().Linear.Length(), 1e-6f);
                Assert.AreEqual(0f, b.Get<GameFramework.World.Transform>().Position.Y, 1e-6f);
            }
        }

        [Test]
        public void 자리는_씨앗이_정하고_같은_씨앗이면_같다()
        {
            var a = Barrels(Spawn(4, 777UL));
            var b = Barrels(Spawn(4, 777UL));
            var c = Barrels(Spawn(4, 778UL));

            bool anyDifferent = false;
            for (int i = 0; i < 4; i++)
            {
                var pa = a[i].Get<GameFramework.World.Transform>().Position;
                Assert.AreEqual(pa, b[i].Get<GameFramework.World.Transform>().Position);
                if (pa != c[i].Get<GameFramework.World.Transform>().Position) anyDifferent = true;
            }
            Assert.IsTrue(anyDifferent, "다른 씨앗은 다른 자리");
        }

        [Test]
        public void 개수_0인_맵에는_드럼통이_없다()
        {
            Assert.AreEqual(0, Barrels(Spawn(0, 12345UL)).Count);
        }
    }
}
