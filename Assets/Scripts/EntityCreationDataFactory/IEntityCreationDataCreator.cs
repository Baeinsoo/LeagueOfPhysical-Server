using UnityEngine;

namespace LOP
{
    public interface IEntityCreationDataCreator
    {
        EntityType EntityType { get; }
        EntityCreationData Create(LOPActor actor);
    }

    public interface IEntityCreationDataCreator<in TEntity> : IEntityCreationDataCreator
        where TEntity : MonoBehaviour
    {
        EntityCreationData Create(TEntity entity);
    }
}
