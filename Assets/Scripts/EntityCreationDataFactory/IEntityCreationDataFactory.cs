using GameFramework;

namespace LOP
{
    public interface IEntityCreationDataFactory
    {
        EntityCreationData Create(IEntity entity);
    }
}
