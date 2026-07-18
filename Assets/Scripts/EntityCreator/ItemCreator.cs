using GameFramework;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class ItemCreator : IEntityCreator<LOPActor, ItemCreationData>
    {
        [Inject] private IObjectResolver objectResolver;
        [Inject] private GameFramework.World.EntityRegistry entityRegistry;

        public LOPActor Create(ItemCreationData creationData)
        {
            var worldEntity = new GameFramework.World.Entity(creationData.entityId);
            worldEntity.Add(new GameFramework.World.Transform
            {
                Position = creationData.position.ToNumerics(),
                Rotation = Quaternion.Euler(creationData.rotation).ToNumerics(),
            });
            worldEntity.Add(new GameFramework.World.Velocity { Linear = creationData.velocity.ToNumerics() });
            worldEntity.Add(new EntityKind(EntityType.Item));
            worldEntity.Add(new MasterDataRef(creationData.itemCode));
            worldEntity.Add(new Appearance(creationData.visualId));
            entityRegistry.Add(worldEntity);

            GameObject root = new GameObject($"Actor_{creationData.entityId}");
            LOPActor actor = root.AddComponent<LOPActor>();
            objectResolver.Inject(actor);
            actor.Initialize(creationData);
            return actor;
        }
    }
}
