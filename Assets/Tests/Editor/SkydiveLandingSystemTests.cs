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
                landingLethalSpeed: 15f,
                restitution: 0.35f);

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

            //  SkydiveDoorSystemTests.닫힌_문_안에_있으면_마지막_선반으로_되돌아간다와 같은 방식으로
            //  기대 좌표를 구한다 — 부활 순번이 비어 있던 참이라 spread 각도는 0(=+X로 2m).
            var transform = diver.Get<GameFramework.World.Transform>();
            Vector3 expected = SkydiveCourseLayout.RespawnPoints[200f] + new Vector3(2f, 0f, 0f);
            Assert.AreEqual(expected.x, transform.Position.X, 0.001f, "마지막으로 지난 선반이 아니라 엉뚱한 자리로 갔다(x)");
            Assert.AreEqual(expected.y, transform.Position.Y, 0.001f, "마지막으로 지난 선반이 아니라 엉뚱한 자리로 갔다(y)");
            Assert.AreEqual(expected.z, transform.Position.Z, 0.001f, "마지막으로 지난 선반이 아니라 엉뚱한 자리로 갔다(z)");
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
            //  Diver()가 만드는 속도는 기본값(0,0,0)이라 "지워졌는지"를 못 잰다 — 지우기 전에도
            //  이미 0이면 이 단언은 부활이 실제로 한 일이 없어도 통과해버린다. 그래서 죽음을
            //  부를 만한 낙하 속도를 미리 넣어 둔다. 부활이 진짜로 지운 경우에만 아래 단언이
            //  통과한다.
            diver.Get<GameFramework.World.Velocity>().Linear = new Vector3(0f, -60f, 0f).ToNumerics();
            registry.Add(diver);

            var system = System(registry);
            system.Tick(0, 0.02f);
            Vector3 afterRespawn = GameFramework.World.EntityMotionExtensions.GetPosition(diver);

            //  다음 틱: 이동이 아직 안 돌아 충격 값은 그대로다. 죽음 반복을 막는 주된 장치는 사실
            //  따로 있다 — 다음 틱 이동(SkydiveWorld.MoveBlockedByMap)이 "이미 서 있던" 다이버의
            //  LandingImpact를 자동으로 0으로 비운다. 여기서 보는 속도 지우기는 그와 별개의
            //  보조 장치다: 부활 직후에도 치명 낙하 속도가 그대로 남아 있으면 물리 팔로워·스냅샷이
            //  잠깐이라도 그 속도를 그대로 비추기 때문에 지운다.
            Assert.That(GameFramework.World.EntityMotionExtensions.GetVelocity(diver).magnitude,
                        Is.EqualTo(0f).Within(1e-3f), "부활이 속도를 안 지웠다 — 다음 틱에 또 죽는다");
            Assert.That(afterRespawn.y, Is.GreaterThan(10f));
        }

        /// <summary>
        /// 같은 틱에 레이저·문이 먼저 이 다이버를 부활시켜 놓은 경우를 흉내낸다. 착지 충격은
        /// 그 사고와 무관하게(이동이 이미 이 틱에 적어 둔 값이라) 남아 있는데, 그걸 그대로 두면
        /// SkydiveLandingSystem이 "방금 부활한 자리"를 죽은 자리로 알고 선반을 한 번 더 되돌려
        /// 한 사고에 두 구간을 잃는다. 한 사고는 한 구간만 잃어야 한다.
        /// </summary>
        [Test]
        public void 같은_틱에_먼저_부활했으면_착지가_두_번_되돌리지_않는다()
        {
            var registry = new GameFramework.World.EntityRegistry();
            //  1000과 1400 사이(800)에서 죽었다고 하면 마지막으로 지난 선반은 1000이다.
            var diver = Diver("a", 800f);
            diver.Get<LandingImpact>().DownwardSpeed = 60f;
            registry.Add(diver);

            //  이 틱의 더 앞선 시스템(레이저/문)이 다른 사고로 이미 부활시켰다고 가정한다.
            int order = 0;
            SkydiveRespawn.To(diver, 800f, Config(),
                              SkydiveCourseLayout.ShelfYs, SkydiveCourseLayout.SpawnY,
                              SkydiveCourseLayout.RespawnPoints, ref order);
            float shelfYAfterFirstRespawn = GameFramework.World.EntityMotionExtensions.GetPosition(diver).y;

            //  착지 충격은 그 부활과 무관하게 아직 60으로 남아 있을 수 있다(고치기 전 코드) —
            //  이 시스템이 그 낡은 값을 보고 또 되돌리면 안 된다.
            System(registry).Tick(0, 0.02f);

            float shelfYAfterLandingTick = GameFramework.World.EntityMotionExtensions.GetPosition(diver).y;
            Assert.AreEqual(shelfYAfterFirstRespawn, shelfYAfterLandingTick, 0.001f,
                "같은 사고인데 착지 판정이 선반을 한 번 더 되돌렸다 — 한 사고에 두 구간을 잃었다");
        }
    }
}
