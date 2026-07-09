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

        [Inject]
        private AbilitySystem abilitySystem;

        public LOPEntity Create(CharacterCreationData creationData)
        {
            GameObject root = new GameObject($"Character_{creationData.entityId}");
            GameObject visual = root.CreateChild("Visual");
            GameObject physics = root.CreateChild("Physics");

            var worldEntity = new GameFramework.World.Entity(creationData.entityId);
            worldEntity.Add(new GameFramework.World.Transform
            {
                Position = creationData.position.ToNumerics(),
                Rotation = Quaternion.Euler(creationData.rotation).ToNumerics(),
            });
            worldEntity.Add(new GameFramework.World.Velocity { Linear = creationData.velocity.ToNumerics() });

            LOPEntity entity = root.CreateChildWithComponent<LOPEntity>();
            objectResolver.Inject(entity);
            entity.LinkWorldMotion(
                worldEntity.Get<GameFramework.World.Transform>(),
                worldEntity.Get<GameFramework.World.Velocity>());
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
            physicsComponent.Initialize(true, false);   // kinematic, non-trigger — 우리가 직접 이동시킴

            LOPEntityController controller = root.CreateChildWithComponent<LOPEntityController>();
            objectResolver.Inject(controller);
            controller.SetEntity(entity);

            LOPEntityView view = root.CreateChildWithComponent<LOPEntityView>();
            objectResolver.Inject(view);
            view.SetEntity(entity);

            bool isPlayer = !string.IsNullOrEmpty(creationData.userId);
            if (isPlayer == false)
            {
                LOPAIController aiController = root.CreateChildWithComponent<LOPAIController>();
                objectResolver.Inject(aiController);
                aiController.SetEntity(entity);
                aiController.SetBrain(objectResolver.Resolve<EnemyBrain>());
            }

            // --- World Core (병렬·추가) — Health/Mana/Level/Stats/Ownership/Abilities. Transform/Velocity는 위에서 생성(파사드 백킹). ---
            var worldHealth = new GameFramework.World.Health(creationData.maxHP) { Current = creationData.currentHP };
            worldEntity.Add(worldHealth);
            worldEntity.Add(new GameFramework.World.Mana(creationData.maxMP) { Current = creationData.currentMP });
            worldEntity.Add(new GameFramework.World.Level { Value = creationData.level, Exp = creationData.currentExp, ExpToNext = 100 });
            var worldStats = new GameFramework.World.Stats();
            worldStats.BaseStats[(int)GameFramework.World.EntityStatType.Strength] = creationData.strength;
            worldStats.BaseStats[(int)GameFramework.World.EntityStatType.Dexterity] = creationData.dexterity;
            worldStats.BaseStats[(int)GameFramework.World.EntityStatType.Intelligence] = creationData.intelligence;
            worldStats.BaseStats[(int)GameFramework.World.EntityStatType.Vitality] = creationData.vitality;
            worldStats.BaseStats[(int)GameFramework.World.EntityStatType.MoveSpeed] = characterComponent.masterData.Speed;
            worldStats.BaseStats[(int)GameFramework.World.EntityStatType.JumpPower] = characterComponent.masterData.JumpPower;
            worldEntity.Add(worldStats);
            if (isPlayer)
            {
                worldEntity.Add(new GameFramework.World.Ownership(creationData.userId));
                // 입력으로 조종되는 엔티티(플레이어)만 — 수신 커맨드를 틱별 버퍼링하고 MovementSystem이 읽는다. AI는 미부여.
                worldEntity.Add(new InputBuffer());
            }
            worldEntity.Add(new Abilities());
            worldEntity.Add(new StatusEffects());
            worldEntity.Add(new MotionContributions());
            entityRegistry.Add(worldEntity);

            // 3d: 헤이스트 어빌리티 부여(발동은 입력 트리거 — AbilityActivator). TEMP: 전체 부여, 캐릭터별 셋은 후속.
            abilitySystem.Grant(worldEntity, 1);
            abilitySystem.Grant(worldEntity, 2);   // dash (TEMP 전체 부여)
            abilitySystem.Grant(worldEntity, 3);   // attack (TEMP 전체 부여)

            Debug.Log($"[World] Registered entity {worldEntity.Id} Health={worldHealth.Current}/{worldHealth.Max}");
            // --- end World Core ---

            return entity;
        }
    }
}
