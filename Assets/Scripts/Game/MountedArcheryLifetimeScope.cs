using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>MountedArchery 덩어리(서버) — 말 위에서 달리며 쏘는 기수들.</summary>
    public class MountedArcheryLifetimeScope : GameLifetimeScope
    {
        // 50Hz.
        private const float TickInterval = 0.02f;

        protected override void ConfigureGame(IContainerBuilder builder)
        {
            builder.Register<MountedArcheryConfigProvider>(Lifetime.Singleton);
            builder.Register<MountedArcheryConfig>(
                c => c.Resolve<MountedArcheryConfigProvider>().Get(), Lifetime.Singleton);

            builder.Register(c => new MountSystem(c.Resolve<MountedArcheryConfig>().Mount), Lifetime.Singleton);
            builder.Register(c =>
            {
                var cfg = c.Resolve<MountedArcheryConfig>();
                return new ArcheryAimSystem(cfg.ToBowConfig(), cfg.RiderEyeHeight, turnBodyToAim: false);
            }, Lifetime.Singleton);

            builder.Register<MountedArcheryWorld>(c => new MountedArcheryWorld(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<GameFramework.World.WorldEventBuffer>(),
                c.Resolve<ArcheryAimSystem>(),
                c.Resolve<MountSystem>(),
                c.Resolve<KinematicMoveSystem>(),
                c.Resolve<GameFramework.World.IMotionBridge>(),
                TickInterval), Lifetime.Singleton)
                .As<GameFramework.World.IWorld>().AsSelf();

            builder.Register<ICharacterCreator, MountedArcheryPlayerCreator>(Lifetime.Singleton);
            builder.Register<MountedArcheryMatchState>(Lifetime.Singleton);
            builder.Register<IGameRuleSystem, MountedArcheryRuleSystem>(Lifetime.Singleton);

            builder.Register(c => new MotionHistorySystem(c.Resolve<GameFramework.World.IWorld>()), Lifetime.Singleton);
            builder.Register(c => new MountedArcheryHitSystem(
                c.Resolve<MountedArcheryWorld>(),
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<MountedArcheryMatchState>(),
                c.Resolve<MountedArcheryConfig>(),
                c.Resolve<GameFramework.World.HealthSystem>(),
                c.Resolve<GameFramework.World.WorldEventBuffer>(),
                c.Resolve<EntitySpawner>().Despawn,
                TerrainAlong,
                TickInterval), Lifetime.Singleton);

            //  순서가 뜻이다: 이번 틱 자리를 먼저 적어야 되감기 0(지금 자리)도 기록에서 찾는다 → 판정.
            //  시스템이 러너를 직접 잡으면 러너→룰→시스템→러너 순환이 생겨 빌드 콜백에서 붙인다(DodgeLifetimeScope와 같다).
            builder.RegisterBuildCallback(container =>
            {
                runner.RegisterSystem<LOP.Event.LOPRunner.Update.End>(container.Resolve<MotionHistorySystem>());
                runner.RegisterSystem<LOP.Event.LOPRunner.Update.End>(container.Resolve<MountedArcheryHitSystem>());
            });
        }

        private static readonly RaycastHit[] terrainHits = new RaycastHit[16];

        /// <summary>
        /// 화살 선분이 지형(바위·나무·테두리 — Default 레이어)에 처음 닿는 비율. 지형은 되감지 않고 지금 모습으로 잰다.
        /// 몸은 Character 레이어라 마스크에서 이미 빠지지만, 몸에 딸린 콜라이더가 Default에 있어도 지형으로 치지 않게 한 번 더 거른다.
        /// </summary>
        private static float? TerrainAlong(Vector3 from, Vector3 to)
        {
            Vector3 segment = to - from;
            float length = segment.magnitude;
            if (length < 1e-6f)
            {
                return null;
            }

            int count = Physics.RaycastNonAlloc(from, segment / length, terrainHits, length,
                LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore);
            float? nearest = null;
            for (int i = 0; i < count; i++)
            {
                if (terrainHits[i].collider.GetComponentInParent<LOPActor>() != null)
                {
                    continue;
                }
                float along = terrainHits[i].distance / length;
                if (nearest.HasValue == false || along < nearest.Value)
                {
                    nearest = along;
                }
            }
            return nearest;
        }
    }
}
