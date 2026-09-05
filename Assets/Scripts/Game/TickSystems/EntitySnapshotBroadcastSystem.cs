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
            EntitySnap[] allEntitySnaps = BuildAllEntitySnaps(tick, deltaTime);

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

        private EntitySnap[] BuildAllEntitySnaps(long tick, float deltaTime)
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
                var stun = worldEntity.Get<FlappyStun>();
                //  남은 시간이 아니라 "끝나는 절대 틱"을 보낸다 — 받는 쪽이 자기 틱과 빼면 되고,
                //  스냅이 늦게 도착해도 값이 낡지 않는다(어빌리티의 ability_end_tick과 같은 관례).
                snap.StunEndTick = FlappyTickDuration.EndTick(stun?.StunRemaining ?? 0f, tick, deltaTime);
                snap.InvulnEndTick = FlappyTickDuration.EndTick(stun?.InvulnRemaining ?? 0f, tick, deltaTime);

                //  대시도 같은 관례로 "끝나는 절대 틱"을 보낸다. 게이지는 발동 자격의 권위라
                //  함께 싣는다 — 이것이 없으면 클라만 가득 찼다고 믿는 상태가 고쳐지지 않는다.
                var dash = worldEntity.Get<FlappyDash>();
                snap.DashEndTick = FlappyTickDuration.EndTick(dash?.DashRemaining ?? 0f, tick, deltaTime);
                snap.DashCharge = dash?.Charge ?? 0f;

                //  결승선이 없는 게임에는 이 컴포넌트가 없다 — 그러면 0(아직)이 나간다.
                //  아래 Skydive 전용 필드와 같은 방식이다.
                snap.FinishPlacement = worldEntity.Get<FinishPlacement>()?.Value ?? 0;

                var activation = worldEntity.Get<Abilities>()?.Activation;
                if (activation != null)
                {
                    snap.ActiveAbilityId = activation.Value.AbilityId;
                    snap.AbilityEndTick = activation.Value.RecoveryEndTick;
                }

                //  Skydive 전용 필드. 이 컴포넌트가 없는 게임(Flappy·FlapWang·판치기)에서는
                //  posture/stamina가 null이라 기본값(0/false)이 나가고, 그 게임들은 이 필드를
                //  읽지 않으므로 영향이 없다. 두 번씩 읽던 걸 지역변수로 한 번만 조회하도록 정리.
                var posture = worldEntity.Get<Posture>();
                var stamina = worldEntity.Get<Stamina>();
                snap.PostureAxis = posture?.Axis ?? 0f;
                snap.Gliding = posture?.Gliding ?? false;
                snap.Stamina = stamina?.Current ?? 0f;
                //  텔레포트(레이저 피격 등) 여부를 클라가 구분하도록 카운터를 함께 싣는다.
                snap.TeleportCount = worldEntity.Get<GameFramework.World.Transform>()?.TeleportCount ?? 0;
                //  비상 펼침(잔고 0에서의 마지막 구제 창)의 남은 초. 남에게는 InputBuffer가 없어
                //  TryStartGlide가 절대 안 불려 EmergencyRemaining이 로컬에서 0에 묶여 있다 —
                //  이 값을 안 실으면 서버가 비상 펼침 중인 순간, 다음 틱 StaminaSystem.Tick이
                //  "잔고 0인데 EmergencyRemaining도 0"으로 보고 곧바로 접어 버려 남이 그 1초
                //  구제 구간 내내 자유낙하로 보인다(다음 스냅이 와야 되돌아옴 = 러버밴딩).
                snap.EmergencyRemaining = stamina?.EmergencyRemaining ?? 0f;

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

        //  남은 시간(초) → 끝나는 절대 틱. 0 이하면 0(= 해당 상태 아님).
    }
}
