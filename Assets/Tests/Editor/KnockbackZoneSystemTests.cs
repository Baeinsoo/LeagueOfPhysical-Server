using System.Collections.Generic;
using NUnit.Framework;

namespace LOP.Tests
{
    public class KnockbackZoneSystemTests
    {
        const float Dt = 0.02f;

        static readonly KnockbackConfig C = new KnockbackConfig(120f, 6f, 10f, 12f, 2f, 0.5f, 5f, 11f, 30f, 18f, 0.3f, 0.55f, 3, 0.95f, 0.26f, 1.2f, 12, 6f, 0.2f, 0.5f, 0.3f, 0.1f, 0.2f, 0.8f, 8f,
            new List<MountedArcheryZoneStageConfig>
            {
                new MountedArcheryZoneStageConfig(10f, 23f, 3f, 0f),
                new MountedArcheryZoneStageConfig(10f, 23f, 1f, 0f),
            });

        KnockbackMatchState state;
        KnockbackWorld world;
        KnockbackZoneSystem system;

        [SetUp]
        public void SetUp()
        {
            state = new KnockbackMatchState();
            world = new KnockbackWorld(new GameFramework.World.EntityRegistry(), new GameFramework.World.WorldEventBuffer(),
                null, null, C.CreateFloor(), null, null, C) { GameplayStartTick = 100 };
            system = new KnockbackZoneSystem(state, C, world, new System.Random(1), Dt);
        }

        [Test]
        public void 원은_8초에_공개되고_다음은_축소_끝에()
        {
            system.Tick(100 + 399, Dt);
            Assert.AreEqual(0, state.ZoneStages.Count);

            system.Tick(100 + 400, Dt);
            Assert.AreEqual(1, state.ZoneStages.Count);
            var first = state.ZoneStages[0];
            Assert.AreEqual(100 + 400, first.revealTick);
            Assert.AreEqual(world.Floor.ZoneStartRadius, first.fromRadius, 1e-4f);
            Assert.AreEqual(3f, first.toRadius, 1e-4f);

            system.Tick(first.shrinkEndTick - 1, Dt);
            Assert.AreEqual(1, state.ZoneStages.Count);
            system.Tick(first.shrinkEndTick, Dt);
            Assert.AreEqual(2, state.ZoneStages.Count);
            Assert.AreEqual(first.shrinkEndTick, state.ZoneStages[1].revealTick);
        }

        [Test]
        public void 출발_전에는_공개하지_않는다()
        {
            system.Tick(99, Dt);
            Assert.AreEqual(0, state.ZoneStages.Count);
        }

        [Test]
        public void 공개되면_바닥이_무너진다()
        {
            Assert.IsTrue(world.Floor.IsSolid(11f, 0f, 100000));
            Assert.IsTrue(world.Floor.IsSolid(-11f, 0f, 100000));

            system.Tick(100 + 400, Dt);
            long end = state.ZoneStages[0].shrinkEndTick;
            //  반지름 3짜리 원은 서로 반대편의 두 가장자리를 함께 덮을 수 없다
            Assert.IsFalse(world.Floor.IsSolid(11f, 0f, end) && world.Floor.IsSolid(-11f, 0f, end));
        }
    }
}
