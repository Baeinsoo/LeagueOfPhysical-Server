using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 넉백 생존전의 몸(서버). 걷고 떨어질 뿐이라 능력·탈것은 없다.
    /// </summary>
    public class KnockbackPlayerCreator : ICharacterCreator
    {
        private readonly GameFramework.World.EntityRegistry entityRegistry;

        public KnockbackPlayerCreator(GameFramework.World.EntityRegistry entityRegistry)
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
            worldEntity.Add(new GameFramework.World.Velocity());
            worldEntity.Add(new EntityKind(EntityType.Character));
            worldEntity.Add(new Appearance(creationData.visualId));
            PlayerLookAttach.Attach(worldEntity, creationData.look);
            worldEntity.Add(new GameFramework.World.Health(creationData.maxHP) { Current = creationData.currentHP });
            worldEntity.Add(new GameFramework.World.GroundState { IsGrounded = true });

            // 이 몸이 누구 것인지. 서버 공용 시스템들이 이걸로 엔티티→유저→세션을 찾는다.
            if (string.IsNullOrEmpty(creationData.userId) == false)
            {
                worldEntity.Add(new GameFramework.World.Ownership(creationData.userId));
            }
            worldEntity.Add(new InputBuffer());
            worldEntity.Add(new GameFramework.World.Simulated());

            // 클라와 같은 값이어야 예측이 안 어긋난다. 이 모양이 없으면 키네마틱 이동이 몸을 조용히 건너뛴다.
            worldEntity.Add(new GameFramework.World.CapsuleShape(BodySizes.CharacterRadius, BodySizes.CharacterHeight));
            worldEntity.Add(new GameFramework.World.PhysicsConfig(
                GameFramework.World.BodyKind.Kinematic, freezeRotation: true, isTrigger: false));

            entityRegistry.Add(worldEntity);

            Debug.Log($"[World] Registered knockback body {worldEntity.Id}");
        }
    }
}
