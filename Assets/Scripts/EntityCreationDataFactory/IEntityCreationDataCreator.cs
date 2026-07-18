using GameFramework;

namespace LOP
{
    public interface IEntityCreationDataCreator
    {
        EntityType EntityType { get; }
        EntityCreationData Create(LOPActor entity);
    }

    public interface IEntityCreationDataCreator<in TEntity> : IEntityCreationDataCreator
        where TEntity : IEntity
    {
        EntityCreationData Create(TEntity entity);
    }
}
