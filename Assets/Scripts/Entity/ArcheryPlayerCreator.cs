using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// Archery의 플레이어 몸(서버). 걷지 않으므로 이동·접지·충돌 부품이 없다 —
    /// 이 게임에는 그런 개념이 없다. 대신 활을 겨누는 상태를 갖는다.
    /// </summary>
    public class ArcheryPlayerCreator : ICharacterCreator
    {
        private readonly GameFramework.World.EntityRegistry entityRegistry;

        public ArcheryPlayerCreator(GameFramework.World.EntityRegistry entityRegistry)
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
            worldEntity.Add(new ArcheryAim());
            worldEntity.Add(new InputBuffer());
            worldEntity.Add(new GameFramework.World.Simulated());   // 서버는 모든 몸을 시뮬한다

            // 걷지는 않지만 EntityBinder가 모든 엔티티에 물리 몸을 붙인다 — 모양과 종류가 없으면
            // PhysicsBodyFactory가 거기서 예외를 던진다. 클라와 같은 치수를 써야 예측이 안 어긋난다.
            worldEntity.Add(new GameFramework.World.CapsuleShape(
                BodySizes.CharacterRadius, BodySizes.CharacterHeight));
            worldEntity.Add(new GameFramework.World.PhysicsConfig(
                GameFramework.World.BodyKind.Kinematic, freezeRotation: true, isTrigger: false));

            entityRegistry.Add(worldEntity);

            Debug.Log($"[World] Registered archer {worldEntity.Id}");
        }
    }
}
