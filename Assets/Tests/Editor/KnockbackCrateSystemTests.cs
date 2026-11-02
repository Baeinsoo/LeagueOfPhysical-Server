using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class KnockbackCrateSystemTests
    {
        const float Dt = 0.02f;
        const long Start = 100;
        //  첫 상자 20초=1000틱, 간격 20초=1000틱, 예고 1.5초=75틱, 줍기 반경 0.9.
        const long FirstAnnounce = Start + 1000;
        const long Land = FirstAnnounce + 75;

        static readonly KnockbackConfig C = new KnockbackConfig(120f, 6f, 10f, 12f, 2f, 0.5f, 5f, 11f, 30f, 18f, 0.3f, 0.55f, 3, 0.95f, 0.26f, 1.2f, 12, 6f, 0.2f, 0.5f, 0.3f, 0.1f, 0.2f, 0.8f, 8f,
            new List<MountedArcheryZoneStageConfig>());

        GameFramework.World.EntityRegistry registry;
        KnockbackMatchState state;
        KnockbackWorld world;
        KnockbackCrateSystem system;

        [SetUp]
        public void SetUp()
        {
            registry = new GameFramework.World.EntityRegistry();
            state = new KnockbackMatchState();
            world = new KnockbackWorld(registry, new GameFramework.World.WorldEventBuffer(), null, null,
                C.CreateFloor(), null, null, C) { GameplayStartTick = Start };
            system = new KnockbackCrateSystem(state, C, world, registry, new System.Random(7));
        }

        GameFramework.World.Entity AddBody(string id, float x, float z, float y = 0f)
        {
            var e = new GameFramework.World.Entity(id);
            e.Add(new GameFramework.World.Transform { Position = new System.Numerics.Vector3(x, y, z) });
            e.Add(new GameFramework.World.Velocity());
            e.Add(new KnockbackBody());
            e.Add(new KnockbackGun(3));
            registry.Add(e);
            state.Players.Add(id);
            return e;
        }

        bool HasBomb(string id) => registry.Get(id).Get<KnockbackGun>().HasBomb;

        void Announce()
        {
            system.Tick(FirstAnnounce, Dt);
            Assert.IsTrue(state.Crate.HasValue);
        }

        static void MoveTo(GameFramework.World.Entity e, Vector2 p, float y = 0f)
            => e.Get<GameFramework.World.Transform>().Position = new System.Numerics.Vector3(p.x, y, p.y);

        [Test]
        public void 첫_상자는_출발_20초_뒤에_예고되고_1_5초_뒤_떨어진다()
        {
            system.Tick(FirstAnnounce - 1, Dt);
            Assert.IsFalse(state.Crate.HasValue);

            int version = state.Version;
            system.Tick(FirstAnnounce, Dt);
            Assert.IsTrue(state.Crate.HasValue);
            Assert.AreEqual(1, state.Crate.Value.Id);
            Assert.AreEqual(Land, state.Crate.Value.LandTick);
            Assert.Greater(state.Version, version, "상자가 생기면 방송 판본이 오른다");
        }

        [Test]
        public void 출발_전에는_상자가_없다()
        {
            world.GameplayStartTick = long.MaxValue;
            system.Tick(FirstAnnounce, Dt);
            Assert.IsFalse(state.Crate.HasValue);
        }

        [Test]
        public void 줍으면_다음_상자는_간격_뒤에_나온다()
        {
            var a = AddBody("a", 0f, 0f);
            Announce();
            MoveTo(a, state.Crate.Value.Position);
            system.Tick(Land, Dt);
            Assert.IsFalse(state.Crate.HasValue);

            system.Tick(FirstAnnounce + 999, Dt);
            Assert.IsFalse(state.Crate.HasValue);
            system.Tick(FirstAnnounce + 1000, Dt);
            Assert.AreEqual(2, state.Crate.Value.Id);
            Assert.AreEqual(FirstAnnounce + 1000 + 75, state.Crate.Value.LandTick);
        }

        [Test]
        public void 안_주운_상자가_있으면_다음_상자는_건너뛴다()
        {
            AddBody("a", 10f, 10f);
            Announce();
            var first = state.Crate.Value;

            system.Tick(FirstAnnounce + 1000, Dt);
            Assert.AreEqual(first.Id, state.Crate.Value.Id);
            Assert.AreEqual(first.Position, state.Crate.Value.Position);

            //  건너뛴 뒤에도 일정은 이어진다 - 그 상자를 주우면 다음 간격에 새 상자가 나온다.
            MoveTo(registry.Get("a"), first.Position);
            system.Tick(FirstAnnounce + 1001, Dt);
            Assert.IsFalse(state.Crate.HasValue);
            system.Tick(FirstAnnounce + 2000, Dt);
            Assert.AreEqual(2, state.Crate.Value.Id);
        }

        [Test]
        public void 줍기_반경_경계_안쪽만_줍는다()
        {
            var a = AddBody("a", 0f, 0f);
            Announce();
            var at = state.Crate.Value.Position;

            MoveTo(a, at + new Vector2(0.91f, 0f));
            system.Tick(Land, Dt);
            Assert.IsFalse(HasBomb("a"));
            Assert.IsTrue(state.Crate.HasValue);

            MoveTo(a, at + new Vector2(0.899f, 0f));
            system.Tick(Land + 1, Dt);
            Assert.IsTrue(HasBomb("a"));
            Assert.IsFalse(state.Crate.HasValue);
        }

        [Test]
        public void 떨어지기_전에는_못_줍는다()
        {
            var a = AddBody("a", 0f, 0f);
            Announce();
            MoveTo(a, state.Crate.Value.Position);
            system.Tick(Land - 1, Dt);
            Assert.IsFalse(HasBomb("a"));
            system.Tick(Land, Dt);
            Assert.IsTrue(HasBomb("a"), "양성 대조 - 떨어진 틱엔 줍는다");
        }

        [Test]
        public void 같은_틱에는_가장_가까운_몸이_줍는다()
        {
            var a = AddBody("a", 0f, 0f);
            var b = AddBody("b", 0f, 0f);
            Announce();
            var at = state.Crate.Value.Position;
            MoveTo(a, at + new Vector2(0.6f, 0f));
            MoveTo(b, at + new Vector2(0.3f, 0f));
            system.Tick(Land, Dt);
            Assert.IsTrue(HasBomb("b"));
            Assert.IsFalse(HasBomb("a"));
        }

        [Test]
        public void 거리가_같으면_id가_앞선_몸이_줍는다()
        {
            //  Players에는 z가 먼저 들어가 있어도 id 순으로 가린다.
            var z = AddBody("z", 0f, 0f);
            var a = AddBody("a", 0f, 0f);
            Announce();
            var at = state.Crate.Value.Position;
            MoveTo(z, at + new Vector2(0.5f, 0f));
            MoveTo(a, at + new Vector2(-0.5f, 0f));
            system.Tick(Land, Dt);
            Assert.IsTrue(HasBomb("a"));
            Assert.IsFalse(HasBomb("z"));
        }

        [Test]
        public void 날아가는_중_떨어지는_중_탈락한_몸은_못_줍는다()
        {
            var flying = AddBody("flying", 0f, 0f);
            var falling = AddBody("falling", 0f, 0f);
            var gone = AddBody("gone", 0f, 0f);
            Announce();
            var at = state.Crate.Value.Position;
            MoveTo(flying, at);
            flying.Get<KnockbackBody>().KnockbackEndTick = Land + 10;
            MoveTo(falling, at, -0.2f);
            MoveTo(gone, at);
            state.Eliminate("gone", Land - 1);

            system.Tick(Land, Dt);
            Assert.IsFalse(HasBomb("flying"));
            Assert.IsFalse(HasBomb("falling"));
            Assert.IsFalse(HasBomb("gone"));
            Assert.IsTrue(state.Crate.HasValue);

            //  양성 대조 - 날림이 끝나면 줍는다.
            system.Tick(Land + 10, Dt);
            Assert.IsTrue(HasBomb("flying"));
        }

        [Test]
        public void 이미_폭탄을_든_몸은_줍지_않는다()
        {
            var a = AddBody("a", 0f, 0f);
            var b = AddBody("b", 0f, 0f);
            a.Get<KnockbackGun>().HasBomb = true;
            Announce();
            MoveTo(a, state.Crate.Value.Position);
            MoveTo(b, state.Crate.Value.Position + new Vector2(0.5f, 0f));
            system.Tick(Land, Dt);
            Assert.IsTrue(HasBomb("b"));
        }

        [Test]
        public void 같은_씨앗이면_같은_자리_그리고_원_안쪽이다()
        {
            var positions = new List<Vector2>();
            for (int run = 0; run < 2; run++)
            {
                var s = new KnockbackMatchState();
                var sys = new KnockbackCrateSystem(s, C, world, registry, new System.Random(42));
                for (int i = 0; i < 6; i++)
                {
                    sys.Tick(FirstAnnounce + i * 1000, Dt);
                    positions.Add(s.Crate.Value.Position);
                    s.ClearCrate();
                }
            }
            for (int i = 0; i < 6; i++)
            {
                Assert.AreEqual(positions[i], positions[i + 6]);
                Assert.LessOrEqual(positions[i].magnitude, world.Floor.ZoneStartRadius - 1.5f + 1e-4f);
                Assert.IsTrue(world.Floor.IsSolid(positions[i].x, positions[i].y, 0), "바닥이 있는 칸이다");
            }
            Assert.AreNotEqual(positions[0], positions[1], "매번 같은 자리가 아니다");
        }

        [Test]
        public void 다음_원이_공개돼_있으면_그_안쪽에_떨어진다()
        {
            var center = new Vector2(5f, -3f);
            for (int i = 0; i < 8; i++)
            {
                var s = new KnockbackMatchState();
                s.AddZoneStage(new ZoneStage(1, 0, 10, 20, Vector2.zero, world.Floor.ZoneStartRadius, center, 4f, 0f));
                new KnockbackCrateSystem(s, C, world, registry, new System.Random(i)).Tick(FirstAnnounce, Dt);
                Assert.LessOrEqual((s.Crate.Value.Position - center).magnitude, 4f - 1.5f + 1e-4f);
            }
        }

        [Test]
        public void 원이_작아_여유가_없으면_원_중심이다()
        {
            var center = new Vector2(2f, 1f);
            state.AddZoneStage(new ZoneStage(1, 0, 10, 20, Vector2.zero, world.Floor.ZoneStartRadius, center, 1.4f, 0f));
            system.Tick(FirstAnnounce, Dt);
            Assert.AreEqual(center, state.Crate.Value.Position);
        }

        //  상자 칸만 무너지고 반대쪽 작은 원은 남는 단계를 floor와 state에 함께 넣는다.
        ZoneStage CollapseAwayFrom(Vector2 crate)
        {
            var center = -crate.normalized * 8f;
            var stage = new ZoneStage(1, FirstAnnounce, FirstAnnounce + 1, FirstAnnounce + 2, Vector2.zero, world.Floor.ZoneStartRadius, center, 4f, 0f);
            world.Floor.SetZoneStages(new List<ZoneStage> { stage });
            state.AddZoneStage(stage);
            return stage;
        }

        [Test]
        public void 상자_칸이_무너지면_사라지고_다음_간격에_새_상자가_예고된다()
        {
            Announce();
            var crate = state.Crate.Value;
            Assert.Greater(crate.Position.magnitude, 3f, "시험 전제 - 상자가 원점 근처가 아니다");
            CollapseAwayFrom(crate.Position);

            system.Tick(Land - 1, Dt);
            Assert.IsTrue(state.Crate.HasValue, "떨어지기 전에는 치우지 않는다");

            int version = state.Version;
            system.Tick(Land, Dt);
            Assert.IsFalse(state.Crate.HasValue);
            Assert.Greater(state.Version, version, "치우면 방송 판본이 오른다");

            system.Tick(FirstAnnounce + 1000, Dt);
            Assert.IsTrue(state.Crate.HasValue, "다음 간격에 새 상자");
            Assert.AreEqual(2, state.Crate.Value.Id);
        }

        [Test]
        public void 서_있는_칸의_상자는_그대로_남는다()
        {
            Announce();
            system.Tick(Land + 5, Dt);
            Assert.IsTrue(state.Crate.HasValue, "양성 대조 - 칸이 멀쩡하면 치우지 않는다");
        }

        [Test]
        public void 폴백_자리가_구멍이면_예고하지_않고_다음_간격에_다시_시도한다()
        {
            //  원 중심이 판 밖이고 여유가 없어 폴백(원 중심)으로 가는 경우.
            state.AddZoneStage(new ZoneStage(1, 0, 10, 20, Vector2.zero, world.Floor.ZoneStartRadius, new Vector2(60f, 0f), 1.4f, 0f));
            int version = state.Version;
            system.Tick(FirstAnnounce, Dt);
            Assert.IsFalse(state.Crate.HasValue);
            Assert.AreEqual(version, state.Version);

            state.AddZoneStage(new ZoneStage(2, 0, 10, 20, Vector2.zero, world.Floor.ZoneStartRadius, Vector2.zero, 6f, 0f));
            system.Tick(FirstAnnounce + 1000, Dt);
            Assert.IsTrue(state.Crate.HasValue, "다음 간격에는 다시 시도한다");
        }
    }
}
