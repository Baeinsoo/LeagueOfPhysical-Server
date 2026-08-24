using GameFramework;

namespace LOP
{
    public class CoinCreationDataCreator : IEntityCreationDataCreator
    {
        public EntityType EntityType => EntityType.Coin;

        public EntityCreationData Create(GameFramework.World.Entity worldEntity)
        {
            var baseEntityCreationData = new BaseEntityCreationData
            {
                EntityId = worldEntity.Id,
                Position = MapperConfig.mapper.Map<ProtoVector3>(GameFramework.World.EntityMotionExtensions.GetPosition(worldEntity)),
                Rotation = MapperConfig.mapper.Map<ProtoVector3>(GameFramework.World.EntityMotionExtensions.GetRotation(worldEntity)),
                Velocity = MapperConfig.mapper.Map<ProtoVector3>(GameFramework.World.EntityMotionExtensions.GetVelocity(worldEntity)),
            };

            global::CoinCreationData coinCreationData = new global::CoinCreationData
            {
                BaseEntityCreationData = baseEntityCreationData,
                VisualId = worldEntity.Get<Appearance>().VisualId,
            };

            return new EntityCreationData
            {
                CoinCreationData = coinCreationData
            };
        }
    }
}
