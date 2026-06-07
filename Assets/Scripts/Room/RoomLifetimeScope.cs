using GameFramework;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    public class RoomLifetimeScope : SceneLifetimeScope
    {
        [SerializeField] private LOPRoom room;
        [SerializeField] private LOPNetworkManager networkManager;

        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);

            builder.RegisterComponent(room).AsImplementedInterfaces();
            builder.RegisterComponent(networkManager);

            builder.Register<ISessionManager, SessionManager>(Lifetime.Singleton);

            builder.Register<IGameFactory, LOPGameFactory>(Lifetime.Singleton);
        }
    }
}
