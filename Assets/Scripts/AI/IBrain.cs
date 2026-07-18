using UnityEngine;

namespace LOP
{
    public interface IBrain
    {
    }

    public interface IBrain<T> : IBrain where T : MonoBehaviour
    {
        void Think(T entity, double deltaTime);
    }
}
