using GameFramework;
using UnityEngine;

namespace LOP
{
    public class Attack : Action
    {
        private bool hasHit = false;
        private float hitTime = 0.01f;
        private float range = 2;
        private float angle = 60;

        protected override void OnActionUpdate()
        {
            if (elapsedTime >= hitTime && !hasHit)
            {
                PerformHit();
                hasHit = true;
            }
        }

        protected override void OnActionEnd()
        {
            Clear();
        }

        private void Clear()
        {
            hasHit = false;
        }

        private void PerformHit()
        {
            LayerMask layerMask = LayerMask.GetMask("Default");

            Collider[] hits = Physics.OverlapSphere(entity.position, range, layerMask);
            foreach (var hit in hits)
            {
                if (IsInAttackSector(entity, hit.transform.position, range, angle))
                {
                    if (hit.transform.name == "Plane")
                    {
                        continue;
                    }

                    ICombatSystem combatSystem = SceneLifetimeScope.Resolve<ICombatSystem>();
                    combatSystem.Attack(entity, hit.transform.parent.parent.GetComponentInChildren<LOPEntity>());
                }
            }
        }

        bool IsInAttackSector(LOPEntity attacker, Vector3 targetPosition, float range, float angle)
        {
            Vector3 toTarget = targetPosition - attacker.position;
            float distance = toTarget.magnitude;

            if (distance > range)
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
