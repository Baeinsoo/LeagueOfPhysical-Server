using UnityEngine;

namespace LOP
{
    /// <summary>
    /// <see cref="DamageEffect"/> 핸들러(서버 전용). Active 진입 시 1회, 시전자 앞 부채꼴 안의 대상을 때린다.
    /// 클라엔 미등록 → executor가 DamageEffect를 무시한다(데미지 = 서버권위, 클라는 연출만 받음).
    /// 레거시 <c>Attack.cs</c>의 OverlapSphere + 부채꼴 판정을 그대로 이식(range/angle은 effect에서).
    /// combatSystem은 DI(어빌리티 그래프와 무관), entityManager는 ctx로 받는다(DI 순환 회피).
    /// </summary>
    public class DamageEffectHandler : AbilityEffectHandler<DamageEffect>
    {
        private readonly ICombatSystem combatSystem;

        public DamageEffectHandler(ICombatSystem combatSystem)
        {
            this.combatSystem = combatSystem;
        }

        protected override void OnActiveEnter(AbilityEffectContext ctx, DamageEffect effect)
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

                combatSystem.Attack(attacker, target);
            }
        }

        // 시전자 정면 부채꼴(전체 각 angle도) 안이고 range 이내인지. 레거시 Attack.IsInAttackSector 이식.
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
