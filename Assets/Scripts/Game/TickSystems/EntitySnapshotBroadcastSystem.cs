using System.Collections.Generic;
using GameFramework;

namespace LOP
{
    /// <summary>구 LOPRunner.EndUpdate(엔티티 스냅샷 청킹·브로드캐스트 부분) + BuildAllEntitySnaps 이동.</summary>
    public class EntitySnapshotBroadcastSystem : GameFramework.Runner.ITickSystem
    {
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly ISessionManager sessionManager;

        public EntitySnapshotBroadcastSystem(GameFramework.World.EntityRegistry entityRegistry, ISessionManager sessionManager)
        {
            this.entityRegistry = entityRegistry;
            this.sessionManager = sessionManager;
        }

        public void Tick(long tick, float deltaTime)
        {
            EntitySnap[] allEntitySnaps = BuildAllEntitySnaps();

            // durable 스냅샷 → unreliable(막 배송). 유실돼도 다음 스냅이 최신 전체를 덮음.
            // Mirror unreliable은 큰 메시지 조각내기(fragment) 불가 → 배치 한도(≈1184B) 초과 시 통째 드롭.
            // 그래서 엔티티를 바이트 예산으로 나눠 여러 메시지(같은 tick)로 청킹(서브셋 청킹, Quake/Source식).
            // 각 청크 독립 → 하나 유실돼도 그 엔티티만 한 틱 놓치고 다음 틱 복구(fragment-재조립의 손실 복리 회피).
            const int MaxEntityBytesPerMessage = 1000;   // 한도(1184) 밑 여유(tick 필드·메시지 프레이밍 몫).

            List<EntitySnapsToC> chunks = new List<EntitySnapsToC>();   // 세션 무관(같은 스냅) → 한 번 만들어 모두에게.
            EntitySnapsToC chunk = new EntitySnapsToC { Tick = tick };
            int chunkBytes = 0;
            foreach (var snap in allEntitySnaps)
            {
                int snapBytes = snap.CalculateSize() + 2;   // +반복 필드 태그/길이 근사
                if (chunk.EntitySnaps.Count > 0 && chunkBytes + snapBytes > MaxEntityBytesPerMessage)
                {
                    chunks.Add(chunk);
                    chunk = new EntitySnapsToC { Tick = tick };
                    chunkBytes = 0;
                }
                chunk.EntitySnaps.Add(snap);
                chunkBytes += snapBytes;
            }
            if (chunk.EntitySnaps.Count > 0)
            {
                chunks.Add(chunk);
            }

            foreach (var session in sessionManager.GetAllSessions())
            {
                foreach (var entitySnapsToC in chunks)
                {
                    session.Send(entitySnapsToC, reliable: false);
                }
            }
        }

        private EntitySnap[] BuildAllEntitySnaps()
        {
            var entitySnapList = new List<EntitySnap>();

            foreach (var worldEntity in entityRegistry.All)
            {
                GameFramework.World.Health health = worldEntity?.Get<GameFramework.World.Health>();
                var snap = new EntitySnap
                {
                    EntityId = worldEntity.Id,
                    Position = MapperConfig.mapper.Map<ProtoVector3>(GameFramework.World.EntityMotionExtensions.GetPosition(worldEntity)),
                    Rotation = MapperConfig.mapper.Map<ProtoVector3>(GameFramework.World.EntityMotionExtensions.GetRotation(worldEntity)),
                    Velocity = MapperConfig.mapper.Map<ProtoVector3>(GameFramework.World.EntityMotionExtensions.GetVelocity(worldEntity)),
                    MaxHP = health?.Max ?? 0,
                    CurrentHP = health?.Current ?? 0,
                };

                snap.Grounded = worldEntity.Get<GameFramework.World.GroundState>()?.IsGrounded ?? false;

                var activation = worldEntity.Get<Abilities>()?.Activation;
                if (activation != null)
                {
                    snap.ActiveAbilityId = activation.Value.AbilityId;
                    snap.AbilityEndTick = activation.Value.RecoveryEndTick;
                }

                var statusEffects = worldEntity.Get<StatusEffects>();
                if (statusEffects != null)
                {
                    foreach (var e in statusEffects.Effects)
                    {
                        snap.StatusEffects.Add(new ProtoActiveEffect
                        {
                            EffectId = e.EffectId,
                            ExpireTick = e.ExpireTick,
                            StackCount = e.StackCount,
                        });
                    }
                }

                var contributions = worldEntity?.Get<MotionContributions>();
                if (contributions != null)
                {
                    foreach (var c in contributions.Items)
                    {
                        snap.MotionContributions.Add(new ProtoMotionContribution
                        {
                            Horizontal = new ProtoVector3 { X = c.Horizontal.X, Y = c.Horizontal.Y, Z = c.Horizontal.Z },
                            Mode = (int)c.Mode,
                            Priority = c.Priority,
                            StartTick = c.StartTick,
                            EndTick = c.EndTick,
                            DecayPerTick = c.DecayPerTick,
                        });
                    }
                }

                entitySnapList.Add(snap);
            }

            return entitySnapList.ToArray();
        }
    }
}
