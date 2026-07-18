using GameFramework;
using System;
using System.Collections.Generic;

namespace LOP
{
    public class EntityCreationDataFactory : IEntityCreationDataFactory
    {
        private readonly Dictionary<EntityType, IEntityCreationDataCreator> creators
            = new Dictionary<EntityType, IEntityCreationDataCreator>();

        private readonly GameFramework.World.EntityRegistry entityRegistry;

        // creator는 DI 컨테이너가 생성·주입해 IEnumerable로 전달한다. (정적 캐시/Activator 없음 →
        // 스코프와 함께 생성·해제되어 룸 재입장 시 stale 참조가 생기지 않는다.)
        public EntityCreationDataFactory(IEnumerable<IEntityCreationDataCreator> creators, GameFramework.World.EntityRegistry entityRegistry)
        {
            this.entityRegistry = entityRegistry;
            foreach (var creator in creators.OrEmpty())
            {
                this.creators[creator.EntityType] = creator;
            }
        }

        public EntityCreationData Create(IEntity entity)
        {
            GameFramework.World.Entity worldEntity = entityRegistry.Get(entity.entityId);
            EntityKind kind = worldEntity?.Get<EntityKind>();
            if (kind == null)
            {
                throw new InvalidOperationException(
                    $"Entity '{entity.entityId}' does not have an EntityKind. Ensure the entity is properly initialized.");
            }

            if (creators.TryGetValue(kind.Kind, out var creator) == false)
            {
                throw new InvalidOperationException(
                    $"No registered creation-data creator found for entity kind '{kind.Kind}'.");
            }

            return creator.Create(entity);
        }
    }
}
