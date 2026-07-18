using UnityEngine;

namespace LOP
{
    public interface IEntityCreationDataCreator
    {
        EntityType EntityType { get; }
        EntityCreationData Create(LOPActor entity);
    }

    public interface IEntityCreationDataCreator<in TEntity> : IEntityCreationDataCreator
        where TEntity : MonoBehaviour
    {
        EntityCreationData Create(TEntity entity);
    }
}
