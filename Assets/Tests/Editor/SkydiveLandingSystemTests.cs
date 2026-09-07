using GameFramework;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class SkydiveLandingSystemTests
    {
        static SkydiveConfig Config()
            => new SkydiveConfig(
                spreadFallSpeed: 60f, diveFallSpeed: 90f, glideFallSpeed: 6f,
                spreadMoveSpeed: 12f, diveMoveSpeed: 9f, glideMoveSpeed: 14f,
                spreadTurnAccel: 22f, diveTurnAccel: 6f, glideTurnAccel: 18f,
                fallApproach: 29f, postureRate: 4f,
                bodyRadius: 0.4f, bodyHeight: 1.8f, groundY: 0f,
                staminaMax: 100f, glideDrain: 20f, groundRecover: 40f, emergencyGlideTime: 1f,
                groundMoveSpeed: 4f, groundAccel: 100f, jumpPower: 11f, poseClearance: 5f, fallBrake: 150f,
                glideWindLag: 0.2f, spreadWindLag: 2.06f, diveWindLag: 3.1f,
                landingLethalSpeed: 15f);

        //  SkydiveDoorSystemTests의 조립을 그대로 따른다 — 판정 대상 집합이 어긋나면 안 되므로
        //  다이버를 만드는 방식도 같아야 한다.
        static GameFramework.World.Entity Diver(string id, float y)
        {
            var e = new GameFramework.World.Entity(id);
            e.Add(new GameFramework.World.Transform { Position = new Vector3(0f, y, 0f).ToNumerics() });
            e.Add(new GameFramework.World.Velocity());
            e.Add(new EntityKind(EntityType.Character));
            e.Add(new GameFramework.World.Simulated());
            e.Add(new LandingImpact());
            return e;
        }

        static SkydiveLandingSystem System(GameFramework.World.EntityRegistry registry)
            => new SkydiveLandingSystem(registry, Config(),
                                        SkydiveCourseLayout.ShelfYs,
                                        SkydiveCourseLayout.SpawnY,
                                        SkydiveCourseLayout.RespawnPoints);

        [Test]
        public void 치명_속도로_착지하면_마지막_선반으로_되돌린다()
        {
            var registry = new GameFramework.World.EntityRegistry();
            //  마지막 선반(200)보다 아래에서 죽으면 그 선반으로 돌아간다.
            var diver = Diver("a", 10f);
            diver.Get<LandingImpact>().DownwardSpeed = 60f;
            registry.Add(diver);

            System(registry).Tick(0, 0.02f);

            float y = GameFramework.World.EntityMotionExtensions.GetPosition(diver).y;
            Assert.Greater(y, 10f, "되돌리지 않았다 — 죽은 자리에 그대로 있다");
            Assert.That(y, Is.EqualTo(SkydiveCourseLayout.ShelfYs[SkydiveCourseLayout.ShelfYs.Count - 1])
                             .Within(50f),
                        "마지막으로 지난 선반이 아니라 엉뚱한 자리로 갔다");
        }

        [Test]
        public void 안전_속도로_착지하면_그대로_둔다()
        {
            var registry = new GameFramework.World.EntityRegistry();
            var diver = Diver("a", 10f);
            diver.Get<LandingImpact>().DownwardSpeed = 6f;
            registry.Add(diver);

            System(registry).Tick(0, 0.02f);

            Assert.That(GameFramework.World.EntityMotionExtensions.GetPosition(diver).y,
                        Is.EqualTo(10f).Within(1e-3f), "안전한 착지인데 되돌렸다");
        }

        /// <summary>부활은 속도를 0으로 지우므로(SkydiveRespawn) 그 다음 틱에 또 죽지 않는다.</summary>
        [Test]
        public void 되돌린_뒤_즉시_또_죽지_않는다()
        {
            var registry = new GameFramework.World.EntityRegistry();
            var diver = Diver("a", 10f);
            diver.Get<LandingImpact>().DownwardSpeed = 60f;
            registry.Add(diver);

            var system = System(registry);
            system.Tick(0, 0.02f);
            Vector3 afterRespawn = GameFramework.World.EntityMotionExtensions.GetPosition(diver);

            //  다음 틱: 이동이 아직 안 돌아 충격 값은 그대로다. 그래도 두 번 되돌리면 안 된다는
            //  뜻이 아니라, 부활이 속도를 지웠는지를 본다 — 그것이 죽음 반복 루프를 막는 유일한 장치다.
            Assert.That(GameFramework.World.EntityMotionExtensions.GetVelocity(diver).magnitude,
                        Is.EqualTo(0f).Within(1e-3f), "부활이 속도를 안 지웠다 — 다음 틱에 또 죽는다");
            Assert.That(afterRespawn.y, Is.GreaterThan(10f));
        }
    }
}
