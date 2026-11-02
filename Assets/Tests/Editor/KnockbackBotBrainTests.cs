using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>넉백 봇 두뇌는 순수하다 — 보이는 것을 넣으면 사람과 같은 입력이 나온다.</summary>
    public class KnockbackBotBrainTests
    {
        const float Dt = 0.02f;

        static readonly KnockbackConfig Config = new KnockbackConfig(120f, 6f, 10f, 12f, 2f, 0.5f, 5f, 11f, 30f, 18f, 0.3f, 0.55f, 3,
            0.95f, 0.26f, 1.2f, 1, 6f, 0.5f, 0.5f, 0.3f, 0.1f, 0.2f, 0.8f, 5f, new List<MountedArcheryZoneStageConfig>());

        static KnockbackBotView View(Vector2 position, long tick = 1000,
                                     IReadOnlyList<KnockbackBotBody> enemies = null, IReadOnlyList<KnockbackBotShot> shots = null,
                                     (Vector2, float)? zone = null, int ammo = 2, long cooldownEndTick = 0,
                                     long knockbackEndTick = 0, bool falling = false)
        {
            var circle = zone ?? (Vector2.zero, 100f);
            return new KnockbackBotView
            {
                SelfId = "me",
                Tick = tick,
                TickInterval = Dt,
                Started = true,
                Position = position,
                Velocity = Vector2.zero,
                Enemies = enemies ?? new List<KnockbackBotBody>(),
                Shots = shots ?? new List<KnockbackBotShot>(),
                Zone = circle,
                NextZone = circle,
                Config = Config,
                Ammo = ammo,
                CooldownEndTick = cooldownEndTick,
                KnockbackEndTick = knockbackEndTick,
                Falling = falling,
            };
        }

        static KnockbackBotBrain Brain(int seed = 1) => new KnockbackBotBrain(new System.Random(seed));

        [Test]
        public void 날아오는_탄을_피한다()
        {
            // 내 6 m 앞까지 +x로 곧장 날아온 탄. 쏜 지 0.6초라 이미 알아챈 뒤다.
            long fireTick = 1000 - 30;
            var me = new Vector2(6f + 30f * 30 * Dt, 0f);
            var shots = new List<KnockbackBotShot> { new KnockbackBotShot("enemy", fireTick, Vector2.zero, Vector2.right) };
            var command = Brain().Think(View(me, 1000, shots: shots));

            Assert.Greater(Mathf.Abs(command.Vertical), 0.4f, "탄 경로에 수직으로 움직인다");
            Assert.Less(Mathf.Abs(command.Horizontal), 0.01f, "탄 방향 성분은 없다");
        }

        [Test]
        public void 내_탄은_피하지_않는다()
        {
            var me = new Vector2(10f, 0f);
            var shots = new List<KnockbackBotShot> { new KnockbackBotShot("me", 990, Vector2.zero, Vector2.right) };
            var command = Brain().Think(View(me, 1000, shots: shots));
            Assert.AreEqual(0f, command.Vertical, 0.001f);
        }

        [Test]
        public void 가장자리면_안쪽으로()
        {
            // 반지름 10 원의 오른쪽 끝(9.5 m)에 서 있다 — 중심(-x)으로 향한다.
            var command = Brain().Think(View(new Vector2(9.5f, 0f), zone: (Vector2.zero, 10f)));
            Assert.Less(command.Horizontal, -0.9f);
        }

        [Test]
        public void 다음_원이_공개되면_그_안쪽으로()
        {
            var view = View(new Vector2(0f, 0f), zone: (Vector2.zero, 50f));
            view.NextZone = (new Vector2(20f, 0f), 6f);
            var command = Brain().Think(view);
            Assert.Greater(command.Horizontal, 0.9f);
        }

        [Test]
        public void 사거리_안이면_쏜다()
        {
            var enemies = new List<KnockbackBotBody> { new KnockbackBotBody("enemy", new Vector2(0f, 10f), Vector2.zero) };
            var brain = Brain();
            bool fired = false;
            InputCommand fire = null;
            // 발사 간격(최대 1.3초 = 65틱)이 지나도록 계속 생각시킨다.
            for (int i = 0; i < 80 && fired == false; i++)
            {
                var command = brain.Think(View(Vector2.zero, 1000 + i, enemies));
                if (command.Fire)
                {
                    fired = true;
                    fire = command;
                }
            }
            Assert.IsTrue(fired);
            // +z(위쪽)에 있는 적 — 조준 오차 최대 0.14 rad 안에서 0도 근처를 향한다.
            Assert.AreEqual(0f, fire.AimYaw, 9f);
        }

        [Test]
        public void 날아가는_중엔_쏘지_않고_예약도_안_민다()
        {
            var enemies = new List<KnockbackBotBody> { new KnockbackBotBody("enemy", new Vector2(0f, 10f), Vector2.zero) };
            var brain = Brain();
            // 예약 시각(최대 65틱)이 지나도록 날아가는 중 — 한 번도 쏘지 않는다.
            for (int i = 0; i < 100; i++)
            {
                Assert.IsFalse(brain.Think(View(Vector2.zero, 1000 + i, enemies, knockbackEndTick: 1100)).Fire);
            }
            // 날림이 끝난 바로 그 틱에 쏜다(예약이 밀리지 않았다).
            Assert.IsTrue(brain.Think(View(Vector2.zero, 1100, enemies, knockbackEndTick: 1100)).Fire);
        }

        [Test]
        public void 떨어지는_중엔_쏘지_않고_예약도_안_민다()
        {
            var enemies = new List<KnockbackBotBody> { new KnockbackBotBody("enemy", new Vector2(0f, 10f), Vector2.zero) };
            var brain = Brain();
            for (int i = 0; i < 100; i++)
            {
                Assert.IsFalse(brain.Think(View(Vector2.zero, 1000 + i, enemies, falling: true)).Fire);
            }
            Assert.IsTrue(brain.Think(View(Vector2.zero, 1100, enemies)).Fire);
        }

        [Test]
        public void 첫_틱에는_바로_쏘지_않는다()
        {
            var enemies = new List<KnockbackBotBody> { new KnockbackBotBody("enemy", new Vector2(0f, 10f), Vector2.zero) };
            Assert.IsFalse(Brain().Think(View(Vector2.zero, 1000, enemies)).Fire);
        }

        [Test]
        public void 사거리_밖이면_쏘지_않는다()
        {
            var enemies = new List<KnockbackBotBody> { new KnockbackBotBody("enemy", new Vector2(0f, 40f), Vector2.zero) };
            var brain = Brain();
            for (int i = 0; i < 200; i++)
            {
                Assert.IsFalse(brain.Think(View(Vector2.zero, 1000 + i, enemies)).Fire);
            }
        }

        [Test]
        public void 걷는_적은_앞질러_겨눈다()
        {
            // 적이 +x로 걷는다 — 앞질러 겨누면 정지한 적을 겨눌 때보다 +x(양의 yaw) 쪽으로 기운다.
            var still = new List<KnockbackBotBody> { new KnockbackBotBody("enemy", new Vector2(0f, 12f), Vector2.zero) };
            var walking = new List<KnockbackBotBody> { new KnockbackBotBody("enemy", new Vector2(0f, 12f), new Vector2(6f, 0f)) };
            float yawStill = FirstFireYaw(still);
            float yawWalking = FirstFireYaw(walking);
            // 같은 씨앗이라 조준 오차도 같다 — 차이는 앞질러 겨눈 몫이다.
            Assert.Greater(yawWalking - yawStill, 2f);
        }

        static float FirstFireYaw(List<KnockbackBotBody> enemies)
        {
            var brain = Brain(7);
            for (int i = 0; i < 100; i++)
            {
                var command = brain.Think(View(Vector2.zero, 1000 + i, enemies));
                if (command.Fire) return command.AimYaw;
            }
            Assert.Fail("쏘지 않았다");
            return 0f;
        }

        [Test]
        public void 가장자리_근처에서_틱마다_방향이_뒤집히지_않는다()
        {
            // 가장자리 경계선(반지름 10 - 여유 3.5)에 서서 바깥 쪽 먼 적을 본다. 움직임을 따라 위치도 옮긴다.
            var enemies = new List<KnockbackBotBody> { new KnockbackBotBody("enemy", new Vector2(18f, 0f), Vector2.zero) };
            var brain = Brain();
            var position = new Vector2(6.5f, 0f);
            float previous = 0f;
            for (int i = 0; i < 20; i++)
            {
                var command = brain.Think(View(position, 1000 + i, enemies, zone: (Vector2.zero, 10f)));
                float radial = command.Horizontal;
                Assert.IsFalse(previous * radial < -1e-4f, $"틱 {i}: {previous} -> {radial}");
                previous = radial;
                position += new Vector2(command.Horizontal, command.Vertical) * 6f * Dt;
            }
        }

        [Test]
        public void 비슷한_거리의_두_적_사이에서_목표를_유지한다()
        {
            var brain = Brain();
            string first = null;
            for (int i = 0; i < 30; i++)
            {
                float da = i % 2 == 0 ? 10.01f : 9.99f;
                float db = i % 2 == 0 ? 9.99f : 10.01f;
                var enemies = new List<KnockbackBotBody>
                {
                    new KnockbackBotBody("a", new Vector2(da, 0f), Vector2.zero),
                    new KnockbackBotBody("b", new Vector2(-db, 0f), Vector2.zero),
                };
                brain.Think(View(Vector2.zero, 1000 + i, enemies));
                first ??= brain.TargetId;
                //  목표가 없으면(null == null) 유지를 안 해도 통과하므로 먼저 붙잡았는지 본다.
                Assert.IsNotNull(first, "적을 붙잡아야 한다");
                Assert.AreEqual(first, brain.TargetId, $"틱 {i}");
            }
        }

        [Test]
        public void 시작_전에는_가만히_있는다()
        {
            var view = View(new Vector2(9.5f, 0f), zone: (Vector2.zero, 10f));
            view.Started = false;
            var command = Brain().Think(view);
            Assert.AreEqual(0f, command.Horizontal);
            Assert.IsFalse(command.Fire);
        }

        [Test]
        public void 같은_씨앗이면_같은_결정()
        {
            var enemies = new List<KnockbackBotBody> { new KnockbackBotBody("enemy", new Vector2(3f, 9f), new Vector2(1f, 0f)) };
            var a = Brain(42);
            var b = Brain(42);
            for (int i = 0; i < 150; i++)
            {
                var ca = a.Think(View(Vector2.zero, 1000 + i, enemies));
                var cb = b.Think(View(Vector2.zero, 1000 + i, enemies));
                Assert.AreEqual(ca.Horizontal, cb.Horizontal);
                Assert.AreEqual(ca.Vertical, cb.Vertical);
                Assert.AreEqual(ca.Fire, cb.Fire);
                Assert.AreEqual(ca.AimYaw, cb.AimYaw);
            }
        }

        [Test]
        public void 이동_입력_길이는_1_이하()
        {
            var enemies = new List<KnockbackBotBody> { new KnockbackBotBody("enemy", new Vector2(0f, 20f), Vector2.zero) };
            var brain = Brain();
            for (int i = 0; i < 100; i++)
            {
                var c = brain.Think(View(Vector2.zero, 1000 + i, enemies));
                Assert.LessOrEqual(new Vector2(c.Horizontal, c.Vertical).magnitude, 1.0001f);
            }
        }

        [Test]
        public void 같은_팀은_적_목록에_없다()
        {
            var team = new Dictionary<string, int> { ["me"] = 1, ["mate"] = 1, ["foe"] = 2, ["solo"] = 0 };
            var bodies = new List<KnockbackBotBody>
            {
                new KnockbackBotBody("mate", Vector2.zero, Vector2.zero),
                new KnockbackBotBody("foe", Vector2.zero, Vector2.zero),
                new KnockbackBotBody("solo", Vector2.zero, Vector2.zero),
            };
            var shots = new List<KnockbackBotShot>
            {
                new KnockbackBotShot("mate", 1, Vector2.zero, Vector2.right),
                new KnockbackBotShot("foe", 1, Vector2.zero, Vector2.right),
            };
            var enemies = new List<KnockbackBotBody>();
            var threats = new List<KnockbackBotShot>();
            KnockbackBotSystem.FilterByTeam("me", team, bodies, shots, enemies, threats);

            CollectionAssert.AreEqual(new[] { "foe", "solo" }, enemies.ConvertAll(b => b.Id));
            CollectionAssert.AreEqual(new[] { "foe" }, threats.ConvertAll(s => s.ShooterId));

            team["me"] = 0;
            KnockbackBotSystem.FilterByTeam("me", team, bodies, shots, enemies, threats);
            Assert.AreEqual(3, enemies.Count, "개인전은 거르지 않는다");
        }

        static List<KnockbackBotBody> OneEnemyInRange() =>
            new List<KnockbackBotBody> { new KnockbackBotBody("enemy", new Vector2(0f, 10f), Vector2.zero) };

        [Test]
        public void 탄이_없으면_쏘지_않는다()
        {
            var brain = Brain();
            var enemies = OneEnemyInRange();
            for (int i = 0; i < 200; i++)
            {
                Assert.IsFalse(brain.Think(View(Vector2.zero, 1000 + i, enemies, ammo: 0)).Fire);
            }
        }

        [Test]
        public void 연사_간격_중엔_쏘지_않는다()
        {
            var brain = Brain();
            var enemies = OneEnemyInRange();
            for (int i = 0; i < 200; i++)
            {
                Assert.IsFalse(brain.Think(View(Vector2.zero, 1000 + i, enemies, cooldownEndTick: 5000)).Fire);
            }
        }

        [Test]
        public void 탄이_차면_다음_틱에_바로_쏜다()
        {
            var brain = Brain();
            var enemies = OneEnemyInRange();
            // 탄이 바닥난 채로 발사 간격(최대 1.3초=65틱)보다 오래 버틴다
            for (int i = 0; i < 300; i++)
            {
                Assert.IsFalse(brain.Think(View(Vector2.zero, 1000 + i, enemies, ammo: 0)).Fire);
            }
            Assert.IsTrue(brain.Think(View(Vector2.zero, 1300, enemies, ammo: 1)).Fire, "탄이 찬 첫 틱에 쏜다");
        }

        [Test]
        public void 탄과_간격이_되면_쏜다_양성_대조()
        {
            var brain = Brain();
            var enemies = OneEnemyInRange();
            bool fired = false;
            for (int i = 0; i < 100 && !fired; i++)
            {
                fired = brain.Think(View(Vector2.zero, 1000 + i, enemies, ammo: 2, cooldownEndTick: 900)).Fire;
            }
            Assert.IsTrue(fired);
        }

        [Test]
        public void 가까이_떨어진_상자_쪽으로_간다()
        {
            var view = View(new Vector2(0f, 0f));
            view.CratePosition = new Vector2(4f, 0f);
            var command = Brain().Think(view);
            Assert.Greater(command.Horizontal, 0.8f);
            Assert.Less(Mathf.Abs(command.Vertical), 0.01f);
        }

        [Test]
        public void 멀거나_날려지는_중이거나_이미_폭탄을_들었으면_상자를_안_쫓는다()
        {
            var far = View(new Vector2(0f, 0f));
            far.CratePosition = new Vector2(6.5f, 0f);
            Assert.AreEqual(0f, Brain().Think(far).Horizontal, 1e-4f);

            var flying = View(new Vector2(0f, 0f), knockbackEndTick: 2000);
            flying.CratePosition = new Vector2(4f, 0f);
            Assert.AreEqual(0f, Brain().Think(flying).Horizontal, 1e-4f);

            var armed = View(new Vector2(0f, 0f));
            armed.CratePosition = new Vector2(4f, 0f);
            armed.HasBomb = true;
            Assert.AreEqual(0f, Brain().Think(armed).Horizontal, 1e-4f);

            //  양성 대조 - 6 m 안이면 쫓는다.
            var near = View(new Vector2(0f, 0f));
            near.CratePosition = new Vector2(5.9f, 0f);
            Assert.Greater(Brain().Think(near).Horizontal, 0.8f);
        }

        [Test]
        public void 가장자리_피하기가_상자보다_앞선다()
        {
            //  원 가장자리 바깥쪽에 상자가 있어도 중심으로 돌아온다.
            var view = View(new Vector2(11.5f, 0f), zone: (Vector2.zero, 12f));
            view.CratePosition = new Vector2(14f, 0f);
            Assert.Less(Brain().Think(view).Horizontal, 0f);
        }
    }
}
