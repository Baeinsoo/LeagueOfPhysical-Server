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
                                     (Vector2, float)? zone = null)
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
    }
}
