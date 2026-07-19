using GameFramework;
using System.Linq;
using UnityEngine;

namespace LOP
{
    public class EnemyBrain : IBrain
    {
        private const int AttackAbilityId = 3;   // TbAbility attack 행(grant-all로 모든 캐릭터 보유)

        private AbilityActivator abilityActivator;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly GameFramework.World.StatsSystem statsSystem;

        public EnemyBrain(AbilityActivator abilityActivator, GameFramework.World.EntityRegistry entityRegistry, GameFramework.World.StatsSystem statsSystem)
        {
            this.abilityActivator = abilityActivator;
            this.entityRegistry = entityRegistry;
            this.statsSystem = statsSystem;
        }

        public void Think(GameFramework.World.Entity worldEntity, double deltaTime)
        {
            Vector3 entityPosition = GameFramework.World.EntityMotionExtensions.GetPosition(worldEntity);

            //  Find the player
            GameFramework.World.Entity target = entityRegistry.All
                .Where(e => e.Has<GameFramework.World.Ownership>())
                .Where(e => (GameFramework.World.EntityMotionExtensions.GetPosition(e) - entityPosition).magnitude <= 10)
                .OrderBy(e => (GameFramework.World.EntityMotionExtensions.GetPosition(e) - entityPosition).sqrMagnitude)
                .FirstOrDefault();

            if (target == null)
            {
                return;
            }

            Vector3 direction = GameFramework.World.EntityMotionExtensions.GetPosition(target) - entityPosition;

            // Rotate
            float myFloat = 0;
            var angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            var smooth = Mathf.SmoothDampAngle(GameFramework.World.EntityMotionExtensions.GetRotation(worldEntity).y, angle, ref myFloat, 0.01f);
            GameFramework.World.EntityMotionExtensions.SetRotation(worldEntity, new Vector3(0, smooth, 0));

            if (direction.magnitude < 2)
            {
                //  Attack the player — 공격 어빌리티(id=3) 발동. 플레이어와 동일 경로라 데미지·연출 cue 자동.
                abilityActivator.TryActivate(worldEntity.Id, AttackAbilityId, Runner.Time.tick);
            }
            else
            {
                //  Move
                var stats = worldEntity.Get<GameFramework.World.Stats>();
                float speed = statsSystem.GetValue(stats, (int)GameFramework.World.EntityStatType.MoveSpeed);
                var velocity = direction.normalized * speed;
                var currentVelocity = GameFramework.World.EntityMotionExtensions.GetVelocity(worldEntity);
                GameFramework.World.EntityMotionExtensions.SetVelocity(worldEntity, new Vector3(velocity.x, currentVelocity.y, velocity.z));
            }
        }
    }
}
