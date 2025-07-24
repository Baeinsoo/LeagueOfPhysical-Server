using GameFramework;
using System;

namespace LOP
{
    [EntityCreationDataCreatorRegistration(EntityType.Character)]
    public class CharacterCreationDataCreator : IEntityCreationDataCreator<LOPEntity>
    {
        public EntityCreationData Create(LOPEntity lopEntity)
        {
            var baseEntityCreationData = new BaseEntityCreationData
            {
                EntityId = lopEntity.entityId,
                Position = MapperConfig.mapper.Map<ProtoVector3>(lopEntity.position),
                Rotation = MapperConfig.mapper.Map<ProtoVector3>(lopEntity.rotation),
                Velocity = MapperConfig.mapper.Map<ProtoVector3>(lopEntity.velocity),
            };

            global::CharacterCreationData characterCreationData = new global::CharacterCreationData
            {
                BaseEntityCreationData = baseEntityCreationData,
                CharacterCode = lopEntity.GetEntityComponent<CharacterComponent>().characterCode,
                VisualId = lopEntity.GetEntityComponent<AppearanceComponent>().visualId,

                MaxHP = lopEntity.GetEntityComponent<HealthComponent>().maxHP,
                CurrentHP = lopEntity.GetEntityComponent<HealthComponent>().currentHP,
                MaxMP = lopEntity.GetEntityComponent<ManaComponent>().maxMP,
                CurrentMP = lopEntity.GetEntityComponent<ManaComponent>().currentMP,
                Level = lopEntity.GetEntityComponent<LevelComponent>().level,
                CurrentExp = lopEntity.GetEntityComponent<LevelComponent>().currentExp,
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
    }
}
