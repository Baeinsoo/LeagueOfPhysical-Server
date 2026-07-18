using GameFramework;
using System;
using VContainer;

namespace LOP
{
    public class ItemCreationDataCreator : IEntityCreationDataCreator<LOPEntity>
    {
        [Inject]
        private GameFramework.World.EntityRegistry entityRegistry;

        public EntityType EntityType => EntityType.Item;

        public EntityCreationData Create(LOPEntity lopEntity)
        {
            var baseEntityCreationData = new BaseEntityCreationData
            {
                EntityId = lopEntity.entityId,
                Position = MapperConfig.mapper.Map<ProtoVector3>(lopEntity.position),
                Rotation = MapperConfig.mapper.Map<ProtoVector3>(lopEntity.rotation),
                Velocity = MapperConfig.mapper.Map<ProtoVector3>(lopEntity.velocity),
            };

            GameFramework.World.Entity worldEntity = entityRegistry.Get(lopEntity.entityId);
            global::ItemCreationData itemCreationData = new global::ItemCreationData
            {
                BaseEntityCreationData = baseEntityCreationData,
                ItemCode = worldEntity.Get<MasterDataRef>().Code,
                VisualId = worldEntity.Get<Appearance>().VisualId,
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
