using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// Flappy Race의 플레이어 몸(새)을 만든다(서버). 캐릭터와 달리 체력·마나·레벨·어빌리티가 없다.
    /// </summary>
    public class FlappyBirdCreator : ICharacterCreator
    {
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly FlappyConfig config;

        public FlappyBirdCreator(GameFramework.World.EntityRegistry entityRegistry, FlappyConfig config)
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
            // 새 몸은 시뮬이 쓰는 그 값(TbFlappyConfig)에서 온다 — 물리 팔로워가 다른 몸을 세우면
            // 겹침 밀어내기가 시뮬이 모르는 위치 점프를 만든다.
            worldEntity.Add(new GameFramework.World.CapsuleShape(config.BodyRadius, config.BodyHeight));
            worldEntity.Add(new FinishState());
            // 지금까지 EntityBinder가 하드코딩하던 값을 그대로 옮긴 것 — 거동 변화 없음.
            worldEntity.Add(new GameFramework.World.PhysicsConfig(
                GameFramework.World.BodyKind.Kinematic, freezeRotation: true, isTrigger: false));
            worldEntity.Add(new FlappyStun());
            worldEntity.Add(new FlappyDash());

            if (string.IsNullOrEmpty(creationData.userId) == false)
            {
                worldEntity.Add(new GameFramework.World.Ownership(creationData.userId));
                worldEntity.Add(new InputBuffer());
            }
            worldEntity.Add(new GameFramework.World.Simulated());   // 서버는 모든 몸을 시뮬한다
            entityRegistry.Add(worldEntity);

            Debug.Log($"[World] Registered flappy bird {worldEntity.Id}");
        }
    }
}
