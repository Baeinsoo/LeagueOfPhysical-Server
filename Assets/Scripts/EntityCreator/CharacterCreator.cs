using GameFramework;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class CharacterCreator : IEntityCreator<LOPEntity, CharacterCreationData>
    {
        [Inject]
        private IObjectResolver objectResolver;

        [Inject]
        private GameFramework.World.EntityRegistry entityRegistry;

        public LOPEntity Create(CharacterCreationData creationData)
        {
            GameObject root = new GameObject($"Character_{creationData.entityId}");
            GameObject visual = root.CreateChild("Visual");
            GameObject physics = root.CreateChild("Physics");

            LOPEntity entity = root.CreateChildWithComponent<LOPEntity>();
            objectResolver.Inject(entity);
            entity.Initialize(creationData);

            EntityTypeComponent entityTypeComponent = entity.AddEntityComponent<EntityTypeComponent>();
            objectResolver.Inject(entityTypeComponent);
            entityTypeComponent.Initialize(EntityType.Character);

            CharacterComponent characterComponent = entity.AddEntityComponent<CharacterComponent>();
            objectResolver.Inject(characterComponent);
            characterComponent.Initialize(creationData.characterCode);

            AppearanceComponent appearanceComponent = entity.AddEntityComponent<AppearanceComponent>();
            objectResolver.Inject(appearanceComponent);
            appearanceComponent.Initialize(creationData.visualId);

            PhysicsComponent physicsComponent = entity.AddEntityComponent<PhysicsComponent>();
            objectResolver.Inject(physicsComponent);
            physicsComponent.Initialize(false, false);

            StatsComponent statsComponent = entity.AddEntityComponent<StatsComponent>();
            objectResolver.Inject(statsComponent);
            statsComponent.Initialize(creationData.characterCode);

            LevelComponent levelComponent = entity.AddEntityComponent<LevelComponent>();
            objectResolver.Inject(levelComponent);
            levelComponent.Initialize(creationData.level, creationData.currentExp);

            LOPEntityController controller = root.CreateChildWithComponent<LOPEntityController>();
            objectResolver.Inject(controller);
            controller.SetEntity(entity);

            LOPEntityView view = root.CreateChildWithComponent<LOPEntityView>();
            objectResolver.Inject(view);
            view.SetEntity(entity);

            bool isPlayer = !string.IsNullOrEmpty(creationData.userId);
            if (isPlayer)
            {
                PlayerComponent playerComponent = entity.AddEntityComponent<PlayerComponent>();
                objectResolver.Inject(playerComponent);
                playerComponent.Initialize(creationData.userId);

                EntityInputComponent entityInputComponent = entity.AddEntityComponent<EntityInputComponent>();
                objectResolver.Inject(entityInputComponent);
            }
            else
            {
                LOPAIController aiController = root.CreateChildWithComponent<LOPAIController>();
                objectResolver.Inject(aiController);
                aiController.SetEntity(entity);
                aiController.SetBrain(objectResolver.Resolve<EnemyBrain>());
            }

            // --- World Core (병렬·추가) — Slice 1: Health, 서버 Motion: Transform/Velocity ---
            var worldEntity = new GameFramework.World.Entity(creationData.entityId);
            var worldHealth = new GameFramework.World.Health(creationData.maxHP) { Current = creationData.currentHP };
            worldEntity.Add(worldHealth);
            worldEntity.Add(new GameFramework.World.Mana(creationData.maxMP) { Current = creationData.currentMP });
            worldEntity.Add(new GameFramework.World.Transform
            {
                Position = entity.position.ToNumerics(),
                Rotation = Quaternion.Euler(entity.rotation).ToNumerics(),
            });
            worldEntity.Add(new GameFramework.World.Velocity { Linear = entity.velocity.ToNumerics() });
            entityRegistry.Add(worldEntity);
            Debug.Log($"[World] Registered entity {worldEntity.Id} Health={worldHealth.Current}/{worldHealth.Max}");
            // --- end World Core ---

            return entity;
        }
    }
}
