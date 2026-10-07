using GameFramework;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class SkydiveRetrySystemTests
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

        static GameFramework.World.Entity Diver(string id, Vector3 position)
        {
            var entity = new GameFramework.World.Entity(id);
            entity.Add(new GameFramework.World.Transform { Position = position.ToNumerics() });
            entity.Add(new GameFramework.World.Velocity { Linear = new System.Numerics.Vector3(0f, -60f, 0f) });
            entity.Add(new EntityKind(EntityType.Character));
            entity.Add(new GameFramework.World.Simulated());
            entity.Add(new Stamina { Current = 10f });
            entity.Add(new Posture { Axis = 1f, Gliding = true });
            entity.Add(new FinishState());
            return entity;
        }

        [Test]
        public void 별을_놓치고_구름_아래로_빠지면_출구_아래에서_다시_떨어진다()
        {
            var registry = new GameFramework.World.EntityRegistry();
            var diver = Diver("a", new Vector3(5f, -20f, 0f));
            registry.Add(diver);
            var field = new RetryField();
            field.Add(new RetryZone(-15f, new Vector3(0f, 270f, 0f)));

            new SkydiveRetrySystem(registry, field, Config()).Tick(0, 0.02f);

            Vector3 p = GameFramework.World.EntityMotionExtensions.GetPosition(diver);
            Assert.AreEqual(270f, p.y, 1e-3f);
            Assert.AreEqual(100f, diver.Get<Stamina>().Current, 1e-3f);
            Assert.IsFalse(diver.Get<Posture>().Gliding);
        }

        [Test]
        public void 구름_위에서는_그대로다()
        {
            var registry = new GameFramework.World.EntityRegistry();
            var diver = Diver("a", new Vector3(5f, 40f, 0f));
            registry.Add(diver);
            var field = new RetryField();
            field.Add(new RetryZone(-15f, new Vector3(0f, 270f, 0f)));

            new SkydiveRetrySystem(registry, field, Config()).Tick(0, 0.02f);

            Assert.AreEqual(40f, GameFramework.World.EntityMotionExtensions.GetPosition(diver).y, 1e-3f);
        }
    }
}
