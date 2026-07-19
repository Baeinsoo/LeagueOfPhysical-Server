using GameFramework;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class CharacterCreator
    {
        [Inject] private GameFramework.World.EntityRegistry entityRegistry;
        [Inject] private AbilitySystem abilitySystem;
        [Inject] private LOP.MasterData.LOPMasterData md;

        public void Create(CharacterCreationData creationData)
        {
            var worldEntity = new GameFramework.World.Entity(creationData.entityId);
            worldEntity.Add(new GameFramework.World.Transform
            {
                Position = creationData.position.ToNumerics(),
                Rotation = Quaternion.Euler(creationData.rotation).ToNumerics(),
            });
            worldEntity.Add(new GameFramework.World.Velocity { Linear = creationData.velocity.ToNumerics() });
            worldEntity.Add(new EntityKind(EntityType.Character));
            worldEntity.Add(new MasterDataRef(creationData.characterCode));
            worldEntity.Add(new Appearance(creationData.visualId));

            var worldHealth = new GameFramework.World.Health(creationData.maxHP) { Current = creationData.currentHP };
            worldEntity.Add(worldHealth);
            worldEntity.Add(new GameFramework.World.Mana(creationData.maxMP) { Current = creationData.currentMP });
            worldEntity.Add(new GameFramework.World.Level { Value = creationData.level, Exp = creationData.currentExp, ExpToNext = 100 });
            var worldStats = new GameFramework.World.Stats();
            worldStats.BaseStats[(int)GameFramework.World.EntityStatType.Strength] = creationData.strength;
            worldStats.BaseStats[(int)GameFramework.World.EntityStatType.Dexterity] = creationData.dexterity;
            worldStats.BaseStats[(int)GameFramework.World.EntityStatType.Intelligence] = creationData.intelligence;
            worldStats.BaseStats[(int)GameFramework.World.EntityStatType.Vitality] = creationData.vitality;
            var characterMasterData = md.Tables.TbCharacter.Get(creationData.characterCode);
            worldStats.BaseStats[(int)GameFramework.World.EntityStatType.MoveSpeed] = characterMasterData.Speed;
            worldStats.BaseStats[(int)GameFramework.World.EntityStatType.JumpPower] = characterMasterData.JumpPower;
            worldEntity.Add(worldStats);

            bool isPlayer = !string.IsNullOrEmpty(creationData.userId);
            if (isPlayer)
            {
                worldEntity.Add(new GameFramework.World.Ownership(creationData.userId));
                worldEntity.Add(new InputBuffer());
            }
            worldEntity.Add(new Abilities());
            worldEntity.Add(new StatusEffects());
            worldEntity.Add(new MotionContributions());
            worldEntity.Add(new GameFramework.World.Simulated());   // 서버는 모든 캐릭터를 시뮬
            entityRegistry.Add(worldEntity);

            abilitySystem.Grant(worldEntity, 1);
            abilitySystem.Grant(worldEntity, 2);   // dash
            abilitySystem.Grant(worldEntity, 3);   // attack
            if (isPlayer)
            {
                abilitySystem.Grant(worldEntity, 4);
            }

            Debug.Log($"[World] Registered entity {worldEntity.Id} Health={worldHealth.Current}/{worldHealth.Max}");
        }
    }
}
