using GameFramework;
using UnityEngine;

namespace LOP
{
    [EntityCreatorRegistration]
    public class CharacterCreator : IEntityCreator<LOPEntity, CharacterCreationData>
    {
        public LOPEntity Create(CharacterCreationData creationData)
        {
            GameObject root = new GameObject($"Character_{creationData.entityId}");
            GameObject visual = root.CreateChild("Visual");
            GameObject physics = root.CreateChild("Physics");

            LOPEntity entity = root.CreateChildWithComponent<LOPEntity>();
            entity.Initialize(creationData);

            EntityTypeComponent entityTypeComponent = entity.AddEntityComponent<EntityTypeComponent>();
            entityTypeComponent.Initialize(EntityType.Character);

            CharacterComponent characterComponent = entity.AddEntityComponent<CharacterComponent>();
            characterComponent.Initialize(creationData.characterCode);

            AppearanceComponent appearanceComponent = entity.AddEntityComponent<AppearanceComponent>();
            appearanceComponent.Initialize(creationData.visualId);

            PhysicsComponent physicsComponent = entity.AddEntityComponent<PhysicsComponent>();
            physicsComponent.Initialize(false, false);

            HealthComponent healthComponent = entity.AddEntityComponent<HealthComponent>();
            healthComponent.Initialize(creationData.maxHP, creationData.currentHP);

            ManaComponent manaComponent = entity.AddEntityComponent<ManaComponent>();
            manaComponent.Initialize(creationData.maxMP, creationData.currentMP);

            StatsComponent statsComponent = entity.AddEntityComponent<StatsComponent>();
            statsComponent.Initialize(creationData.characterCode);

            LevelComponent levelComponent = entity.AddEntityComponent<LevelComponent>();
            levelComponent.Initialize(creationData.level, creationData.currentExp);

            LOPEntityController controller = root.CreateChildWithComponent<LOPEntityController>();
            controller.SetEntity(entity);

            LOPEntityView view = root.CreateChildWithComponent<LOPEntityView>();
            view.SetEntity(entity);
            view.SetEntityController(controller);

            //  Temp.. must be modified later
            bool isPlayer = !string.IsNullOrEmpty(creationData.userId);
            if (isPlayer)
            {
                EntityInputComponent entityInputComponent = entity.gameObject.AddComponent<EntityInputComponent>();
            }
            else
            {
                LOPAIController aiController = root.CreateChildWithComponent<LOPAIController>();
                aiController.SetEntity(entity);
                aiController.SetBrain(new EnemyBrain());
            }

            return entity;
        }
    }
}
