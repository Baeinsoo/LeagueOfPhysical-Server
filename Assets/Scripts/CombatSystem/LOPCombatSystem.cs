using GameFramework;
using UnityEngine;

namespace LOP
{
    public class LOPCombatSystem : ICombatSystem
    {
        private ISessionManager sessionManager;

        public LOPCombatSystem(ISessionManager sessionManager)
        {
            this.sessionManager = sessionManager;
        }

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

            int damage = 10;

            StatsComponent attackerStats = attacker.GetEntityComponent<StatsComponent>();
            StatsComponent targetStats = target.GetEntityComponent<StatsComponent>();

            damage += attackerStats.strength;

            bool isDodged = IsDodge(attackerStats.dexterity, targetStats.dexterity);
            bool isCritical = IsCritical(attackerStats.strength, targetStats.strength);
            if (isCritical)
            {
                damage = Mathf.RoundToInt(damage * Random.Range(1.25f, 1.75f));
            }

            if (!isDodged)
            {
                healthComponent.TakeDamage(attacker.entityId, damage);
            }

            DamageEventToC damageEventToC = new DamageEventToC
            {
                Tick = GameEngine.Time.tick,
                AttackerId = attacker.entityId,
                TargetId = target.entityId,
                ActionCode = "attack",
                DamageType = "physical",
                Damage = damage,
                IsCritical = isCritical,
                IsDodged = isDodged,
                IsBlocked = false,
                RemainingHP = healthComponent.currentHP,
                IsDead = healthComponent.currentHP <= 0
            };

            foreach (var session in sessionManager.GetAllSessions())
            {
                session.Send(damageEventToC);
            }
        }

        public bool IsDodge(int attackerDex, int targetDex)
        {
            float dodgeChance = (float)targetDex / (attackerDex + targetDex);
            dodgeChance = Mathf.Clamp(dodgeChance, 0.05f, 0.95f);
            double roll = Random.Range(0.0f, 1.0f);
            return roll < dodgeChance;
        }

        public bool IsCritical(int attackerStr, int targetStr)
        {
            float critChance = (float)attackerStr / (attackerStr + targetStr);
            critChance = Mathf.Clamp(critChance, 0.05f, 0.50f);
            double roll = Random.Range(0.0f, 1.0f);
            return roll < critChance;
        }
    }
}
