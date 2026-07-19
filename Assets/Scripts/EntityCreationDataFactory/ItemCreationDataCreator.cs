using GameFramework;

namespace LOP
{
    public class ItemCreationDataCreator : IEntityCreationDataCreator
    {
        public EntityType EntityType => EntityType.Item;

        public EntityCreationData Create(GameFramework.World.Entity worldEntity)
        {
            var baseEntityCreationData = new BaseEntityCreationData
            {
                EntityId = worldEntity.Id,
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
