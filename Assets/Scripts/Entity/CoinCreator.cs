using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 동전(서버). 다이나믹 몸이라 유니티 물리가 굴리고, 그 결과를
    /// PhysicsSimulationSystem이 World로 되읽어 스냅샷에 실린다.
    /// Simulated을 붙이지 않는다 — 우리 시뮬이 굴리는 것이 아니다.
    /// </summary>
    public class CoinCreator
    {
        private readonly GameFramework.World.EntityRegistry entityRegistry;

        public CoinCreator(GameFramework.World.EntityRegistry entityRegistry)
        {
            this.entityRegistry = entityRegistry;
        }

        public void Create(CoinCreationData creationData)
        {
            var worldEntity = new GameFramework.World.Entity(creationData.entityId);
            worldEntity.Add(new GameFramework.World.Transform
            {
                Position = creationData.position.ToNumerics(),
                Rotation = Quaternion.Euler(creationData.rotation).ToNumerics(),
            });
            worldEntity.Add(new GameFramework.World.Velocity { Linear = creationData.velocity.ToNumerics() });
            worldEntity.Add(new EntityKind(EntityType.Coin));
            worldEntity.Add(new Appearance(creationData.visualId));
            worldEntity.Add(new GameFramework.World.DiscShape(0.15f, 0.04f));
            //  회전을 풀어야 뒤집힌다. 다이나믹이라 PhysX가 진실원본이 된다.
            worldEntity.Add(new GameFramework.World.PhysicsConfig(
                GameFramework.World.BodyKind.Dynamic, freezeRotation: false, isTrigger: false));

            entityRegistry.Add(worldEntity);
            Debug.Log($"[World] Registered coin {worldEntity.Id}");
        }
    }
}
