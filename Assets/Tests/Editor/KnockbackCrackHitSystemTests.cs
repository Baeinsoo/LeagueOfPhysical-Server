using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>금 가는 바닥: 사거리 끝까지 간 보통 탄이 끝 칸에 금을 내고, 금이 모이면 칸이 무너질 틱이 정해진다.</summary>
    public class KnockbackCrackHitSystemTests
    {
        const float Dt = 0.02f;

        static KnockbackConfig Make(int crackEnabled) => new KnockbackConfig(120f, 6f, 10f, 12f, 2f, 0.5f, 5f, 11f, 30f, 18f, 0.3f, 0.55f, 3, 0.95f, 0.26f, 1.2f, 12, 6f, 0.2f, 0.5f, 0.3f, 0.1f, 0.2f, 0.8f, 8f,
            new List<MountedArcheryZoneStageConfig>(), null, 0, 0f, 1f, 20f, 20f, 1.5f, 0.9f, 2.5f, crackEnabled, 2, 1f);

        sealed class FakeSink : GameFramework.World.IEventSink
        {
            public void Emit(IReadOnlyList<GameFramework.World.WorldEvent> events) { }
        }

        KnockbackConfig config;
        GameFramework.World.EntityRegistry registry;
        KnockbackMatchState state;
        KnockbackWorld world;
        KnockbackHitSystem system;
        KnockbackPillars pillars;

        void Build(int crackEnabled, KnockbackPillarLayout layout = null)
        {
            config = Make(crackEnabled);
            registry = new GameFramework.World.EntityRegistry();
            state = new KnockbackMatchState();
            var floor = config.CreateFloor();
            pillars = new KnockbackPillars(layout ?? KnockbackPillarLayout.None, floor);
            world = new KnockbackWorld(registry, new GameFramework.World.WorldEventBuffer(), null, null,
                floor, null, null, config) { GameplayStartTick = 0 };
            system = new KnockbackHitSystem(world, registry, state, config, new FakeSink(), pillars, Dt);
        }

        void AddBody(string id, float x, float z)
        {
            var e = new GameFramework.World.Entity(id);
            e.Add(new GameFramework.World.Transform { Position = new System.Numerics.Vector3(x, 0f, z) });
            e.Add(new GameFramework.World.Velocity());
            e.Add(new KnockbackBody());
            registry.Add(e);
            state.Players.Add(id);
        }

        //  사거리 끝이 (3, 0)이 되게 (-15, 0)에서 오른쪽으로 쏜다.
        void Fire(string shooter, long fireTick, bool bomb = false)
            => world.Shots.Add(new KnockbackShot(shooter, fireTick, new Vector2(-15f, 0f), Vector2.right, bomb));

        //  월드 틱이 End 전에 하는 정리(기둥·다 쓴 탄)를 흉내 내고 판정 시스템을 돌린다.
        void Run(long from, long to)
        {
            for (long t = from; t <= to; t++)
            {
                world.Shots.RemoveBlocked(t - 1, pillars.CollectStanding, pillars.Radius, config, Dt);
                world.Shots.RemoveSpent(t, config.ShotSpeed, config.ShotRange, Dt);
                system.Tick(t, Dt);
            }
        }

        int EndTile() => world.Floor.TileIndexAt(3f, 0f);

        [Test]
        public void 사거리_끝까지_간_탄_한_발은_끝_칸에_금_하나를_낸다()
        {
            Build(1);
            Fire("a", 10);
            int version = state.Version;

            Run(10, 60);

            Assert.GreaterOrEqual(EndTile(), 0);
            Assert.AreEqual(1, state.Cracks[EndTile()].cracks);
            Assert.AreEqual(-1L, state.Cracks[EndTile()].collapseTick);
            Assert.AreEqual(KnockbackFloor.NoCollapse, world.Floor.CollapseTickOf(EndTile()));
            Assert.Greater(state.Version, version, "바뀌었으니 알린다");
            Assert.IsTrue(world.Floor.IsSolid(3f, 0f, 100));
        }

        [Test]
        public void 두_발이면_예고_시간_뒤_무너지고_그_위는_더_이상_바닥이_아니다()
        {
            Build(1);
            Fire("a", 10);
            Fire("a", 20);

            Run(10, 80);

            long second = state.Cracks[EndTile()].collapseTick;
            Assert.AreEqual(2, state.Cracks[EndTile()].cracks);
            Assert.Greater(second, 0L);
            Assert.AreEqual(second, world.Floor.CollapseTickOf(EndTile()));
            Assert.AreEqual(50, config.CrackCollapseTicks);
            Assert.IsTrue(world.Floor.IsSolid(3f, 0f, second - 1));
            Assert.IsFalse(world.Floor.IsSolid(3f, 0f, second), "무너지는 틱부터 바닥이 없다");
            var (tx, tz) = KnockbackFloor.TileAt(3f, 0f, world.Floor.TileSize);
            Assert.IsTrue(world.Floor.IsWarning(tx, tz, second - 1, config.CrackCollapseTicks));
        }

        [Test]
        public void 같은_틱에_두_탄이_같은_칸에_닿으면_한_번에_금_둘이다()
        {
            Build(1);
            Fire("a", 10);
            Fire("b", 10);

            Run(10, 60);

            Assert.AreEqual(2, state.Cracks[EndTile()].cracks);
            Assert.AreNotEqual(KnockbackFloor.NoCollapse, world.Floor.CollapseTickOf(EndTile()));
        }

        [Test]
        public void 몸에_맞은_탄은_금을_내지_않는다()
        {
            Build(1);
            AddBody("c", 0f, 0f);
            Fire("a", 10);

            Run(10, 60);

            Assert.AreEqual(0, state.Cracks.Count);
        }

        [Test]
        public void 폭탄_탄은_금을_내지_않는다()
        {
            Build(1);
            Fire("a", 10, bomb: true);

            Run(10, 60);

            Assert.AreEqual(0, state.Cracks.Count);
        }

        [Test]
        public void 기둥에_막힌_탄은_금을_내지_않는다()
        {
            Build(1, new KnockbackPillarLayout(1, 4f, 0.9f, 3f));
            var dir = pillars.Center(0).normalized;
            world.Shots.Add(new KnockbackShot("a", 10, Vector2.zero, dir));

            Run(10, 60);

            Assert.AreEqual(0, state.Cracks.Count);
        }

        [Test]
        public void 금_기능이_꺼진_맵은_아무것도_하지_않는다()
        {
            Build(0);
            Fire("a", 10);
            Fire("a", 20);

            Run(10, 80);

            Assert.AreEqual(0, state.Cracks.Count);
            Assert.AreEqual(KnockbackFloor.NoCollapse, world.Floor.CollapseTickOf(EndTile()));
        }

        [Test]
        public void 이미_무너질_칸이나_원판_밖에_닿은_탄은_무시한다()
        {
            Build(1);
            world.Floor.SetCollapseTick(EndTile(), 5000);
            Fire("a", 10);
            world.Shots.Add(new KnockbackShot("a", 11, new Vector2(-5f, 0f), Vector2.right));   // 끝이 (13, 0) = 원판 밖

            Run(10, 60);

            Assert.AreEqual(0, state.Cracks.Count);
            Assert.AreEqual(5000L, world.Floor.CollapseTickOf(EndTile()));
        }

        [Test]
        public void 금이_상태_메시지에_실린다()
        {
            Build(1);
            Fire("a", 10);
            Fire("a", 20);
            Run(10, 80);

            var wire = KnockbackStateBroadcastSystem.ToWire(state, registry);

            Assert.AreEqual(1, wire.Cracks.Count);
            Assert.AreEqual(EndTile(), wire.Cracks[0].Tile);
            Assert.AreEqual(2, wire.Cracks[0].Cracks);
            Assert.AreEqual(world.Floor.CollapseTickOf(EndTile()), wire.Cracks[0].CollapseTick);
        }

        [Test]
        public void 금만_간_칸은_무너지는_틱이_빠진_채_실린다()
        {
            Build(1);
            Fire("a", 10);
            Run(10, 60);

            var wire = KnockbackStateBroadcastSystem.ToWire(state, registry);

            Assert.AreEqual(1, wire.Cracks[0].Cracks);
            Assert.AreEqual(-1L, wire.Cracks[0].CollapseTick);
        }

        [Test]
        public void 상자는_무너질_칸에_놓이지_않는다()
        {
            //  대조: 아무 칸도 안 무너질 때는 상자가 놓인다.
            Build(1);
            var control = new KnockbackCrateSystem(state, config, world, registry, new System.Random(1), pillars);
            control.Tick(0, Dt);
            control.Tick(config.CrateFirstTicks, Dt);
            Assert.IsTrue(state.Crate.HasValue, "대조: 상자가 놓인다");

            Build(1);
            for (int i = 0; i < world.Floor.Tiles.Count; i++)
            {
                world.Floor.SetCollapseTick(i, 100000);
            }
            var crates = new KnockbackCrateSystem(state, config, world, registry, new System.Random(1), pillars);
            crates.Tick(0, Dt);
            crates.Tick(config.CrateFirstTicks, Dt);

            Assert.IsFalse(state.Crate.HasValue, "모든 칸이 무너질 예정이면 놓을 자리가 없다");
        }
    }
}
