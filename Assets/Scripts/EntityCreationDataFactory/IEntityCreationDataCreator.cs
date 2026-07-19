namespace LOP
{
    public interface IEntityCreationDataCreator
    {
        EntityType EntityType { get; }
        EntityCreationData Create(GameFramework.World.Entity worldEntity);
    }
}
