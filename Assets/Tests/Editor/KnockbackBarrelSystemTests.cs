using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>드럼통 굴림과 탄 맞음, 몸 치기. 마찰 2.5, 반발 0.6, 탄 충격 6, 몸 칠 때 배율 1.</summary>
    public class KnockbackBarrelSystemTests
    {
        const float Dt = 0.02f;

        static KnockbackConfig MakeConfig(int barrels, KnockbackPillarLayout pillars = null)
            => new KnockbackConfig(120f, 6f, 10f, 12f, 2f, 0.5f, 5f, 11f, 30f, 18f, 0.3f, 0.55f, 3, 0.95f, 0.26f, 1.2f, 12, 6f, 0.2f, 0.5f, 0.3f, 0.1f, 0.2f, 0.8f, 8f,
                                   new List<MountedArcheryZoneStageConfig>(), pillars,
                                   barrels: new KnockbackBarrelLayout(barrels, 0.55f, 9f, 2.5f, 0.6f, 6f, 1f));

        sealed class FakeSink : GameFramework.World.IEventSink
        {
            public readonly List<GameFramework.World.WorldEvent> Got = new List<GameFramework.World.WorldEvent>();
            public void Emit(IReadOnlyList<GameFramework.World.WorldEvent> events) => Got.AddRange(events);
        }

        GameFramework.World.EntityRegistry registry;
        KnockbackMatchState state;
        KnockbackWorld world;
        KnockbackConfig config;
        KnockbackPillars pillars;
        KnockbackBarrelSystem barrelSystem;
        KnockbackHitSystem hitSystem;
        FakeSink sink;
        List<string> despawned;

        void Build(int barrels = 2, KnockbackPillarLayout pillarLayout = null)
        {
            config = MakeConfig(barrels, pillarLayout);
            registry = new GameFramework.World.EntityRegistry();
            state = new KnockbackMatchState();
            sink = new FakeSink();
            despawned = new List<string>();
            var floor = config.CreateFloor();
            pillars = new KnockbackPillars(config.Pillars, floor);
            world = new KnockbackWorld(registry, new GameFramework.World.WorldEventBuffer(), null, null, floor, null, null, config) { GameplayStartTick = 0 };
            barrelSystem = new KnockbackBarrelSystem(state, config, world, registry, pillars, id => despawned.Add(id), Dt);
            hitSystem = new KnockbackHitSystem(world, registry, state, config, sink, pillars, Dt);
        }

        GameFramework.World.Entity AddBarrel(string id, float x, float z, float vx = 0f, float vz = 0f)
        {
            var e = new GameFramework.World.Entity(id);
            e.Add(new GameFramework.World.Transform { Position = new System.Numerics.Vector3(x, 0f, z) });
            e.Add(new GameFramework.World.Velocity { Linear = new System.Numerics.Vector3(vx, 0f, vz) });
            e.Add(new EntityKind(EntityType.Barrel));
            e.Add(new KnockbackBarrel());
            registry.Add(e);
            return e;
        }

        GameFramework.World.Entity AddBody(string id, float x, float z, int team = 0)
        {
            var e = new GameFramework.World.Entity(id);
            e.Add(new GameFramework.World.Transform { Position = new System.Numerics.Vector3(x, 0f, z) });
            e.Add(new GameFramework.World.Velocity());
            e.Add(new KnockbackBody());
            registry.Add(e);
            state.Players.Add(id);
            state.Team[id] = team;
            return e;
        }

        static Vector2 Pos(GameFramework.World.Entity e) => new Vector2(e.Get<GameFramework.World.Transform>().Position.X, e.Get<GameFramework.World.Transform>().Position.Z);
        static Vector2 Vel(GameFramework.World.Entity e) => new Vector2(e.Get<GameFramework.World.Velocity>().Linear.X, e.Get<GameFramework.World.Velocity>().Linear.Z);
        static float Y(GameFramework.World.Entity e) => e.Get<GameFramework.World.Transform>().Position.Y;

        void Run(long from, int ticks)
        {
            for (int i = 0; i < ticks; i++) barrelSystem.Tick(from + i, Dt);
        }

        [Test]
        public void 마찰로_줄다가_서고_거기서_더_안_간다()
        {
            Build();
            var b = AddBarrel("b1", 0f, 0f, 2f, 0f);

            Run(0, 20);
            Assert.Less(Vel(b).x, 2f);
            Assert.Greater(Vel(b).x, 0f, "0.4초 뒤엔 아직 구르는 중");

            Run(20, 100);
            Assert.AreEqual(0f, Vel(b).x, 1e-6f);
            float stopped = Pos(b).x;
            Assert.Greater(stopped, 0.5f, "굴러서 갔다");
            Run(120, 10);
            Assert.AreEqual(stopped, Pos(b).x, 1e-6f);
        }

        [Test]
        public void 기둥에_부딪히면_튕겨_나온다()
        {
            Build(2, new KnockbackPillarLayout(1, 5f, 0.9f, 3f));
            var pillar = pillars.Center(0);
            var toward = pillar.normalized;
            var start = pillar - toward * 3f;
            var b = AddBarrel("b1", start.x, start.y, toward.x * 6f, toward.y * 6f);

            float closest = float.MaxValue;
            bool bounced = false;
            for (int i = 0; i < 80; i++)
            {
                barrelSystem.Tick(i, Dt);
                closest = Mathf.Min(closest, (Pos(b) - pillar).magnitude);
                if (Vector2.Dot(Vel(b), toward) < 0f) bounced = true;
            }

            Assert.GreaterOrEqual(closest, pillars.Radius + 0.55f - 1e-3f, "기둥 안으로 들어가지 않는다");
            Assert.IsTrue(bounced, "되돌아 나온다");
        }

        [Test]
        public void 두_드럼통이_부딪히면_서로_튕긴다()
        {
            Build();
            var a = AddBarrel("a", 0f, 0f, 4f, 0f);
            var b = AddBarrel("b", 1.5f, 0f);

            Run(0, 20);

            Assert.Greater(Vel(b).x, 0.5f, "맞은 쪽이 굴러간다");
            Assert.Greater(Pos(b).x, 1.5f);
            Assert.GreaterOrEqual((Pos(b) - Pos(a)).magnitude, 1.1f - 1e-3f, "겹치지 않는다");
        }

        [Test]
        public void 바닥_칸이_없는_곳으로_나가면_떨어져_깊이를_넘으면_치운다()
        {
            Build();
            var b = AddBarrel("b1", 11.5f, 0f, 6f, 0f);

            long tick = 0;
            while (tick < 400 && despawned.Count == 0)
            {
                barrelSystem.Tick(tick++, Dt);
            }

            Assert.AreEqual(1, despawned.Count);
            Assert.AreEqual("b1", despawned[0]);
            Assert.Less(Y(b), -config.FallDepth);

            barrelSystem.Tick(tick, Dt);
            Assert.AreEqual(1, despawned.Count, "한 번만 치운다");
        }

        [Test]
        public void 떨어지는_중에는_옆_속도가_마찰로_안_줄고_몸도_안_친다()
        {
            Build();
            AddBody("p", 20f, 0f);
            var b = AddBarrel("b1", 20f, 0f, 3f, 0f);
            b.Get<GameFramework.World.Transform>().Position = new System.Numerics.Vector3(20f, -1f, 0f);

            Run(0, 5);

            Assert.Less(Y(b), 0f, "바닥이 없어 떨어진다");
            Assert.AreEqual(3f, Vel(b).x, 1e-5f);
            Assert.AreEqual(long.MinValue, registry.Get("p").Get<KnockbackBody>().KnockbackEndTick, "떨어지는 드럼통은 몸을 안 친다");
        }

        [Test]
        public void 다가오는_드럼통이_몸을_치면_날리고_위험도는_그대로이고_크레딧은_민_사람이다()
        {
            Build();
            AddBody("shooter", -8f, 0f);
            var victim = AddBody("v", 2f, 0f);
            var b = AddBarrel("b1", 1.0f, 0f, 6f, 0f);
            b.Get<KnockbackBarrel>().PusherId = "shooter";

            barrelSystem.Tick(0, Dt);

            var body = victim.Get<KnockbackBody>();
            Assert.AreEqual(0, body.DamagePercent, "위험도 그대로");
            Assert.Greater(body.KnockbackEndTick, 0, "날림이 걸렸다");
            Assert.Greater(Vel(victim).x, 4f, "드럼→몸 방향으로 날아간다");
            Assert.AreEqual(0f, Vel(victim).y, 1e-5f);
            state.Eliminate("v", 100);
            Assert.AreEqual("shooter", state.KoBy["v"]);
            Assert.Greater(Vel(b).x, 0f, "드럼통은 무거워서 계속 간다");
            Assert.LessOrEqual(Vel(b).x, Vel(victim).x + 1e-3f, "몸보다 빠르게 몸 쪽으로 가지 않는다");

            long firstEnd = body.KnockbackEndTick;
            barrelSystem.Tick(1, Dt);
            Assert.AreEqual(firstEnd, body.KnockbackEndTick, "같은 몸을 다음 틱에 또 치지 않는다");
        }

        [Test]
        public void 같은_팀이_민_드럼통은_크레딧을_안_남기고_안_다가오면_안_친다()
        {
            Build();
            AddBody("mate", -8f, 0f, team: 1);
            var victim = AddBody("v", 2f, 0f, team: 1);
            var b = AddBarrel("b1", 1.0f, 0f, 6f, 0f);
            b.Get<KnockbackBarrel>().PusherId = "mate";
            barrelSystem.Tick(0, Dt);
            Assert.Greater(victim.Get<KnockbackBody>().KnockbackEndTick, 0, "날리기는 한다");
            state.Eliminate("v", 100);
            Assert.IsFalse(state.KoBy.ContainsKey("v"), "같은 팀이라 크레딧 없음");

            var still = AddBody("still", 0f, 5f);
            AddBarrel("b2", 0f, 4.3f, 0f, 0f);
            barrelSystem.Tick(1, Dt);
            Assert.AreEqual(long.MinValue, still.Get<KnockbackBody>().KnockbackEndTick, "서 있는 드럼통은 안 친다");
        }

        [Test]
        public void 날아가는_중인_몸도_맞고_탈락한_몸은_안_맞는다()
        {
            Build();
            var flying = AddBody("fly", 2f, 0f);
            flying.Get<KnockbackBody>().KnockbackEndTick = 500;
            var gone = AddBody("gone", 2f, 1f);
            state.EliminatedTick["gone"] = 5;
            AddBarrel("b1", 1.0f, 0.4f, 6f, 0f);

            barrelSystem.Tick(0, Dt);

            Assert.AreNotEqual(500, flying.Get<KnockbackBody>().KnockbackEndTick, "날아가는 중에도 다시 날림이 갱신된다");
            Assert.AreEqual(long.MinValue, gone.Get<KnockbackBody>().KnockbackEndTick);
        }

        [Test]
        public void 탄이_드럼통을_맞히면_탄이_사라지고_드럼통이_탄_방향으로_밀린다()
        {
            Build();
            AddBody("a", 0f, 0f);
            var b = AddBarrel("b1", 3f, 0f);
            world.Shots.Add(new KnockbackShot("a", 10, Vector2.zero, Vector2.right));

            hitSystem.Tick(14, Dt);

            Assert.AreEqual(0, world.Shots.Shots.Count);
            Assert.AreEqual(6f, Vel(b).x, 1e-4f);
            Assert.AreEqual("a", b.Get<KnockbackBarrel>().PusherId);
            Assert.IsTrue(sink.Got.Exists(e => e is KnockbackHitEvent h && h.victimId == "b1"));
        }

        [Test]
        public void 몸보다_드럼통이_앞이면_드럼통이_맞고_몸이_앞이면_몸이_맞는다()
        {
            //  한 틱 선분(1.8m → 2.4m)에 둘 다 들어오는 거리로 놓는다: 드럼통 3.0은 2.15에서, 몸 3.1은 2.25에서 닿는다.
            Build();
            AddBody("a", 0f, 0f);
            var behind = AddBody("v", 3.1f, 0f);
            var near = AddBarrel("near", 3.0f, 0f);
            world.Shots.Add(new KnockbackShot("a", 10, Vector2.zero, Vector2.right));
            hitSystem.Tick(14, Dt);
            Assert.AreEqual(6f, Vel(near).x, 1e-4f, "앞의 드럼통이 막는다");
            Assert.AreEqual(0, behind.Get<KnockbackBody>().DamagePercent, "뒤의 몸은 안 맞는다");

            Build();
            AddBody("a", 0f, 0f);
            var front = AddBody("v", 2.9f, 0f);
            var far = AddBarrel("far", 3.0f, 0f);
            world.Shots.Add(new KnockbackShot("a", 10, Vector2.zero, Vector2.right));
            hitSystem.Tick(14, Dt);
            Assert.AreEqual(12, front.Get<KnockbackBody>().DamagePercent, "앞의 몸이 맞는다");
            Assert.AreEqual(0f, Vel(far).x, 1e-6f, "뒤의 드럼통은 안 밀린다");
        }

        [Test]
        public void 기둥이_드럼통보다_앞이면_드럼통은_안_맞는다()
        {
            Build(1, new KnockbackPillarLayout(1, 3f, 0.9f, 3f));
            var dir = pillars.Center(0).normalized;
            AddBody("a", 0f, 0f);
            var blocked = AddBarrel("b1", dir.x * 4.6f, dir.y * 4.6f);
            world.Shots.Add(new KnockbackShot("a", 10, Vector2.zero, dir));
            hitSystem.Tick(17, Dt);
            Assert.AreEqual(0f, Vel(blocked).magnitude, 1e-6f, "기둥에 막혔다");

            //  양성 대조: 기둥이 없으면 같은 탄이 같은 자리의 드럼통을 민다.
            Build(1);
            AddBody("a", 0f, 0f);
            var open = AddBarrel("b1", dir.x * 4.6f, dir.y * 4.6f);
            world.Shots.Add(new KnockbackShot("a", 10, Vector2.zero, dir));
            hitSystem.Tick(17, Dt);
            Assert.Greater(Vel(open).magnitude, 5f);
        }

        [Test]
        public void 폭탄이_드럼통에_맞으면_거기서_터져_반경_안_드럼통을_바깥으로_민다()
        {
            Build();
            AddBody("a", 0f, 0f);
            var hit = AddBarrel("hit", 3f, 0f);
            var side = AddBarrel("side", 3f, 1.8f);
            var far = AddBarrel("far", 3f, 6f);
            world.Shots.Add(new KnockbackShot("a", 10, Vector2.zero, Vector2.right, isBomb: true));

            hitSystem.Tick(14, Dt);

            Assert.AreEqual(0, world.Shots.Shots.Count);
            Assert.IsTrue(sink.Got.Exists(e => e is KnockbackBlastEvent));
            Assert.Greater(Vel(side).y, 5f, "터진 자리에서 바깥(위)으로");
            Assert.Greater(Vel(hit).magnitude, 5f, "맞은 드럼통도 민다");
            Assert.AreEqual(0f, Vel(far).magnitude, 1e-6f, "반경 밖은 안 민다");
            Assert.AreEqual("a", side.Get<KnockbackBarrel>().PusherId);
        }

        [Test]
        public void 서_있는_드럼통에_걸어_들어가도_아무_일도_없다()
        {
            Build();
            var body = AddBody("v", 1.7f, 0f);
            body.Get<GameFramework.World.Velocity>().Linear = new System.Numerics.Vector3(-6f, 0f, 0f);
            AddBarrel("b1", 1.0f, 0f);

            barrelSystem.Tick(0, Dt);

            Assert.AreEqual(long.MinValue, body.Get<KnockbackBody>().KnockbackEndTick, "걸어서는 못 친다 - 몸은 지나간다");
            Assert.AreEqual(-6f, Vel(body).x, 1e-6f, "속도도 그대로");
        }

        [Test]
        public void 몸_쪽으로_문턱_미만으로_가는_드럼통은_안_치고_문턱_이상이면_친다()
        {
            Build();
            var slowVictim = AddBody("slow", 1.8f, 0f);
            AddBarrel("s", 1.0f, 0f, 0.3f, 0f);
            barrelSystem.Tick(0, Dt);
            Assert.AreEqual(long.MinValue, slowVictim.Get<KnockbackBody>().KnockbackEndTick, "느린 드럼통은 안 친다");

            Build();
            var fastVictim = AddBody("fast", 1.8f, 0f);
            AddBarrel("f", 1.0f, 0f, 0.8f, 0f);
            barrelSystem.Tick(0, Dt);
            Assert.Greater(fastVictim.Get<KnockbackBody>().KnockbackEndTick, 0, "양성 대조: 문턱 이상이면 친다");
        }

        [Test]
        public void 막혀서_낀_몸은_같은_드럼통에_매틱_다시_맞지_않는다()
        {
            Build();
            var body = AddBody("v", 2.0f, 0f);
            AddBarrel("b1", 1.0f, 0f, 6f, 0f);

            barrelSystem.Tick(0, Dt);
            long firstEnd = body.Get<KnockbackBody>().KnockbackEndTick;
            Assert.Greater(firstEnd, 0, "처음엔 맞는다");

            for (long t = 1; t <= 20; t++)
            {
                //  기둥·벽에 막혀 몸이 못 움직이는 걸 흉내 낸다.
                body.Get<GameFramework.World.Velocity>().Linear = System.Numerics.Vector3.Zero;
                body.Get<GameFramework.World.Transform>().Position = new System.Numerics.Vector3(2.0f, 0f, 0f);
                registry.Get("b1").Get<GameFramework.World.Velocity>().Linear = new System.Numerics.Vector3(6f, 0f, 0f);
                barrelSystem.Tick(t, Dt);
            }

            Assert.AreEqual(firstEnd, body.Get<KnockbackBody>().KnockbackEndTick, "겹침이 풀리기 전엔 다시 치지 않는다");
        }

        [Test]
        public void 겹침이_풀리면_다시_칠_수_있다()
        {
            Build();
            var body = AddBody("v", 2.0f, 0f);
            var barrel = AddBarrel("b1", 1.0f, 0f, 6f, 0f);
            barrelSystem.Tick(0, Dt);
            long firstEnd = body.Get<KnockbackBody>().KnockbackEndTick;

            //  드럼통이 멀어졌다가(겹침 풀림) 다시 몸 쪽으로 온다.
            barrel.Get<GameFramework.World.Transform>().Position = new System.Numerics.Vector3(-5f, 0f, 0f);
            barrel.Get<GameFramework.World.Velocity>().Linear = System.Numerics.Vector3.Zero;
            body.Get<GameFramework.World.Transform>().Position = new System.Numerics.Vector3(2.0f, 0f, 0f);
            barrelSystem.Tick(1, Dt);
            barrel.Get<GameFramework.World.Transform>().Position = new System.Numerics.Vector3(1.0f, 0f, 0f);
            barrel.Get<GameFramework.World.Velocity>().Linear = new System.Numerics.Vector3(6f, 0f, 0f);
            body.Get<GameFramework.World.Velocity>().Linear = System.Numerics.Vector3.Zero;
            barrelSystem.Tick(2, Dt);

            Assert.Greater(body.Get<KnockbackBody>().KnockbackEndTick, firstEnd, "풀렸다 다시 닿으면 다시 친다");
        }

        [Test]
        public void 드럼통이_거의_서면_민_사람_크레딧을_지운다()
        {
            Build();
            var slow = AddBarrel("slow", 0f, 0f, 0.4f, 0f);
            slow.Get<KnockbackBarrel>().PusherId = "old";
            var fast = AddBarrel("fast", 0f, 6f, 5f, 0f);
            fast.Get<KnockbackBarrel>().PusherId = "now";

            barrelSystem.Tick(0, Dt);

            Assert.IsNull(slow.Get<KnockbackBarrel>().PusherId, "문턱 아래로 느려지면 옛 크레딧은 사라진다");
            Assert.AreEqual("now", fast.Get<KnockbackBarrel>().PusherId, "아직 빠른 쪽은 그대로");
        }

        [Test]
        public void 드럼통끼리_부딪히면_친_쪽의_민_사람이_맞은_드럼통으로_넘어간다()
        {
            Build();
            var a = AddBarrel("a", 0f, 0f, 5f, 0f);
            a.Get<KnockbackBarrel>().PusherId = "p";
            var b = AddBarrel("b", 1.0f, 0f);
            b.Get<KnockbackBarrel>().PusherId = "other";

            barrelSystem.Tick(0, Dt);

            Assert.AreEqual("p", b.Get<KnockbackBarrel>().PusherId, "더 빠른 쪽의 민 사람이 막타를 물려준다");
            Assert.AreEqual("p", a.Get<KnockbackBarrel>().PusherId);
        }

        [Test]
        public void 드럼통끼리_부딪힐_때_친_쪽에_민_사람이_없으면_맞은_쪽도_지워진다()
        {
            Build();
            AddBarrel("a", 0f, 0f, 5f, 0f);
            var b = AddBarrel("b", 1.0f, 0f);
            b.Get<KnockbackBarrel>().PusherId = "old";

            barrelSystem.Tick(0, Dt);

            Assert.IsNull(b.Get<KnockbackBarrel>().PusherId, "친 쪽에 크레딧이 없으면 옛 크레딧을 남기지 않는다");
        }

        [Test]
        public void 탄_출발점이_드럼통_안이면_그_드럼통은_표적에서_빠지고_탄은_뒤의_드럼통을_민다()
        {
            Build();
            AddBody("a", 0f, 0f);
            var inside = AddBarrel("in", 0.2f, 0f);
            var outside = AddBarrel("out", 4f, 0f);
            world.Shots.Add(new KnockbackShot("a", 10, Vector2.zero, Vector2.right));

            for (long t = 10; t <= 20; t++) hitSystem.Tick(t, Dt);

            Assert.AreEqual(0f, Vel(inside).magnitude, 1e-6f, "발밑 드럼통은 안 맞는다");
            Assert.Greater(Vel(outside).x, 5f, "양성 대조: 탄은 지나가 뒤의 드럼통을 민다");
        }

        [Test]
        public void 치운_드럼통_번호는_치운_뒤_다시_쓰여도_다시_치울_수_있다()
        {
            Build();
            AddBarrel("b1", 11.5f, 0f, 6f, 0f);
            long tick = 0;
            while (tick < 400 && despawned.Count == 0) barrelSystem.Tick(tick++, Dt);
            Assert.AreEqual(1, despawned.Count);

            registry.Remove("b1");
            barrelSystem.Tick(tick++, Dt);
            AddBarrel("b1", 11.5f, 0f, 6f, 0f);
            while (tick < 900 && despawned.Count == 1) barrelSystem.Tick(tick++, Dt);

            Assert.AreEqual(2, despawned.Count, "새 판의 같은 번호도 치워진다");
        }

        [Test]
        public void 같은_초기_조건이면_같은_결과다()
        {
            List<Vector2> Simulate()
            {
                Build(3);
                var a = AddBarrel("a", 0f, 0f, 5f, 1f);
                var b = AddBarrel("b", 3f, 0.3f, -2f, 0f);
                var c = AddBarrel("c", 1.5f, 2f, 0f, -3f);
                Run(0, 120);
                return new List<Vector2> { Pos(a), Pos(b), Pos(c), Vel(a), Vel(b), Vel(c) };
            }

            var first = Simulate();
            var second = Simulate();
            for (int i = 0; i < first.Count; i++) Assert.AreEqual(first[i], second[i]);
            Assert.Greater(first[0].magnitude + first[1].magnitude + first[2].magnitude, 0.1f, "실제로 움직였다");
        }

        [Test]
        public void 드럼통_개수_0인_맵에서는_아무것도_하지_않는다()
        {
            Build(0);
            var b = AddBarrel("b1", 0f, 0f, 5f, 0f);
            Run(0, 10);
            Assert.AreEqual(5f, Vel(b).x, 1e-6f, "시스템이 손대지 않는다");
            Assert.AreEqual(0, despawned.Count);

            AddBody("a", 0f, 0f);
            world.Shots.Add(new KnockbackShot("a", 10, Vector2.zero, Vector2.right));
            hitSystem.Tick(14, Dt);
            Assert.AreEqual(5f, Vel(b).x, 1e-6f, "탄도 드럼통을 안 민다");
        }

        [Test]
        public void 출발_전에는_굴리지_않는다()
        {
            Build();
            world.GameplayStartTick = 100;
            var b = AddBarrel("b1", 0f, 0f, 5f, 0f);
            barrelSystem.Tick(50, Dt);
            Assert.AreEqual(0f, Pos(b).x, 1e-6f);
        }
    }
}
