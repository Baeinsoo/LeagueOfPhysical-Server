using GameFramework;

namespace LOP
{
    public interface IEntityCreationDataCreator
    {
        EntityType EntityType { get; }
        EntityCreationData Create(IEntity entity);
    }

    public interface IEntityCreationDataCreator<in TEntity> : IEntityCreationDataCreator
        where TEntity : IEntity
    {
        EntityCreationData Create(TEntity entity);
    }
}
