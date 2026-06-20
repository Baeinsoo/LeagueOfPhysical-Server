using GameFramework;
using UnityEngine;

namespace LOP
{
    public class LOPCombatSystem : ICombatSystem
    {
        private readonly GameFramework.World.WorldEventBuffer worldEventBuffer;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly GameFramework.World.HealthSystem healthSystem;
        private readonly GameFramework.World.StatsSystem statsSystem;

        public LOPCombatSystem(
            GameFramework.World.WorldEventBuffer worldEventBuffer,
            GameFramework.World.EntityRegistry entityRegistry,
            GameFramework.World.HealthSystem healthSystem,
            GameFramework.World.StatsSystem statsSystem)
        {
            this.worldEventBuffer = worldEventBuffer;
            this.entityRegistry = entityRegistry;
            this.healthSystem = healthSystem;
            this.statsSystem = statsSystem;
        }

        public void Attack(LOPEntity attacker, LOPEntity target)
        {
            bool attackerIsPlayer = entityRegistry.Get(attacker.entityId)?.Has<GameFramework.World.Ownership>() == true;
            bool targetIsPlayer = entityRegistry.Get(target.entityId)?.Has<GameFramework.World.Ownership>() == true;

            if (!attackerIsPlayer && !targetIsPlayer)
            {
                return;
            }

            GameFramework.World.Entity worldEntity = entityRegistry.Get(target.entityId);
            GameFramework.World.Health health = worldEntity?.Get<GameFramework.World.Health>();
            if (health == null)
            {
                Debug.LogWarning($"[World] Attack: Health not found for entity {target.entityId}");
                return;
            }

            if (health.IsDead)
            {
                Debug.LogWarning($"Target {target.entityId} is already dead.");
                return;
            }

            int damage = 10;

            GameFramework.World.Stats attackerStats = entityRegistry.Get(attacker.entityId)?.Get<GameFramework.World.Stats>();
            GameFramework.World.Stats targetStats = entityRegistry.Get(target.entityId)?.Get<GameFramework.World.Stats>();

            int attackerStrength = attackerStats != null ? Mathf.RoundToInt(statsSystem.GetValue(attackerStats, (int)GameFramework.World.EntityStatType.Strength)) : 0;
            int attackerDexterity = attackerStats != null ? Mathf.RoundToInt(statsSystem.GetValue(attackerStats, (int)GameFramework.World.EntityStatType.Dexterity)) : 0;
            int targetStrength = targetStats != null ? Mathf.RoundToInt(statsSystem.GetValue(targetStats, (int)GameFramework.World.EntityStatType.Strength)) : 0;
            int targetDexterity = targetStats != null ? Mathf.RoundToInt(statsSystem.GetValue(targetStats, (int)GameFramework.World.EntityStatType.Dexterity)) : 0;

            damage += attackerStrength;

            bool isDodged = IsDodge(attackerDexterity, targetDexterity);
            bool isCritical = IsCritical(attackerStrength, targetStrength);
            if (isCritical)
            {
                damage = Mathf.RoundToInt(damage * Random.Range(1.25f, 1.75f));
            }

            int dealtAmount = isDodged ? 0 : damage;

            // --- World Core — Slice 2: writer flip + 사망 발행 재배치 ---
            // World.Health가 HP 진실원본. Generation(여기)이 mutate, WorldEventApplicator(ProcessEvent)가
            // remaining으로 재적용(멱등). 디스폰 구동 신호 EntityDeath도 사망 시 여기서 발행
            // (예전엔 HealthComponent.TakeDamage 안에 있었음). 디스폰 경로/구독자는 무변경.
            if (!isDodged)
            {
                healthSystem.TakeDamage(health, dealtAmount);
            }

            bool isDead = health.IsDead;

            worldEventBuffer.Append(new GameFramework.World.DamageDealtEvent(
                targetId:   target.entityId,
                attackerId: attacker.entityId,
                amount:     dealtAmount,
                isCritical: isCritical,
                isDodged:   isDodged,
                remaining:  health.Current,
                isDead:     isDead
            ));

            if (isDead)
            {
                worldEventBuffer.Append(new GameFramework.World.DeathEvent(
                    victimId:   target.entityId,
                    attackerId: attacker.entityId
                ));

                EventBus.Default.Publish(EventTopic.Entity, new Event.Entity.EntityDeath(
                    target.entityId, attacker.entityId, target.position));
            }
            // --- end World Core slice 2 ---
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
