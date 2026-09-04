using GameFramework;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class SkydiveLaserSystemTests
    {
        const float DeltaTime = 0.02f;

        static SkydiveConfig Config()
            => new SkydiveConfig(
                spreadFallSpeed: 60f, diveFallSpeed: 90f, glideFallSpeed: 6f,
                spreadMoveSpeed: 12f, diveMoveSpeed: 9f, glideMoveSpeed: 14f,
                spreadTurnAccel: 22f, diveTurnAccel: 6f, glideTurnAccel: 18f,
                fallApproach: 29f, postureRate: 4f,
                bodyRadius: 0.4f, bodyHeight: 1.8f, groundY: 0f,
                staminaMax: 100f, glideDrain: 20f, groundRecover: 40f, emergencyGlideTime: 1f,
                groundMoveSpeed: 4f, groundAccel: 100f, jumpPower: 11f, poseClearance: 5f, fallBrake: 150f,
                glideWindLag: 0.2f, spreadWindLag: 2.06f, diveWindLag: 3.1f);

        //  선반 1800과 1400 사이, x축을 따라 통로를 가로지르는 고정 빔.
        static Laser CrossingLaser()
            => new Laser(new System.Numerics.Vector3(-5f, 1600f, 0f), length: 10f, radius: 0.1f,
                startAngle: 0f, angularSpeed: 0f, sweepHalfRange: 0f, period: 0, onTicks: 0, phase: 0);

        //  선반 1800 부활 지점(30,1800,-10) 바로 아래를 지나는 빔 — "부활 지점 근처를 지나는 빔"
        //  시나리오를 실제로 재현해 무적 창이 없으면 곧바로 다시 걸리게 만든다.
        static Laser LaserNearRespawnPoint()
            => new Laser(new System.Numerics.Vector3(27f, 1600f, -10f), length: 10f, radius: 0.1f,
                startAngle: 0f, angularSpeed: 0f, sweepHalfRange: 0f, period: 0, onTicks: 0, phase: 0);

        static GameFramework.World.Entity Diver(string id, Vector3 position)
        {
            var entity = new GameFramework.World.Entity(id);
            entity.Add(new GameFramework.World.Transform { Position = position.ToNumerics() });
            entity.Add(new GameFramework.World.Velocity());
            entity.Add(new EntityKind(EntityType.Character));
            entity.Add(new GameFramework.World.Simulated());
            entity.Add(new Stamina { Current = 10f });
            entity.Add(new Posture { Axis = 1f, Gliding = true });
            return entity;
        }

        static SkydiveLaserSystem BuildSystem(GameFramework.World.EntityRegistry registry, LaserField laserField)
            => new SkydiveLaserSystem(registry, laserField, Config(),
                SkydiveCourseLayout.ShelfYs, SkydiveCourseLayout.SpawnY, SkydiveCourseLayout.RespawnPoints);

        [Test]
        public void 레이저를_지나면_마지막_선반으로_되돌아가고_스태미나가_찬다()
        {
            var registry = new GameFramework.World.EntityRegistry();
            var diver = Diver("diver-1", new Vector3(0f, 1650f, 0f));   // 선반 1800 아래, 빔(1600) 위
            registry.Add(diver);

            var laserField = new LaserField();
            laserField.Add(CrossingLaser());

            var system = BuildSystem(registry, laserField);

            system.Tick(1, DeltaTime);   // 첫 틱 — 위치만 캐시
            diver.Get<GameFramework.World.Transform>().Position = new Vector3(0f, 1550f, 0f).ToNumerics();   // 빔을 뚫고 낙하
            system.Tick(2, DeltaTime);   // 이번 틱에 지나온 경로가 빔을 가로지른다

            var transform = diver.Get<GameFramework.World.Transform>();
            var expected = SkydiveCourseLayout.RespawnPoints[1800f] + new Vector3(2f, 0f, 0f);   // 첫 부활은 spread 각도 0
            Assert.AreEqual(expected.x, transform.Position.X, 0.001f);
            Assert.AreEqual(expected.y, transform.Position.Y, 0.001f);
            Assert.AreEqual(expected.z, transform.Position.Z, 0.001f);

            var stamina = diver.Get<Stamina>();
            Assert.AreEqual(Config().StaminaMax, stamina.Current);
            Assert.IsFalse(stamina.EmergencyUsed);
            Assert.AreEqual(0f, stamina.EmergencyRemaining);

            var posture = diver.Get<Posture>();
            Assert.AreEqual(0f, posture.Axis);
            Assert.IsFalse(posture.Gliding);
        }

        [Test]
        public void 부활은_텔레포트_카운트를_올린다()
        {
            var registry = new GameFramework.World.EntityRegistry();
            var diver = Diver("diver-1", new Vector3(0f, 1650f, 0f));
            registry.Add(diver);

            var laserField = new LaserField();
            laserField.Add(CrossingLaser());

            var system = BuildSystem(registry, laserField);

            Assert.AreEqual(0, diver.Get<GameFramework.World.Transform>().TeleportCount);

            system.Tick(1, DeltaTime);
            diver.Get<GameFramework.World.Transform>().Position = new Vector3(0f, 1550f, 0f).ToNumerics();
            system.Tick(2, DeltaTime);

            //  클라가 이 카운트 변화를 보고 보간 대신 스냅 처리한다 — 부활은 이어지는 이동이 아니다.
            Assert.AreEqual(1, diver.Get<GameFramework.World.Transform>().TeleportCount);
        }

        [Test]
        public void 첫_틱은_캐시된_위치가_없어_아무도_판정하지_않는다()
        {
            var registry = new GameFramework.World.EntityRegistry();
            //  원점에서 이 위치까지 지나왔다고 잘못 가정하면 빔(1600)을 가로지르는 자리.
            var start = new Vector3(0f, 1700f, 0f);
            var diver = Diver("diver-1", start);
            registry.Add(diver);

            var laserField = new LaserField();
            laserField.Add(CrossingLaser());

            var system = BuildSystem(registry, laserField);

            system.Tick(1, DeltaTime);

            var transform = diver.Get<GameFramework.World.Transform>();
            Assert.AreEqual(start.x, transform.Position.X, 0.001f);
            Assert.AreEqual(start.y, transform.Position.Y, 0.001f);
            Assert.AreEqual(start.z, transform.Position.Z, 0.001f);
            Assert.AreEqual(0, transform.TeleportCount);
        }

        [Test]
        public void 무적_시간_동안은_같은_빔에_다시_걸려도_되돌아가지_않는다()
        {
            var registry = new GameFramework.World.EntityRegistry();
            var diver = Diver("diver-1", new Vector3(0f, 1650f, 0f));
            registry.Add(diver);

            var laserField = new LaserField();
            laserField.Add(CrossingLaser());
            laserField.Add(LaserNearRespawnPoint());   // 부활 지점(32,1800,-10) 바로 아래를 지나는 빔

            var system = BuildSystem(registry, laserField);

            system.Tick(1, DeltaTime);
            diver.Get<GameFramework.World.Transform>().Position = new Vector3(0f, 1550f, 0f).ToNumerics();
            system.Tick(2, DeltaTime);   // 첫 번째 부활(→ 32,1800,-10) — 여기서 무적 창이 열린다

            var transform = diver.Get<GameFramework.World.Transform>();
            int teleportCountAfterFirstRespawn = transform.TeleportCount;
            var stamina = diver.Get<Stamina>();
            stamina.Current = 3f;   // 부활 이후 스태미나를 소모했다고 가정 — 다시 부활하면 안 바뀌어야 한다

            //  무적 창 안에서 부활 지점 바로 아래 빔을 가로지르도록 낙하시킨다(x=32 그대로, y만 내려감).
            //  무적이 없다면 LaserNearRespawnPoint에 곧바로 다시 걸린다.
            var fallThroughAgain = new Vector3(32f, 1550f, -10f);
            transform.Position = fallThroughAgain.ToNumerics();
            system.Tick(3, DeltaTime);   // now=0.06 << 무적 만료(2.04)

            Assert.AreEqual(fallThroughAgain.x, transform.Position.X, 0.001f);
            Assert.AreEqual(fallThroughAgain.y, transform.Position.Y, 0.001f);
            Assert.AreEqual(fallThroughAgain.z, transform.Position.Z, 0.001f);
            Assert.AreEqual(teleportCountAfterFirstRespawn, transform.TeleportCount);
            Assert.AreEqual(3f, stamina.Current);
        }
    }
}
