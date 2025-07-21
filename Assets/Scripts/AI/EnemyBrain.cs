using GameFramework;
using System;
using UnityEngine;

namespace LOP
{
    public class EnemyBrain : IBrain<LOPEntity>
    {
        public void Think(LOPEntity entity, double deltaTime)
        {
            //  Find the player
            LOPEntity target = null;
            var entities = GameEngine.current.entityManager.GetEntities<LOPEntity>();
            foreach (var otherEntity in entities)
            {
                if (otherEntity.TryGetComponent<EntityInputComponent>(out var inputComponent))
                {
                    target = otherEntity;
                    break;
                }
            }

            Vector3 direction = target.position -entity.position;
            
            if (direction.magnitude < 10f)
            {
                // Rotate
                float myFloat = 0;
                var angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                var smooth = Mathf.SmoothDampAngle(entity.rotation.y, angle, ref myFloat, 0.01f);
                entity.rotation = new Vector3(0, smooth, 0);

                if (direction.magnitude < 2f)
                {
                    //  Attack the player
                    SceneLifetimeScope.Resolve<IActionManager>().TryExecuteAction(entity, "attack_001");
                }
                else
                {
                    //  Move
                    var velocity = direction.normalized * entity.GetEntityComponent<CharacterComponent>().masterData.Speed;
                    entity.velocity = new Vector3(velocity.x, entity.velocity.y, velocity.z);
                }
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
