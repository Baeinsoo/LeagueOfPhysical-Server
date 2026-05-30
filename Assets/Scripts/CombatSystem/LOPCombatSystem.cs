using GameFramework;
using UnityEngine;

namespace LOP
{
    public class LOPCombatSystem : ICombatSystem
    {
        private readonly GameFramework.World.WorldEventBuffer worldEventBuffer;

        public LOPCombatSystem(GameFramework.World.WorldEventBuffer worldEventBuffer)
        {
            this.worldEventBuffer = worldEventBuffer;
        }

        public void Attack(LOPEntity attacker, LOPEntity target)
        {
            bool attackerIsPlayer = attacker.HasEntityComponent<PlayerComponent>();
            bool targetIsPlayer = target.HasEntityComponent<PlayerComponent>();

            if (!attackerIsPlayer && !targetIsPlayer)
            {
                return;
            }

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

            // --- World Core — 슬라이스 3: Generation → 버퍼 Append ---
            // 송신은 WireBroadcaster, Application은 WorldEventApplicator가 ProcessEvent에서 처리.
            // 레거시 HealthComponent.TakeDamage는 walking-skeleton 병렬 경로로 그대로 유지.
            int dealtAmount = isDodged ? 0 : damage;
            bool isDead = healthComponent.currentHP <= 0;

            worldEventBuffer.Append(new GameFramework.World.DamageDealtEvent(
                targetId:   target.entityId,
                attackerId: attacker.entityId,
                amount:     dealtAmount,
                isCritical: isCritical,
                isDodged:   isDodged,
                remaining:  healthComponent.currentHP,
                isDead:     isDead
            ));

            if (isDead)
            {
                worldEventBuffer.Append(new GameFramework.World.DeathEvent(
                    victimId:   target.entityId,
                    attackerId: attacker.entityId
                ));
            }
            // --- end World Core slice 3 ---
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
