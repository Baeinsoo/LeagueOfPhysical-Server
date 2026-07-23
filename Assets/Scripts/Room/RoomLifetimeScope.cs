using GameFramework;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    public class RoomLifetimeScope : LifetimeScope
    {
        [SerializeField] private LOPRoom room;
        [SerializeField] private LOPNetworkManager networkManager;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponent(room).AsImplementedInterfaces();
            builder.RegisterComponent(networkManager);

            builder.Register<ISessionManager, SessionManager>(Lifetime.Singleton);

            builder.Register<IGameFactory, LOPGameFactory>(Lifetime.Singleton);

            builder.RegisterBuildCallback(container =>
            {
                container.InjectSceneObjects(gameObject.scene);
            });
        }
    }
}
