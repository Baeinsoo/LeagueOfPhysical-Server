using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 카드 러시의 몸(서버). 시뮬에 물리가 없다 — 선수끼리 안 부딪히고, 자리는 월드가 상태에서 계산해 적는다(PhysX 대리 몸은 키네마틱 트리거).
    /// 출발 자리가 곧 레인 출발점이다(룰이 레인 출발점에 스폰한다).
    /// </summary>
    public class CardRushRunnerCreator : ICharacterCreator
    {
        private readonly GameFramework.World.EntityRegistry entityRegistry;

        public CardRushRunnerCreator(GameFramework.World.EntityRegistry entityRegistry)
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

            // 이 몸이 누구 것인지. 서버 공용 시스템들이 이걸로 엔티티→유저→세션을 찾는다.
            if (string.IsNullOrEmpty(creationData.userId) == false)
            {
                worldEntity.Add(new GameFramework.World.Ownership(creationData.userId));
            }
            worldEntity.Add(new InputBuffer());
            worldEntity.Add(new GameFramework.World.Simulated());

            //  결승 틱은 월드가 상태에서 다시 적고, 등수는 FinishTrackingSystem이 적어 스냅에 싣는다.
            worldEntity.Add(new FinishState());
            worldEntity.Add(new FinishPlacement());
            worldEntity.Add(new CardRushRunner { LaneOrigin = creationData.position });

            //  시뮬엔 물리가 없지만 EntityBinder가 모든 몸에 PhysX 대리 몸을 세운다(PhysicsBodyFactory) — 모양과 종류가 있어야 한다.
            //  클라와 같은 키네마틱 트리거라 아무것도 밀지 않고, 물리 루프도 키네마틱 몸은 월드 자리에 다시 쓰지 않는다.
            worldEntity.Add(new GameFramework.World.CapsuleShape(BodySizes.CharacterRadius, BodySizes.CharacterHeight));
            worldEntity.Add(new GameFramework.World.PhysicsConfig(
                GameFramework.World.BodyKind.Kinematic, freezeRotation: true, isTrigger: true));

            entityRegistry.Add(worldEntity);

            Debug.Log($"[World] Registered card rush runner {worldEntity.Id}");
        }
    }
}
