using UnityEngine;

namespace LOP
{
    /// <summary>
    /// <see cref="KnockbackEffect"/> 핸들러(서버 전용). Active 진입 시 1회, 시전자 앞 부채꼴 대상을
    /// 공격자 반대 방향으로 미는 Additive 기여를 대상 <see cref="MotionContributions"/>에 등록한다.
    /// 클라 미등록 → executor가 KnockbackEffect를 무시(클라는 스냅샷으로 결과 수신). entityRegistry로 대상
    /// side→World 매핑(combatSystem처럼 DI, 어빌리티 그래프와 무관).
    /// </summary>
    public class KnockbackEffectHandler : AbilityEffectHandler<KnockbackEffect>
    {
        private readonly GameFramework.World.EntityRegistry entityRegistry;

        public KnockbackEffectHandler(GameFramework.World.EntityRegistry entityRegistry)
        {
            this.entityRegistry = entityRegistry;
        }

        protected override void OnActiveEnter(AbilityEffectContext ctx, KnockbackEffect effect)
        {
            var attacker = ctx.EntityManager.GetEntity<LOPEntity>(ctx.Caster.Id);
            if (attacker == null)
            {
                return;
            }

            LayerMask layerMask = LayerMask.GetMask("Default");
            Collider[] hits = Physics.OverlapSphere(attacker.position, effect.Range, layerMask);
            foreach (var hit in hits)
            {
                if (hit.transform.name == "Plane")
                {
                    continue;
                }
                if (IsInAttackSector(attacker, hit.transform.position, effect.Range, effect.Angle) == false)
                {
                    continue;
                }

                var target = hit.transform.parent?.parent?.GetComponentInChildren<LOPEntity>();
                if (target == null || target.entityId == attacker.entityId)
                {
                    continue;
                }

                var contributions = entityRegistry.Get(target.entityId)?.Get<MotionContributions>();
                if (contributions == null)
                {
                    continue;
                }

                contributions.Items.Add(MotionContributionSystem.CreateRadialKnockback(
                    attacker.position.ToNumerics(), target.position.ToNumerics(),
                    effect.Strength, effect.DurationTicks, effect.DecayPerTick, ctx.CurrentTick));
            }
        }

        // 시전자 정면 부채꼴(전체 각 angle도) 안이고 range 이내인지. DamageEffectHandler.IsInAttackSector 이식.
        private static bool IsInAttackSector(LOPEntity attacker, Vector3 targetPosition, float range, float angle)
        {
            Vector3 toTarget = targetPosition - attacker.position;
            if (toTarget.magnitude > range)
            {
                return false;
            }
            Vector3 forward = Quaternion.Euler(attacker.rotation) * Vector3.forward;
            float dot = Vector3.Dot(forward.normalized, toTarget.normalized);
            float targetAngle = Mathf.Acos(dot) * Mathf.Rad2Deg;
            return targetAngle <= (angle * 0.5f);
        }
    }
}
