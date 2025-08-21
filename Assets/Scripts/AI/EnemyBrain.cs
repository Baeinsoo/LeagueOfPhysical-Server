using GameFramework;
using System;
using UnityEngine;

namespace LOP
{
    public class EnemyBrain : IBrain<LOPEntity>
    {
        private IActionManager actionManager;

        public EnemyBrain(IActionManager actionManager)
        {
            this.actionManager = actionManager;
        }

        public void Think(LOPEntity entity, double deltaTime)
        {
            //  Find the player
            LOPEntity target = null;
            var entities = GameEngine.current.entityManager.GetEntities<LOPEntity>();
            foreach (var otherEntity in entities)
            {
                if (otherEntity.HasEntityComponent<PlayerComponent>())
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
                    switch (entity.GetEntityComponent<AppearanceComponent>().visualId)
                    {
                        case "Assets/Art/Characters/Knight/Knight.prefab":
                            actionManager.TryStartAction(entity, "knight_attack_001");
                            break;
                        case "Assets/Art/Characters/Archer/Archer.prefab":
                            actionManager.TryStartAction(entity, "archer_attack_001");
                            break;
                        case "Assets/Art/Characters/Necromancer/Necromancer.prefab":
                            actionManager.TryStartAction(entity, "necromancer_attack_001");
                            break;
                    }
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
