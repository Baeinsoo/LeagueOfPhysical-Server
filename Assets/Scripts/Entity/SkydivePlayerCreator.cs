using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>Skydive의 플레이어 몸(서버). 체력·마나·레벨·어빌리티가 없다.</summary>
    public class SkydivePlayerCreator : ICharacterCreator
    {
        // 몸 크기. 클라(SkydivePlayerCreator)도 같은 값을 상수로 든다 — 슬라이스 2에서
        // TbSkydiveConfig로 옮길 때 한쪽만 옮기면 클·서 캡슐 크기가 갈라진다(컴파일도 테스트도
        // 못 잡는다). 옮길 땐 반드시 같이 옮길 것.
        private const float BodyRadius = 0.4f;
        private const float BodyHeight = 1.8f;

        private readonly GameFramework.World.EntityRegistry entityRegistry;

        public SkydivePlayerCreator(GameFramework.World.EntityRegistry entityRegistry)
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
            worldEntity.Add(new GameFramework.World.CapsuleShape(BodyRadius, BodyHeight));
            worldEntity.Add(new GameFramework.World.PhysicsConfig(
                GameFramework.World.BodyKind.Kinematic, freezeRotation: true, isTrigger: false));

            if (string.IsNullOrEmpty(creationData.userId) == false)
            {
                worldEntity.Add(new GameFramework.World.Ownership(creationData.userId));
                worldEntity.Add(new InputBuffer());
            }
            worldEntity.Add(new GameFramework.World.Simulated());   // 서버는 모든 몸을 시뮬한다
            entityRegistry.Add(worldEntity);

            Debug.Log($"[World] Registered skydive body {worldEntity.Id}");
        }
    }
}
