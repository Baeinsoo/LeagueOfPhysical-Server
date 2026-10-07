using GameFramework;
using GameFramework.Runner;
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
            //  판 도중 끊긴 사람 기록 — 방(LOPRoom)이 쓰고, 러너(최종 등수)·게임 규칙(기다리지 않기)이 읽는다.
            builder.Register<PlayerPresence>(Lifetime.Singleton);

            builder.Register<IGameFactory, LOPGameFactory>(Lifetime.Singleton);

            builder.RegisterBuildCallback(container =>
            {
                container.InjectSceneObjects(gameObject.scene);
            });
        }
    }
}
