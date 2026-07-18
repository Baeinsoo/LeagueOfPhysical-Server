using GameFramework;

namespace LOP
{
    public interface IBrain
    {
    }

    public interface IBrain<T> : IBrain where T : IEntity
    {
        void Think(T entity, double deltaTime);
    }
}
