using GameFramework;

namespace LOP
{
    public interface IBrain
    {
        void Think(IEntity entity, double deltaTime);
    }

    public interface IBrain<T> : IBrain where T : IEntity
    {
        void Think(T entity, double deltaTime);
    }
}
