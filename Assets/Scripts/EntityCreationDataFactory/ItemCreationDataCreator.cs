using GameFramework;
using VContainer;

namespace LOP
{
    public class ItemCreationDataCreator : IEntityCreationDataCreator<LOPActor>
    {
        [Inject]
        private GameFramework.World.EntityRegistry entityRegistry;

        public EntityType EntityType => EntityType.Item;

        public EntityCreationData Create(LOPActor lopEntity)
        {
            GameFramework.World.Entity worldEntity = entityRegistry.Get(lopEntity.entityId);

            var baseEntityCreationData = new BaseEntityCreationData
            {
                EntityId = lopEntity.entityId,
                Position = MapperConfig.mapper.Map<ProtoVector3>(GameFramework.World.EntityMotionExtensions.GetPosition(worldEntity)),
                Rotation = MapperConfig.mapper.Map<ProtoVector3>(GameFramework.World.EntityMotionExtensions.GetRotation(worldEntity)),
                Velocity = MapperConfig.mapper.Map<ProtoVector3>(GameFramework.World.EntityMotionExtensions.GetVelocity(worldEntity)),
            };

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
    }
}
