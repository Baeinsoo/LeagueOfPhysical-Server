using GameFramework;
using GameFramework.Runner;
using System.Linq;
using UnityEngine;

namespace LOP
{
    public class EnemyBrain : IBrain
    {
        private const int AttackSlot = 1;   // 기본 공격 자리 — 캐릭터마다 실제 어빌리티가 다르다

        private AbilityActivator abilityActivator;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly GameFramework.World.StatsSystem statsSystem;
        private readonly ITickUpdater tickUpdater;

        public EnemyBrain(AbilityActivator abilityActivator, GameFramework.World.EntityRegistry entityRegistry, GameFramework.World.StatsSystem statsSystem, ITickUpdater tickUpdater)
        {
            this.abilityActivator = abilityActivator;
            this.entityRegistry = entityRegistry;
            this.statsSystem = statsSystem;
            this.tickUpdater = tickUpdater;
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
                //  Attack the player — 기본 공격 자리(슬롯 1) 발동. 플레이어와 동일 경로.
                abilityActivator.TryActivateSlot(worldEntity.Id, AttackSlot, tickUpdater.tick);
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
