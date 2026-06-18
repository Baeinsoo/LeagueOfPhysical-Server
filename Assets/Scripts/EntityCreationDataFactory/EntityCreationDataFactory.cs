using GameFramework;
using System;
using System.Collections.Generic;

namespace LOP
{
    public class EntityCreationDataFactory : IEntityCreationDataFactory
    {
        private readonly Dictionary<EntityType, IEntityCreationDataCreator> creators
            = new Dictionary<EntityType, IEntityCreationDataCreator>();

        // creator는 DI 컨테이너가 생성·주입해 IEnumerable로 전달한다. (정적 캐시/Activator 없음 →
        // 스코프와 함께 생성·해제되어 룸 재입장 시 stale 참조가 생기지 않는다.)
        public EntityCreationDataFactory(IEnumerable<IEntityCreationDataCreator> creators)
        {
            foreach (var creator in creators.OrEmpty())
            {
                this.creators[creator.EntityType] = creator;
            }
        }

        public EntityCreationData Create(IEntity entity)
        {
            if (entity.TryGetEntityComponent<EntityTypeComponent>(out var entityTypeComponent) == false)
            {
                throw new InvalidOperationException(
                    $"Entity '{entity.entityId}' does not have an EntityTypeComponent. " +
                    "Ensure the entity is properly initialized with its components."
                );
            }

            if (creators.TryGetValue(entityTypeComponent.entityType, out var creator) == false)
            {
                throw new InvalidOperationException(
                    $"No registered creation-data creator found for entity type '{entityTypeComponent.entityType}'. " +
                    "Ensure the appropriate IEntityCreationDataCreator is registered in the DI container."
                );
            }

            return creator.Create(entity);
        }
    }
}
