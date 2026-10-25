using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>봇 두뇌는 순수하다 — 보이는 것을 넣으면 사람과 같은 입력이 나온다.</summary>
    public class MountedArcheryBotBrainTests
    {
        const float Dt = 0.02f;

        static readonly MountSettings Mount = new MountSettings(13f, 9f, 16f, 172f, 92f, 100f, 0.35f, 0.5f, 3f);

        static MountedArcheryConfig Config(float aimErrorDeg = 0f, float reactionMin = 0.3f, float reactionMax = 0.3f)
            => new MountedArcheryConfig(150f, 40f, 80f, 2.5f, Mount, 100, 34, 0.5f, 100f, 30f, null,
                                        4f, 90f, 0.6f, 0.5f, 15f, aimErrorDeg, reactionMin, reactionMax, 60f);

        static BotView View(MountedArcheryConfig config, Vector3 position, float heading = 0f,
                            IReadOnlyList<BotBody> bodies = null, IReadOnlyList<BotArrow> arrows = null,
                            System.Func<Vector3, Vector3, bool> visible = null, long tick = 100,
                            (Vector2, float)? zone = null)
        {
            var circle = zone ?? (Vector2.zero, 100f);
            return new BotView
            {
                SelfId = "me",
                Tick = tick,
                TickInterval = Dt,
                Started = true,
                Position = position,
                HeadingDeg = heading,
                Velocity = Vector3.zero,
                Zone = circle,
                NextZone = circle,
                Bodies = bodies ?? new List<BotBody>(),
                Arrows = arrows ?? new List<BotArrow>(),
                Visible = visible ?? ((a, b) => true),
                Config = config,
            };
        }

        static List<BotBody> Enemy(Vector3 position, Vector3 velocity = default)
            => new List<BotBody> { new BotBody("enemy", position, 0f, velocity) };

        /// <summary>당김이 만작에 닿을 때까지 시뮬처럼 당김 상태를 올려 가며 생각시킨다. 놓은 틱의 커맨드와 놓을 때 당김을 돌려준다.</summary>
        static (InputCommand release, float drawAtRelease) DrawAndRelease(MountedArcheryBotBrain brain, BotView view, int maxTicks = 200)
        {
            bool drawing = false;
            float ratio = 0f;
            for (int i = 0; i < maxTicks; i++)
            {
                view.Tick += 1;
                view.AimDrawing = drawing;
                view.AimDrawRatio = ratio;
                var command = brain.Think(view);
                if (command.Release)
                {
                    return (command, ratio);
                }
                if (command.Drawing && drawing == false)
                {
                    ratio = 0f;
                }
                drawing = command.Drawing;
                ratio = Mathf.MoveTowards(ratio, drawing ? 1f : 0f,
                    (drawing ? ArcheryAimSystem.DrawRisePerSecond : ArcheryAimSystem.DrawFallPerSecond) * Dt);
            }
            return (null, ratio);
        }

        /// <summary>
        /// 서버와 같은 순서로 화살을 날려 본다: 쏜 틱에 쏜 사람·과녁이 한 틱 움직이고, 화살이 s초 날았을 때
        /// 과녁은 (s + 한 틱) 뒤 자리에 있다. 판정은 공유 식(<see cref="ArcheryTrajectory"/>·<see cref="MountedArcheryHitTest"/>) 그대로.
        /// </summary>
        static bool Hits(MountedArcheryConfig config, Vector3 shooter, InputCommand release, float drawAtRelease,
                         Vector3 target, Vector3 targetVelocity, float targetHeading = 0f)
        {
            float speed = ArcheryAimSystem.SpeedFor(drawAtRelease, config.ArrowMinSpeed, config.ArrowMaxSpeed);
            var shot = new ArcheryShot("me", 0, shooter + Vector3.up * config.RiderEyeHeight,
                ArcheryTrajectory.DirectionFrom(release.AimYaw, release.AimPitch) * speed);
            for (int k = 1; k * Dt <= ArcheryTrajectory.LifetimeSeconds; k++)
            {
                Vector3 from = ArcheryTrajectory.PositionAt(shot, (k - 1) * Dt);
                Vector3 to = ArcheryTrajectory.PositionAt(shot, k * Dt);
                Vector3 body = target + targetVelocity * (Dt + k * Dt);
                if (MountedArcheryHitTest.SegmentHits(from, to, body, targetHeading, out _))
                {
                    return true;
                }
            }
            return false;
        }

        [Test]
        public void 원_밖이면_원_중심으로_간다()
        {
            var config = Config();
            //  원(반지름 60) 밖 동쪽 90 m에서 북쪽을 보고 있다 — 중심은 왼쪽(서쪽)이다.
            var brain = new MountedArcheryBotBrain(config, new System.Random(1));
            var command = brain.Think(View(config, new Vector3(90f, 0f, 0f), heading: 0f, zone: (Vector2.zero, 60f)));

            Assert.AreEqual(MountedArcheryBotBrain.Mode.Zone, brain.CurrentMode);
            Assert.AreEqual(-1f, command.Horizontal, 1e-4f, "중심 쪽(왼쪽)으로 끝까지 돌아야 한다");
            Assert.Greater(command.Vertical, 0.9f);

            //  이미 중심을 보고 있으면 곧장 달린다.
            var facing = new MountedArcheryBotBrain(config, new System.Random(1));
            var straight = facing.Think(View(config, new Vector3(90f, 0f, 0f), heading: -90f, zone: (Vector2.zero, 60f)));
            Assert.AreEqual(0f, straight.Horizontal, 1e-3f);
            Assert.Greater(straight.Vertical, 0.9f);
        }

        [Test]
        public void 원_밖이면_보이는_적보다_원이_먼저다()
        {
            var config = Config();
            var brain = new MountedArcheryBotBrain(config, new System.Random(1));
            var command = brain.Think(View(config, new Vector3(90f, 0f, 0f), heading: 0f,
                bodies: Enemy(new Vector3(90f, 0f, 30f)), zone: (Vector2.zero, 60f)));

            Assert.AreEqual(MountedArcheryBotBrain.Mode.Zone, brain.CurrentMode);
            Assert.AreEqual(-1f, command.Horizontal, 1e-4f);
            Assert.IsTrue(command.Drawing, "원으로 달리면서도 보이는 적에게는 당긴다");
        }

        [Test]
        public void 보이는_적이면_당겨서_쏜다()
        {
            var config = Config();
            var brain = new MountedArcheryBotBrain(config, new System.Random(1));
            var view = View(config, Vector3.zero, bodies: Enemy(new Vector3(0f, 0f, 30f)));

            var first = brain.Think(view);
            Assert.AreEqual(MountedArcheryBotBrain.Mode.Fight, brain.CurrentMode);
            Assert.IsTrue(first.Drawing);

            var (release, draw) = DrawAndRelease(brain, view);
            Assert.IsNotNull(release, "만작에 닿으면 놓아야 한다");
            Assert.GreaterOrEqual(draw, MountedArcheryBotBrain.ReleaseDraw);
            Assert.Greater(release.ViewTick, view.Tick, "봇은 지연이 없어 그 틱을 화면 틱으로 싣는다");
        }

        [Test]
        public void 가린_적은_쏘지_않는다()
        {
            var config = Config();
            var brain = new MountedArcheryBotBrain(config, new System.Random(1));
            var view = View(config, Vector3.zero, bodies: Enemy(new Vector3(0f, 0f, 30f)), visible: (a, b) => false);

            var (release, _) = DrawAndRelease(brain, view, 100);
            Assert.IsNull(release);
            Assert.IsFalse(brain.Think(view).Drawing);
            Assert.AreNotEqual(MountedArcheryBotBrain.Mode.Fight, brain.CurrentMode);
        }

        [Test]
        public void 육십m_밖_적은_쏘지_않는다()
        {
            var config = Config();
            var brain = new MountedArcheryBotBrain(config, new System.Random(1));
            var view = View(config, Vector3.zero, bodies: Enemy(new Vector3(0f, 0f, 61f)));

            var (release, _) = DrawAndRelease(brain, view, 100);
            Assert.IsNull(release);
            Assert.IsFalse(brain.Think(view).Drawing);

            //  양성 대조: 59 m면 쏜다.
            var near = new MountedArcheryBotBrain(config, new System.Random(1));
            var (nearRelease, _) = DrawAndRelease(near, View(config, Vector3.zero, bodies: Enemy(new Vector3(0f, 0f, 59f))));
            Assert.IsNotNull(nearRelease);
        }

        [Test]
        public void 날아오는_화살은_반응_지연_뒤에_피한다()
        {
            var config = Config(reactionMin: 0.3f, reactionMax: 0.3f);   // 15틱
            //  원 밖에서 중심을 보고 곧장 달리는 중이다(스틱 좌우 0) — 피하기가 시작되면 좌우 끝까지로 바뀌는 게 보인다.
            var me = new Vector3(90f, 0f, 0f);
            const float heading = -90f;
            (Vector2, float) zone = (Vector2.zero, 60f);
            //  서쪽 60 m에서 동쪽으로 날아오는 화살 — 1.5초 뒤 내 몸 가운데에 닿는다.
            var shot = new ArcheryShot("enemy", 0, me + new Vector3(-60f, MountedArcheryBotBrain.AimHeight, 0f),
                new Vector3(40f, 0.5f * ArcheryTrajectory.Gravity * 1.5f, 0f));

            var dodger = new MountedArcheryBotBrain(config, new System.Random(7));
            var calm = new MountedArcheryBotBrain(config, new System.Random(7));   // 같은 씨앗, 화살을 못 본다
            const long start = 100;
            const int reactionTicks = 15;
            for (int i = 0; i <= reactionTicks + 2; i++)
            {
                float age = i * Dt;
                var arrows = new List<BotArrow>
                {
                    new BotArrow("enemy", ArcheryTrajectory.PositionAt(shot, age), ArcheryTrajectory.VelocityAt(shot, age)),
                };
                var seen = dodger.Think(View(config, me, heading, arrows: arrows, tick: start + i, zone: zone));
                var unseen = calm.Think(View(config, me, heading, tick: start + i, zone: zone));

                Assert.AreEqual(0f, unseen.Horizontal, 1e-3f, "대조: 화살을 못 보면 곧장 달린다");
                if (i < reactionTicks)
                {
                    Assert.AreEqual(unseen.Horizontal, seen.Horizontal, 1e-5f, $"반응 지연 전({i}틱)엔 그대로 달린다");
                }
                else
                {
                    Assert.AreEqual(1f, Mathf.Abs(seen.Horizontal), 1e-5f, $"지연 뒤({i}틱)엔 옆으로 끝까지 튼다");
                    Assert.AreEqual(MountedArcheryBotBrain.Mode.Dodge, dodger.CurrentMode);
                }
            }
        }

        [Test]
        public void 빗나가는_화살은_피하지_않는다()
        {
            var config = Config(reactionMin: 0.3f, reactionMax: 0.3f);
            //  옆으로 10 m 비켜 지나가는 화살.
            var shot = new ArcheryShot("enemy", 0, new Vector3(10f, MountedArcheryBotBrain.AimHeight, 60f),
                new Vector3(0f, 0.5f * ArcheryTrajectory.Gravity * 1.5f, -40f));
            var brain = new MountedArcheryBotBrain(config, new System.Random(7));
            for (int i = 0; i < 30; i++)
            {
                float age = i * Dt;
                var arrows = new List<BotArrow>
                {
                    new BotArrow("enemy", ArcheryTrajectory.PositionAt(shot, age), ArcheryTrajectory.VelocityAt(shot, age)),
                };
                brain.Think(View(config, Vector3.zero, arrows: arrows, tick: 100 + i));
                Assert.AreNotEqual(MountedArcheryBotBrain.Mode.Dodge, brain.CurrentMode);
            }
        }

        [Test]
        public void 오차_0이면_서_있는_과녁을_맞힌다()
        {
            var config = Config(aimErrorDeg: 0f);
            //  50 m면 낙하가 약 1.9 m — 낙하 보정이 빠지면 확실히 빗나간다.
            var target = new Vector3(20f, 0f, 46f);
            var brain = new MountedArcheryBotBrain(config, new System.Random(1));
            var (release, draw) = DrawAndRelease(brain, View(config, Vector3.zero, bodies: Enemy(target)));

            Assert.IsNotNull(release);
            Assert.IsTrue(Hits(config, Vector3.zero, release, draw, target, Vector3.zero));
            //  대조: 과녁 자리를 3 m 옮기면 같은 화살은 빗나간다 — 위 단정이 아무 데나 맞는 화살로 통과하지 않는다.
            Assert.IsFalse(Hits(config, Vector3.zero, release, draw, target + new Vector3(3f, 0f, 0f), Vector3.zero));
        }

        [Test]
        public void 오차_0이면_달리는_과녁을_앞질러_맞힌다()
        {
            var config = Config(aimErrorDeg: 0f);
            var target = new Vector3(0f, 0f, 40f);
            var velocity = new Vector3(13f, 0f, 0f);   // 최고 속도로 옆으로 달린다
            var brain = new MountedArcheryBotBrain(config, new System.Random(1));

            //  당기는 동안 과녁을 같은 자리로 붙잡아 둔다 — 놓는 틱의 자리·속도가 겨누는 근거다.
            var view = View(config, Vector3.zero, bodies: Enemy(target, velocity));
            var (release, draw) = DrawAndRelease(brain, view);

            Assert.IsNotNull(release);
            Assert.IsTrue(Hits(config, Vector3.zero, release, draw, target, velocity, 90f));
            Assert.IsFalse(Hits(config, Vector3.zero, release, draw, target, Vector3.zero, 90f),
                "대조: 앞질러 겨눈 화살은 서 있는 과녁 자리는 지나친다");
        }

        [Test]
        public void 오차가_있으면_발마다_조준이_다르다()
        {
            var config = Config(aimErrorDeg: 2.5f);
            var brain = new MountedArcheryBotBrain(config, new System.Random(3));
            var view = View(config, Vector3.zero, bodies: Enemy(new Vector3(0f, 0f, 30f)));

            var (first, _) = DrawAndRelease(brain, view);
            view.Tick += 200;
            var (second, _) = DrawAndRelease(brain, view);

            Assert.IsNotNull(first);
            Assert.IsNotNull(second);
            Assert.AreNotEqual(first.AimYaw, second.AimYaw);
            Assert.AreNotEqual(first.AimPitch, second.AimPitch);

            //  대조: 오차 0이면 같은 자리를 두 번 똑같이 겨눈다.
            var exact = new MountedArcheryBotBrain(Config(aimErrorDeg: 0f), new System.Random(3));
            var exactView = View(Config(aimErrorDeg: 0f), Vector3.zero, bodies: Enemy(new Vector3(0f, 0f, 30f)));
            var (a, _) = DrawAndRelease(exact, exactView);
            exactView.Tick += 200;
            var (b, _) = DrawAndRelease(exact, exactView);
            Assert.AreEqual(a.AimYaw, b.AimYaw, 1e-4f);
            Assert.AreEqual(a.AimPitch, b.AimPitch, 1e-4f);
        }
    }
}
