using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>충격파 폭탄 탄은 몸·기둥·사거리 끝 어디서든 한 번만 터지고, 반경 안 상대를 함께 민다.</summary>
    public class KnockbackBombHitSystemTests
    {
        const float Dt = 0.02f;

        //  탄 속도 30, 사거리 18, 몸 반지름 0.55, 탄 반지름 0.3, 폭발 반경 2.5(기본)
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
            system = new KnockbackHitSystem(world, registry, state, C, sink, new KnockbackPillars(KnockbackPillarLayout.None, C.CreateFloor()), Dt);
            AddBody("a", 0f, 0f);
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

        void FireBomb(bool isBomb = true, Vector2? direction = null)
            => world.Shots.Add(new KnockbackShot("a", 10, Vector2.zero, direction ?? Vector2.right, isBomb));

        int Damage(string id) => registry.Get(id).Get<KnockbackBody>().DamagePercent;
        float VelocityX(string id) => registry.Get(id).Get<GameFramework.World.Velocity>().Linear.X;
        float VelocityZ(string id) => registry.Get(id).Get<GameFramework.World.Velocity>().Linear.Z;

        int Count<T>() where T : GameFramework.World.WorldEvent
        {
            int n = 0;
            foreach (var e in sink.Got) if (e is T) n++;
            return n;
        }

        [Test]
        public void 몸에_맞으면_그_자리에서_터져_맞은_몸과_반경_안_상대가_함께_날아간다()
        {
            AddBody("b", 3f, 0f);
            AddBody("c", 3f, 2f);
            AddBody("far", 3f, 5f);
            FireBomb();

            system.Tick(14, Dt);

            Assert.AreEqual(12, Damage("b"), "직접 맞은 몸은 한 번만 민다");
            Assert.AreEqual(12, Damage("c"));
            Assert.AreEqual(0, Damage("far"), "반경 밖은 안 민다");
            Assert.AreEqual(0, Damage("a"), "쏜 사람은 안 민다");
            Assert.Greater(VelocityZ("c"), 1f, "터진 자리에서 몸 쪽으로 민다");
            Assert.Greater(VelocityX("b"), 1f);
            Assert.AreEqual(0, world.Shots.Shots.Count);
            Assert.AreEqual(1, Count<KnockbackBlastEvent>());
            var blast = (KnockbackBlastEvent)sink.Got.Find(e => e is KnockbackBlastEvent);
            Assert.AreEqual("a", blast.shooterId);
            Assert.AreEqual(14, blast.tick);
            Assert.AreEqual(3f - (C.HitRadius + C.ShotRadius), blast.position.x, 0.3f, "몸 원에 닿은 자리");

            system.Tick(15, Dt);
            Assert.AreEqual(12, Damage("b"), "탄이 사라졌으니 또 터지지 않는다");
            Assert.AreEqual(1, Count<KnockbackBlastEvent>());
        }

        [Test]
        public void 같은_팀은_안_밀리고_상대만_밀린다()
        {
            AddBody("b", 3f, 0f);
            AddBody("mate", 3f, 2f);
            AddBody("foe", 3f, -2f);
            state.Team["a"] = 1;
            state.Team["mate"] = 1;
            state.Team["b"] = 2;
            state.Team["foe"] = 2;
            FireBomb();

            system.Tick(14, Dt);

            Assert.AreEqual(0, Damage("mate"));
            Assert.AreEqual(12, Damage("b"));
            Assert.AreEqual(12, Damage("foe"));
        }

        [Test]
        public void 터진_상대에게_쏜_사람의_막타_크레딧이_남는다()
        {
            AddBody("b", 3f, 0f);
            AddBody("c", 3f, 2f);
            FireBomb();
            system.Tick(14, Dt);

            state.Eliminate("b", 14 + 100);
            state.Eliminate("c", 14 + 100);
            Assert.AreEqual("a", state.KoBy["b"]);
            Assert.AreEqual("a", state.KoBy["c"]);
            Assert.IsFalse(state.KoChain.Contains("c"));
        }

        [Test]
        public void 보통_탄은_반경_안_다른_몸을_밀지_않는다()
        {
            AddBody("b", 3f, 0f);
            AddBody("c", 3f, 2f);
            FireBomb(isBomb: false);
            system.Tick(14, Dt);

            Assert.AreEqual(12, Damage("b"));
            Assert.AreEqual(0, Damage("c"));
            Assert.AreEqual(0, Count<KnockbackBlastEvent>());
        }

        [Test]
        public void 기둥에_막힌_폭탄은_그_자리에서_한_번_터진다()
        {
            var floor = C.CreateFloor();
            var pillars = new KnockbackPillars(new KnockbackPillarLayout(1, 4f, 0.9f, 3f), floor);
            var dir = pillars.Center(0).normalized;
            var blocked = new KnockbackHitSystem(world, registry, state, C, sink, pillars, Dt);
            //  기둥 앞 1 m 옆에 선 상대 - 터지면 함께 날아간다.
            var near = pillars.Center(0) - dir * 1.5f + new Vector2(-dir.y, dir.x) * 1.2f;
            AddBody("c", near.x, near.y);
            world.Shots.Add(new KnockbackShot("a", 10, Vector2.zero, dir, true));

            long blastTick = -1;
            for (long t = 10; t < 40; t++)
            {
                blocked.Tick(t, Dt);
                if (blastTick < 0 && Count<KnockbackBlastEvent>() > 0) blastTick = t;
            }

            Assert.AreEqual(1, Count<KnockbackBlastEvent>(), "한 번만 터진다");
            Assert.Greater(blastTick, 10);
            Assert.Less(blastTick, 20, "기둥에 닿는 틱에 터진다");
            Assert.AreEqual(0, world.Shots.Shots.Count);
            Assert.AreEqual(12, Damage("c"));
            var blast = (KnockbackBlastEvent)sink.Got.Find(e => e is KnockbackBlastEvent);
            float gap = (blast.position - pillars.Center(0)).magnitude;
            Assert.AreEqual(pillars.Radius + C.ShotRadius, gap, 0.05f, "기둥 가장자리에서 터진다");
        }

        [Test]
        public void 보통_탄은_기둥에_막혀도_터지지_않는다()
        {
            var floor = C.CreateFloor();
            var pillars = new KnockbackPillars(new KnockbackPillarLayout(1, 4f, 0.9f, 3f), floor);
            var dir = pillars.Center(0).normalized;
            var blocked = new KnockbackHitSystem(world, registry, state, C, sink, pillars, Dt);
            world.Shots.Add(new KnockbackShot("a", 10, Vector2.zero, dir, false));
            for (long t = 10; t < 40; t++) blocked.Tick(t, Dt);
            Assert.AreEqual(0, Count<KnockbackBlastEvent>());
        }

        [Test]
        public void 사거리_끝에_닿은_폭탄은_그_자리에서_한_번_터진다()
        {
            AddBody("c", 17.5f, 1.5f);
            FireBomb();

            long blastTick = -1;
            for (long t = 10; t < 70; t++)
            {
                if (world.Shots.Shots.Count > 0) { /* 월드 틱의 정리 */ world.Shots.RemoveSpent(t, C.ShotSpeed, C.ShotRange, Dt); }
                system.Tick(t, Dt);
                if (blastTick < 0 && Count<KnockbackBlastEvent>() > 0) blastTick = t;
            }

            Assert.AreEqual(1, Count<KnockbackBlastEvent>());
            //  사거리 18 / 틱당 0.6 = 30틱 뒤(float 오차로 한 틱 더)
            Assert.That(blastTick, Is.InRange(40L, 41L), "float 오차로 한 틱 늦을 수 있다");
            var blast = (KnockbackBlastEvent)sink.Got.Find(e => e is KnockbackBlastEvent);
            Assert.AreEqual(18f, blast.position.x, 1e-3f);
            Assert.AreEqual(12, Damage("c"));
        }

        [Test]
        public void 사거리_끝_바로_전_틱에는_안_터진다()
        {
            FireBomb();
            system.Tick(39, Dt);
            Assert.AreEqual(0, Count<KnockbackBlastEvent>());
            Assert.AreEqual(1, world.Shots.Shots.Count);
        }

        [Test]
        public void 쏘자마자_코앞의_상대도_터져_밀린다()
        {
            //  쏘는 틱에 쏜 사람 코앞(0.4 m)의 상대 - 터진 자리(쏜 자리)에서 탄 방향 쪽에 있어 그쪽으로 밀린다.
            AddBody("b", 0.4f, 0f);
            FireBomb();
            system.Tick(10, Dt);
            Assert.AreEqual(12, Damage("b"));
            Assert.Greater(VelocityX("b"), 1f);
        }

        //  실제 틱 순서: 월드 정리(막힌 탄·다 쓴 탄) 다음에 End 판정.
        void RealTick(KnockbackHitSystem sys, KnockbackPillars pillars, long t)
        {
            world.Shots.RemoveBlocked(t - 1, pillars.CollectStanding, pillars.Radius, C, Dt);
            world.Shots.RemoveSpent(t, C.ShotSpeed, C.ShotRange, Dt);
            sys.Tick(t, Dt);
        }

        [Test]
        public void 몸과_사거리_끝이_같은_틱이어도_월드_정리를_지나_한_번만_터진다()
        {
            //  사거리 끝(18 m) 바로 앞에 선 몸 - 몸 맞음과 사거리 끝이 겹친다.
            AddBody("c", 18.6f, 0f);
            var pillars = new KnockbackPillars(KnockbackPillarLayout.None, C.CreateFloor());
            FireBomb();
            for (long t = 10; t < 70; t++) RealTick(system, pillars, t);

            Assert.AreEqual(1, Count<KnockbackBlastEvent>());
            Assert.AreEqual(12, Damage("c"), "한 번만 민다");
            Assert.AreEqual(0, world.Shots.Shots.Count);
        }

        [Test]
        public void 몸과_기둥이_같은_틱이어도_월드_정리를_지나_한_번만_터진다()
        {
            var pillars = new KnockbackPillars(new KnockbackPillarLayout(1, 4f, 0.9f, 3f), C.CreateFloor());
            var dir = pillars.Center(0).normalized;
            var sys = new KnockbackHitSystem(world, registry, state, C, sink, pillars, Dt);
            //  기둥 바로 앞에 붙어 선 몸 - 같은 선분이 몸과 기둥 둘 다에 닿는다.
            var at = pillars.Center(0) - dir * (pillars.Radius + C.HitRadius + C.ShotRadius - 0.1f);
            AddBody("c", at.x, at.y);
            world.Shots.Add(new KnockbackShot("a", 10, Vector2.zero, dir, true));
            for (long t = 10; t < 40; t++) RealTick(sys, pillars, t);

            Assert.AreEqual(1, Count<KnockbackBlastEvent>());
            Assert.AreEqual(12, Damage("c"));
            Assert.AreEqual(0, world.Shots.Shots.Count);
        }
    }
}
