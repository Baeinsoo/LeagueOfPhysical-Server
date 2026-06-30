using GameFramework;
using System;
using System.Linq;
using UnityEngine;

namespace LOP
{
    public class EnemyBrain : IBrain<LOPEntity>
    {
        private const int AttackAbilityId = 3;   // TbAbility attack 행(grant-all로 모든 캐릭터 보유)

        private AbilityActivator abilityActivator;
        private readonly GameFramework.World.EntityRegistry entityRegistry;

        public EnemyBrain(AbilityActivator abilityActivator, GameFramework.World.EntityRegistry entityRegistry)
        {
            this.abilityActivator = abilityActivator;
            this.entityRegistry = entityRegistry;
        }

        public void Think(LOPEntity entity, double deltaTime)
        {
            //  Find the player
            var entities = Runner.current.entityManager.GetEntities<LOPEntity>();
            LOPEntity target = entities
                .Where(e => entityRegistry.Get(e.entityId)?.Has<GameFramework.World.Ownership>() == true)
                .Where(e => (e.position - entity.position).magnitude <= 10)
                .OrderBy(e => (e.position - entity.position).sqrMagnitude)
                .FirstOrDefault();

            if (target == null)
            {
                return;
            }

            Vector3 direction = target.position - entity.position;

            // Rotate
            float myFloat = 0;
            var angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            var smooth = Mathf.SmoothDampAngle(entity.rotation.y, angle, ref myFloat, 0.01f);
            entity.rotation = new Vector3(0, smooth, 0);

            if (direction.magnitude < 2)
            {
                //  Attack the player — 공격 어빌리티(id=3) 발동. 플레이어와 동일 경로라 데미지·연출 cue 자동.
                abilityActivator.TryActivate(entity.entityId, AttackAbilityId, Runner.Time.tick);
            }
            else
            {
                //  Move
                var velocity = direction.normalized * entity.GetEntityComponent<CharacterComponent>().masterData.Speed;
                entity.velocity = new Vector3(velocity.x, entity.velocity.y, velocity.z);
            }
        }

        public void Think(IEntity entity, double deltaTime)
        {
            if (entity is LOPEntity lopEntity)
            {
                Think(lopEntity, deltaTime);
            }
            else
            {
                throw new InvalidCastException("Entity is not of type LOPEntity");
            }
        }
    }
}
