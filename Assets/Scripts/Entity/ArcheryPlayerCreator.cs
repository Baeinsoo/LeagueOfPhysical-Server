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
            entityRegistry.Add(worldEntity);

            Debug.Log($"[World] Registered archer {worldEntity.Id}");
        }
    }
}
