using GameFramework;

namespace LOP
{
    public interface IEntityCreationDataFactory
    {
        EntityCreationData Create(GameFramework.World.Entity worldEntity);
    }
}
