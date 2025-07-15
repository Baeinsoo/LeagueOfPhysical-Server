using GameFramework;
using System;

namespace LOP
{
    [EntityCreationDataCreatorRegistration(EntityType.Item)]
    public class ItemCreationDataCreator : IEntityCreationDataCreator<LOPEntity>
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

            global::ItemCreationData itemCreationData = new global::ItemCreationData
            {
                BaseEntityCreationData = baseEntityCreationData,
                ItemCode = lopEntity.GetEntityComponent<ItemComponent>().itemCode,
                VisualId = lopEntity.GetEntityComponent<AppearanceComponent>().visualId,
            };

            return new EntityCreationData
            {
                ItemCreationData = itemCreationData
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
