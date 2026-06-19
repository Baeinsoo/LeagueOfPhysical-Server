using GameFramework;
using System;
using VContainer;

namespace LOP
{
    public class CharacterCreationDataCreator : IEntityCreationDataCreator<LOPEntity>
    {
        [Inject]
        private GameFramework.World.EntityRegistry entityRegistry;

        public EntityType EntityType => EntityType.Character;

        public EntityCreationData Create(LOPEntity lopEntity)
        {
            var baseEntityCreationData = new BaseEntityCreationData
            {
                EntityId = lopEntity.entityId,
                Position = MapperConfig.mapper.Map<ProtoVector3>(lopEntity.position),
                Rotation = MapperConfig.mapper.Map<ProtoVector3>(lopEntity.rotation),
                Velocity = MapperConfig.mapper.Map<ProtoVector3>(lopEntity.velocity),
            };

            // HP/MP/Level/Exp는 World 코어에서 읽는다.
            GameFramework.World.Entity worldEntity = entityRegistry.Get(lopEntity.entityId);
            GameFramework.World.Health health = worldEntity?.Get<GameFramework.World.Health>();
            if (health == null)
            {
                UnityEngine.Debug.LogWarning($"[World] CharacterCreationData: Health not found for entity {lopEntity.entityId}");
            }

            GameFramework.World.Mana mana = worldEntity?.Get<GameFramework.World.Mana>();
            if (mana == null)
            {
                UnityEngine.Debug.LogWarning($"[World] CharacterCreationData: Mana not found for entity {lopEntity.entityId}");
            }

            GameFramework.World.Level level = worldEntity?.Get<GameFramework.World.Level>();
            if (level == null)
            {
                UnityEngine.Debug.LogWarning($"[World] CharacterCreationData: Level not found for entity {lopEntity.entityId}");
            }

            GameFramework.World.Stats stats = worldEntity?.Get<GameFramework.World.Stats>();
            if (stats == null)
            {
                UnityEngine.Debug.LogWarning($"[World] CharacterCreationData: Stats not found for entity {lopEntity.entityId}");
            }

            global::CharacterCreationData characterCreationData = new global::CharacterCreationData
            {
                BaseEntityCreationData = baseEntityCreationData,
                CharacterCode = lopEntity.GetEntityComponent<CharacterComponent>().characterCode,
                VisualId = lopEntity.GetEntityComponent<AppearanceComponent>().visualId,

                MaxHP = health?.Max ?? 0,
                CurrentHP = health?.Current ?? 0,
                MaxMP = mana?.Max ?? 0,
                CurrentMP = mana?.Current ?? 0,
                Level = level?.Value ?? 0,
                CurrentExp = level?.Exp ?? 0,
                Strength = BaseStatInt(stats, GameFramework.World.EntityStatType.Strength),
                Dexterity = BaseStatInt(stats, GameFramework.World.EntityStatType.Dexterity),
                Intelligence = BaseStatInt(stats, GameFramework.World.EntityStatType.Intelligence),
                Vitality = BaseStatInt(stats, GameFramework.World.EntityStatType.Vitality),
            };

            return new EntityCreationData
            {
                CharacterCreationData = characterCreationData
            };
        }

        public EntityCreationData Create(IEntity entity)
        {
            if (entity is LOPEntity lopEntity)
            {
                return Create(lopEntity);
            }

            throw new ArgumentException("Entity must be of type LOPEntity");
        }

        private static int BaseStatInt(GameFramework.World.Stats stats, GameFramework.World.EntityStatType statType)
        {
            return stats != null && stats.BaseStats.TryGetValue((int)statType, out var v) ? (int)v : 0;
        }
    }
}
