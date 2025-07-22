using GameFramework;
using UnityEngine;

namespace LOP
{
    public class LOPCombatSystem : ICombatSystem
    {
        public void Attack(LOPEntity attacker, LOPEntity target)
        {
            if (target.TryGetEntityComponent<HealthComponent>(out var healthComponent) == false)
            {
                return;
            }

            if (healthComponent.currentHP <= 0)
            {
                Debug.LogWarning($"Target {target.entityId} is already dead.");
                return;
            }

            healthComponent.TakeDamage(attacker.entityId, 10);
        }
    }
}
