using GameFramework;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class SkydiveDoorSystemTests
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
                glideWindLag: 0.2f, spreadWindLag: 2.06f, diveWindLag: 3.1f,
                landingLethalSpeed: 15f);

        //  문 중심(0,100,0), HalfWidth/HalfDepth=5, Thickness=0.5. 몸(반지름 0.4, 키 1.8)이
        //  y=99.8에 있으면 캡슐이 [100.2, 101.2]가 되어 패널(99.75~100.25)에 걸린다 —
        //  DoorCrushTests와 같은 배치.
        static DoorVolume PlaceDoor(int period, int openTicks, int moveTicks)
        {
            var go = new GameObject("door-test");
            go.transform.position = new Vector3(0f, 100f, 0f);
            var volume = go.AddComponent<DoorVolume>();
            volume.HalfWidth = 5f;
            volume.HalfDepth = 5f;
            volume.Thickness = 0.5f;
            volume.AxisAngleDegrees = 0f;
            volume.Period = period;
            volume.OpenTicks = openTicks;
            volume.MoveTicks = moveTicks;
            volume.Phase = 0;
            return volume;
        }

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

        static SkydiveDoorSystem BuildSystem(GameFramework.World.EntityRegistry registry, DoorField doorField)
            => new SkydiveDoorSystem(registry, doorField, Config(),
                SkydiveCourseLayout.ShelfYs, SkydiveCourseLayout.SpawnY, SkydiveCourseLayout.RespawnPoints);

        [Test]
        public void 닫힌_문_안에_있으면_마지막_선반으로_되돌아간다()
        {
            var registry = new GameFramework.World.EntityRegistry();
            var diver = Diver("diver-1", new Vector3(0f, 99.8f, 0f));
            registry.Add(diver);

            var doorField = new DoorField();
            var door = PlaceDoor(period: 20, openTicks: 6, moveTicks: 4);   // 닫힘 구간 = [10,16)
            try
            {
                doorField.Add(door);
                var system = BuildSystem(registry, doorField);

                system.Tick(12, DeltaTime);   // 완전히 닫힌 틱

                var transform = diver.Get<GameFramework.World.Transform>();
                var expected = SkydiveCourseLayout.RespawnPoints[200f] + new Vector3(2f, 0f, 0f);   // 첫 부활은 spread 각도 0
                Assert.AreEqual(expected.x, transform.Position.X, 0.001f);
                Assert.AreEqual(expected.y, transform.Position.Y, 0.001f);
                Assert.AreEqual(expected.z, transform.Position.Z, 0.001f);

                var stamina = diver.Get<Stamina>();
                Assert.AreEqual(Config().StaminaMax, stamina.Current);

                var posture = diver.Get<Posture>();
                Assert.AreEqual(0f, posture.Axis);
                Assert.IsFalse(posture.Gliding);
            }
            finally
            {
                Object.DestroyImmediate(door.gameObject);
            }
        }

        [Test]
        public void 열려_있으면_안_되돌아간다()
        {
            var registry = new GameFramework.World.EntityRegistry();
            var start = new Vector3(0f, 99.8f, 0f);
            var diver = Diver("diver-1", start);
            registry.Add(diver);

            var doorField = new DoorField();
            var door = PlaceDoor(period: 20, openTicks: 6, moveTicks: 4);
            try
            {
                doorField.Add(door);
                var system = BuildSystem(registry, doorField);

                system.Tick(0, DeltaTime);   // 열림 구간

                var transform = diver.Get<GameFramework.World.Transform>();
                Assert.AreEqual(start.x, transform.Position.X, 0.001f);
                Assert.AreEqual(start.y, transform.Position.Y, 0.001f);
                Assert.AreEqual(start.z, transform.Position.Z, 0.001f);
                Assert.AreEqual(0, transform.TeleportCount);
            }
            finally
            {
                Object.DestroyImmediate(door.gameObject);
            }
        }

        [Test]
        public void 닫히는_중이면_안_되돌아간다()
        {
            var registry = new GameFramework.World.EntityRegistry();
            var start = new Vector3(0f, 99.8f, 0f);
            var diver = Diver("diver-1", start);
            registry.Add(diver);

            var doorField = new DoorField();
            var door = PlaceDoor(period: 20, openTicks: 6, moveTicks: 4);   // 닫히는 중 = [6,10)
            try
            {
                doorField.Add(door);
                var system = BuildSystem(registry, doorField);

                system.Tick(8, DeltaTime);   // 밀려날 기회를 줘야 하는 구간

                var transform = diver.Get<GameFramework.World.Transform>();
                Assert.AreEqual(start.x, transform.Position.X, 0.001f);
                Assert.AreEqual(start.y, transform.Position.Y, 0.001f);
                Assert.AreEqual(start.z, transform.Position.Z, 0.001f);
                Assert.AreEqual(0, transform.TeleportCount);
            }
            finally
            {
                Object.DestroyImmediate(door.gameObject);
            }
        }

        [Test]
        public void 부활은_텔레포트_카운트를_올린다()
        {
            var registry = new GameFramework.World.EntityRegistry();
            var diver = Diver("diver-1", new Vector3(0f, 99.8f, 0f));
            registry.Add(diver);

            var doorField = new DoorField();
            var door = PlaceDoor(period: 20, openTicks: 6, moveTicks: 4);
            try
            {
                doorField.Add(door);
                var system = BuildSystem(registry, doorField);

                Assert.AreEqual(0, diver.Get<GameFramework.World.Transform>().TeleportCount);

                system.Tick(12, DeltaTime);

                //  클라가 이 카운트 변화를 보고 보간 대신 스냅 처리한다 — 부활은 이어지는 이동이 아니다.
                Assert.AreEqual(1, diver.Get<GameFramework.World.Transform>().TeleportCount);
            }
            finally
            {
                Object.DestroyImmediate(door.gameObject);
            }
        }
    }
}
