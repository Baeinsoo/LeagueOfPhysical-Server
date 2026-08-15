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

        public FlappyBirdCreator(GameFramework.World.EntityRegistry entityRegistry)
        {
            this.entityRegistry = entityRegistry;
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
