using GameFramework;

namespace LOP
{
    public interface IEntityCreationDataFactory
    {
        EntityCreationData Create(LOPActor actor);
    }
}
