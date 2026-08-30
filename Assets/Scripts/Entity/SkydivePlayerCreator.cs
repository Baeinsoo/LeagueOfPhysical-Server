using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>Skydive의 플레이어 몸(서버). 체력·마나·레벨·어빌리티가 없다.</summary>
    public class SkydivePlayerCreator : ICharacterCreator
    {
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly SkydiveConfig config;

        public SkydivePlayerCreator(GameFramework.World.EntityRegistry entityRegistry, SkydiveConfig config)
        {
            this.entityRegistry = entityRegistry;
            this.config = config;
        }

        public void Create(CharacterCreationData creationData)
        {
            var worldEntity = new GameFramework.World.Entity(creationData.entityId);
            worldEntity.Add(new GameFramework.World.Transform
            {
                Position = creationData.position.ToNumerics(),
                Rotation = Quaternion.Euler(creationData.rotation).ToNumerics(),
            });
            worldEntity.Add(new GameFramework.World.Velocity { Linear = creationData.velocity.ToNumerics() });
            worldEntity.Add(new EntityKind(EntityType.Character));
            worldEntity.Add(new Appearance(creationData.visualId));
            worldEntity.Add(new MotionContributions());
            worldEntity.Add(new GameFramework.World.CapsuleShape(config.BodyRadius, config.BodyHeight));
            worldEntity.Add(new GameFramework.World.PhysicsConfig(
                GameFramework.World.BodyKind.Kinematic, freezeRotation: true, isTrigger: false));

            if (string.IsNullOrEmpty(creationData.userId) == false)
            {
                worldEntity.Add(new GameFramework.World.Ownership(creationData.userId));
                worldEntity.Add(new InputBuffer());
            }
            worldEntity.Add(new GameFramework.World.Simulated());   // 서버는 모든 몸을 시뮬한다
            worldEntity.Add(new Posture());
            worldEntity.Add(new Stamina { Current = config.StaminaMax });
            entityRegistry.Add(worldEntity);

            Debug.Log($"[World] Registered skydive body {worldEntity.Id}");
        }
    }
}
