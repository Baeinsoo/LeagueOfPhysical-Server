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
        private readonly IRandom rng;

        public LOPCombatSystem(
            GameFramework.World.WorldEventBuffer worldEventBuffer,
            GameFramework.World.EntityRegistry entityRegistry,
            GameFramework.World.HealthSystem healthSystem,
            GameFramework.World.StatsSystem statsSystem,
            IRandom rng)
        {
            this.worldEventBuffer = worldEventBuffer;
            this.entityRegistry = entityRegistry;
            this.healthSystem = healthSystem;
            this.statsSystem = statsSystem;
            this.rng = rng;
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
                damage = Mathf.RoundToInt(damage * rng.Range(1.25f, 1.75f));
            }

            int dealtAmount = isDodged ? 0 : damage;

            // --- World Core: DeathEvent → (resolve) ProcessDeaths → DeathCascadeSystem ---
            // World.Health가 HP 진실원본 — Generation(여기)이 직접 mutate하고, 스냅샷(UserEntitySnap)이
            // 클라로의 유일 권위 경로다. DamageDealtEvent는 연출(숫자/크리)용으로만 송출.
            // DeathEvent를 WorldEventBuffer에 append → resolve 단계 ProcessDeaths가 읽어
            // DeathCascadeSystem이 디스폰+경험치 구슬 처리(egress 전).
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
                isDodged:   isDodged
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
            double roll = rng.Range(0.0f, 1.0f);
            return roll < dodgeChance;
        }

        public bool IsCritical(int attackerStr, int targetStr)
        {
            float critChance = (float)attackerStr / (attackerStr + targetStr);
            critChance = Mathf.Clamp(critChance, 0.05f, 0.50f);
            double roll = rng.Range(0.0f, 1.0f);
            return roll < critChance;
        }
    }
}
