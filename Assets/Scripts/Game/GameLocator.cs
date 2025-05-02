using GameFramework;
using UnityEngine;

namespace LOP
{
    public static class GameLocator
    {
        public static IGame game => SceneLifetimeScope.Resolve<IGame>();
    }
}
