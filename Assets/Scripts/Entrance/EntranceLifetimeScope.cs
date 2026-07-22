using GameFramework;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    public class EntranceLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<IEntranceComponent, ConfigureRoomComponent>(Lifetime.Transient);
            builder.Register<IEntranceComponent, LoadMasterDataComponent>(Lifetime.Transient);

            builder.RegisterBuildCallback(container =>
            {
                container.InjectSceneObjects(gameObject.scene);
            });
        }
    }
}
