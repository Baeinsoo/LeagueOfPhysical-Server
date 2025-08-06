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
        [SerializeField] private LOPGame game;
        [SerializeField] private LOPGameEngine gameEngine;

        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);

            builder.RegisterComponent(room).AsImplementedInterfaces();
            builder.RegisterComponent(networkManager);
            builder.RegisterComponent(game).As<IGame>();
            builder.RegisterComponent(gameEngine).As<IGameEngine>();

            builder.Register<IRoomMessageHandler, GameMessageHandler>(Lifetime.Transient);

            builder.Register<IGameMessageHandler, GameEntityMessageHandler>(Lifetime.Transient);
            builder.Register<IGameMessageHandler, GameInputMessageHandler>(Lifetime.Transient);

            builder.Register<ISessionManager, SessionManager>(Lifetime.Singleton);

            builder.Register<IActionManager, LOPActionManager>(Lifetime.Singleton);
            builder.Register<IMovementManager, LOPMovementManager>(Lifetime.Singleton);

            builder.Register<ICombatSystem, LOPCombatSystem>(Lifetime.Singleton);

            #region RegisterBuildCallback
            builder.RegisterBuildCallback(container =>
            {
            });
            #endregion
        }
    }
}
