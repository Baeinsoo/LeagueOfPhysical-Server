using GameFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LOP
{
    public static class EntityCreationDataFactory
    {
        private static readonly Dictionary<object, IEntityCreationDataCreator> entityCreationDataCreators;

        static EntityCreationDataFactory()
        {
            entityCreationDataCreators = new Dictionary<object, IEntityCreationDataCreator>();

            RegisterCreators();
        }

        private static void RegisterCreators()
        {
            var types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes());
            foreach (var type in types.OrEmpty())
            {
                var attribute = (EntityCreationDataCreatorRegistrationAttribute)Attribute.GetCustomAttribute(type, typeof(EntityCreationDataCreatorRegistrationAttribute));
                if (attribute == null || attribute.value == false)
                {
                    continue;
                }

                if (Activator.CreateInstance(type) is IEntityCreationDataCreator instance)
                {
                    entityCreationDataCreators[attribute.type] = instance;
                    Debug.Log($"Registered Creator: {type.Name} for {attribute.type}");
                }
            }
        }

        public static EntityCreationData Create(IEntity entity)
        {
            if (entity.TryGetEntityComponent<EntityTypeComponent>(out var entityTypeComponent) == false)
            {
                throw new InvalidOperationException(
                    $"Entity '{entity.entityId}' does not have an EntityTypeComponent. " +
                    "Ensure the entity is properly initialized with its components."
                );
            }

            if (entityCreationDataCreators.TryGetValue(entityTypeComponent.entityType, out var creator) == false)
            {
                throw new InvalidOperationException(
                    $"No registered creator found for entity type '{entity.GetType().Name}' " +
                    "Ensure the appropriate IEntityCreationDataCreator is registered."
                );
            }

            return creator.Create(entity);
        }
    }
}
