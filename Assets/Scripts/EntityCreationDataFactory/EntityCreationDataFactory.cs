using GameFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LOP
{
    public static class EntityCreationDataFactory
    {
        private static readonly Dictionary<Type, IEntityCreationDataCreator> entityCreationDataCreators;

        static EntityCreationDataFactory()
        {
            entityCreationDataCreators = new Dictionary<Type, IEntityCreationDataCreator>();

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

                var genericInterface = type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEntityCreationDataCreator<>));
                if (genericInterface == null || type.IsAbstract || type.IsInterface)
                {
                    continue;
                }

                if (Activator.CreateInstance(type) is IEntityCreationDataCreator instance)
                {
                    var typeArguments = genericInterface.GetGenericArguments();
                    var key = typeArguments[0];

                    entityCreationDataCreators[key] = instance;
                    Debug.Log($"Registered Creator: {type.Name} for {key.Name}");
                }
            }
        }

        public static EntityCreationData Create(IEntity entity)
        {
            if (entityCreationDataCreators.TryGetValue(entity.GetType(), out var creator) == false)
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
