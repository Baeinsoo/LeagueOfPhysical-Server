using GameFramework;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    public class RootLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<IMasterDataManager, LOPMasterDataManager>(Lifetime.Singleton);

            builder.Register<RoomDataStore>(Lifetime.Singleton)
                .As<IRoomDataStore>()
                .As<IDataStore>()
                .AsSelf();

            builder.Register<IDataUpdater, LOPDataUpdater>(Lifetime.Singleton);


            #region RegisterBuildCallback
            builder.RegisterBuildCallback(container =>
            {
                container.Resolve<IDataUpdater>();
            });
            #endregion
        }
    }
}
