using GameFramework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    [EntityCreatorRegistration]
    public class LOPEntityCreator : IEntityCreator<LOPEntity, LOPEntityCreationData>
    {
        public LOPEntity Create(LOPEntityCreationData lopEntityCreationData)
        {
            GameObject root = new GameObject($"{nameof(LOPEntity)}_{lopEntityCreationData.entityId}");
            GameObject visual = root.CreateChild("Visual");
            GameObject physics = root.CreateChild("Physics");

            LOPEntity entity = root.CreateChildWithComponent<LOPEntity>();
            entity.Initialize(lopEntityCreationData);

            CharacterComponent characterComponent = entity.AddEntityComponent<CharacterComponent>();
            characterComponent.Initialize(lopEntityCreationData.characterCode);

            AppearanceComponent appearanceComponent = entity.AddEntityComponent<AppearanceComponent>();
            appearanceComponent.Initialize(lopEntityCreationData.visualId);

            PhysicsComponent physicsComponent = entity.AddEntityComponent<PhysicsComponent>();
            physicsComponent.Initialize();

            LOPEntityController controller = root.CreateChildWithComponent<LOPEntityController>();
            controller.SetEntity(entity);

            LOPEntityView view = root.CreateChildWithComponent<LOPEntityView>();
            view.SetEntity(entity);
            view.SetEntityController(controller);

            EntityInputComponent entityInputComponent = entity.gameObject.AddComponent<EntityInputComponent>();

            return entity;
        }
    }
}
