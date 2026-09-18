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
        private readonly ArcheryCourse course;

        public ArcheryPlayerCreator(GameFramework.World.EntityRegistry entityRegistry, ArcheryCourse course)
        {
            this.entityRegistry = entityRegistry;
            this.course = course;
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
            worldEntity.Add(new ArcheryScore());
            //  사거리 맵은 화살이 과녁 수만큼이다. 웨이브 맵은 0을 돌려주므로 그릇을 안 붙인다 —
            //  붙이는 순간 한 발도 못 쏘게 되므로 이 조건이 곧 "원형 맵은 안 바뀐다"의 보증이다.
            int arrows = course.ArrowsPerArcher;
            if (arrows > 0)
            {
                worldEntity.Add(new ArcheryQuiver { Remaining = arrows });
            }
            // 이 몸이 누구 것인지. 서버 공용 시스템들이 이걸로 엔티티→유저→세션을 찾는다 —
            // 없으면 InputTimingFeedbackSystem이 null을 키로 조회하다 터지고, 그 예외가 틱 루프를
            // 통째로 죽인다(실측: 틱 360에서 시뮬 정지).
            if (string.IsNullOrEmpty(creationData.userId) == false)
            {
                worldEntity.Add(new GameFramework.World.Ownership(creationData.userId));
            }
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
