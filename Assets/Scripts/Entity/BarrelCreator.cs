using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 굴러다니는 드럼통(서버). 유니티 물리 몸이 없다 — 위치·속도는 KnockbackBarrelSystem이 직접 쓰므로
    /// 키네마틱 몸이 드럼통을 벽으로 보지 않는다(걸어서는 밀지 않는 규칙).
    /// Simulated도 붙이지 않는다 — 월드 이동 시뮬이 아니라 End 시스템이 굴린다.
    /// </summary>
    public class BarrelCreator
    {
        private readonly GameFramework.World.EntityRegistry entityRegistry;

        public BarrelCreator(GameFramework.World.EntityRegistry entityRegistry)
        {
            this.entityRegistry = entityRegistry;
        }

        public void Create(BarrelCreationData creationData)
        {
            var worldEntity = new GameFramework.World.Entity(creationData.entityId);
            worldEntity.Add(new GameFramework.World.Transform
            {
                Position = creationData.position.ToNumerics(),
                Rotation = Quaternion.Euler(creationData.rotation).ToNumerics(),
            });
            worldEntity.Add(new GameFramework.World.Velocity { Linear = creationData.velocity.ToNumerics() });
            worldEntity.Add(new EntityKind(EntityType.Barrel));
            worldEntity.Add(new Appearance(creationData.visualId));
            worldEntity.Add(new KnockbackBarrel());

            entityRegistry.Add(worldEntity);
            Debug.Log($"[World] Registered barrel {worldEntity.Id}");
        }
    }
}
