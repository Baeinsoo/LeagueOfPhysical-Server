using GameFramework;
using System;
using VContainer;

namespace LOP
{
    public class CharacterCreationDataCreator : IEntityCreationDataCreator<LOPEntity>
    {
        [Inject]
        private GameFramework.World.EntityRegistry entityRegistry;

        public EntityType EntityType => EntityType.Character;

        public EntityCreationData Create(LOPEntity lopEntity)
        {
            var baseEntityCreationData = new BaseEntityCreationData
            {
                EntityId = lopEntity.entityId,
                Position = MapperConfig.mapper.Map<ProtoVector3>(lopEntity.position),
                Rotation = MapperConfig.mapper.Map<ProtoVector3>(lopEntity.rotation),
                Velocity = MapperConfig.mapper.Map<ProtoVector3>(lopEntity.velocity),
            };

            // HP는 World.Health(코어, Slice 1b)에서 읽는다. MP/Level/Exp는 각자 이행 전까지 legacy 컴포넌트 유지.
            GameFramework.World.Entity worldEntity = entityRegistry.Get(lopEntity.entityId);
            GameFramework.World.Health health = worldEntity?.Get<GameFramework.World.Health>();
            if (health == null)
            {
                UnityEngine.Debug.LogWarning($"[World] CharacterCreationData: Health not found for entity {lopEntity.entityId}");
            }

            global::CharacterCreationData characterCreationData = new global::CharacterCreationData
            {
                BaseEntityCreationData = baseEntityCreationData,
                CharacterCode = lopEntity.GetEntityComponent<CharacterComponent>().characterCode,
                VisualId = lopEntity.GetEntityComponent<AppearanceComponent>().visualId,

                MaxHP = health?.Max ?? 0,
                CurrentHP = health?.Current ?? 0,
                MaxMP = lopEntity.GetEntityComponent<ManaComponent>().maxMP,
                CurrentMP = lopEntity.GetEntityComponent<ManaComponent>().currentMP,
                Level = lopEntity.GetEntityComponent<LevelComponent>().level,
                CurrentExp = lopEntity.GetEntityComponent<LevelComponent>().currentExp,
            };

            return new EntityCreationData
            {
                CharacterCreationData = characterCreationData
            };
        }

        public EntityCreationData Create(IEntity entity)
        {
            if (entity is LOPEntity lopEntity)
            {
                return Create(lopEntity);
            }

            throw new ArgumentException("Entity must be of type LOPEntity");
        }
    }
}
