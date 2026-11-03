using GameFramework;

namespace LOP
{
    /// <summary>
    /// 드럼통은 따로 와이어 메시지를 두지 않고 동전과 같은 CoinCreationData에 실어 보낸다.
    /// 클라는 visual_id가 <see cref="KnockbackBarrel.VisualId"/>이면 드럼통으로 만든다.
    /// </summary>
    public class BarrelCreationDataCreator : IEntityCreationDataCreator
    {
        public EntityType EntityType => EntityType.Barrel;

        public EntityCreationData Create(GameFramework.World.Entity worldEntity)
        {
            var baseEntityCreationData = new BaseEntityCreationData
            {
                EntityId = worldEntity.Id,
                Position = MapperConfig.mapper.Map<ProtoVector3>(GameFramework.World.EntityMotionExtensions.GetPosition(worldEntity)),
                Rotation = MapperConfig.mapper.Map<ProtoVector3>(GameFramework.World.EntityMotionExtensions.GetRotation(worldEntity)),
                Velocity = MapperConfig.mapper.Map<ProtoVector3>(GameFramework.World.EntityMotionExtensions.GetVelocity(worldEntity)),
            };

            return new EntityCreationData
            {
                CoinCreationData = new global::CoinCreationData
                {
                    BaseEntityCreationData = baseEntityCreationData,
                    VisualId = KnockbackBarrel.VisualId,
                }
            };
        }
    }
}
