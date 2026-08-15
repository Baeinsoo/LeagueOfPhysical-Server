using GameFramework;

namespace LOP
{
    public class CharacterCreationDataCreator : IEntityCreationDataCreator
    {
        public EntityType EntityType => EntityType.Character;

        public EntityCreationData Create(GameFramework.World.Entity worldEntity)
        {
            var baseEntityCreationData = new BaseEntityCreationData
            {
                EntityId = worldEntity.Id,
                Position = MapperConfig.mapper.Map<ProtoVector3>(GameFramework.World.EntityMotionExtensions.GetPosition(worldEntity)),
                Rotation = MapperConfig.mapper.Map<ProtoVector3>(GameFramework.World.EntityMotionExtensions.GetRotation(worldEntity)),
                Velocity = MapperConfig.mapper.Map<ProtoVector3>(GameFramework.World.EntityMotionExtensions.GetVelocity(worldEntity)),
            };

            // 마스터데이터로 스탯을 받는 몸만 체력·마나·레벨·스탯을 갖는다 — 새에겐 없는 게 정상이다.
            bool masterDataBacked = worldEntity.Has<MasterDataRef>();

            // HP/MP/Level/Exp는 World 코어에서 읽는다.
            GameFramework.World.Health health = worldEntity?.Get<GameFramework.World.Health>();
            if (masterDataBacked && health == null)
            {
                UnityEngine.Debug.LogWarning($"[World] CharacterCreationData: Health not found for entity {worldEntity.Id}");
            }

            GameFramework.World.Mana mana = worldEntity?.Get<GameFramework.World.Mana>();
            if (masterDataBacked && mana == null)
            {
                UnityEngine.Debug.LogWarning($"[World] CharacterCreationData: Mana not found for entity {worldEntity.Id}");
            }

            GameFramework.World.Level level = worldEntity?.Get<GameFramework.World.Level>();
            if (masterDataBacked && level == null)
            {
                UnityEngine.Debug.LogWarning($"[World] CharacterCreationData: Level not found for entity {worldEntity.Id}");
            }

            GameFramework.World.Stats stats = worldEntity?.Get<GameFramework.World.Stats>();
            if (masterDataBacked && stats == null)
            {
                UnityEngine.Debug.LogWarning($"[World] CharacterCreationData: Stats not found for entity {worldEntity.Id}");
            }

            global::CharacterCreationData characterCreationData = new global::CharacterCreationData
            {
                BaseEntityCreationData = baseEntityCreationData,
                // 마스터데이터로 스탯을 받지 않는 몸(Flappy의 새)은 이 참조가 아예 없다.
                CharacterCode = worldEntity.Get<MasterDataRef>()?.Code ?? "",
                VisualId = worldEntity.Get<Appearance>().VisualId,

                MaxHP = health?.Max ?? 0,
                CurrentHP = health?.Current ?? 0,
                MaxMP = mana?.Max ?? 0,
                CurrentMP = mana?.Current ?? 0,
                Level = level?.Value ?? 0,
                CurrentExp = level?.Exp ?? 0,
                Strength = BaseStatInt(stats, GameFramework.World.EntityStatType.Strength),
                Dexterity = BaseStatInt(stats, GameFramework.World.EntityStatType.Dexterity),
                Intelligence = BaseStatInt(stats, GameFramework.World.EntityStatType.Intelligence),
                Vitality = BaseStatInt(stats, GameFramework.World.EntityStatType.Vitality),
            };

            return new EntityCreationData
            {
                CharacterCreationData = characterCreationData
            };
        }

        private static int BaseStatInt(GameFramework.World.Stats stats, GameFramework.World.EntityStatType statType)
        {
            return stats != null && stats.BaseStats.TryGetValue((int)statType, out var v) ? (int)v : 0;
        }
    }
}
