using System;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>두뇌가 보는 다른 몸 하나(서 있고 살아 있는 몸만 들어온다). 좌표는 바닥 평면(x, z).</summary>
    public readonly struct KnockbackBotBody
    {
        public readonly string Id;
        public readonly Vector2 Position;
        public readonly Vector2 Velocity;

        public KnockbackBotBody(string id, Vector2 position, Vector2 velocity)
        {
            Id = id;
            Position = position;
            Velocity = velocity;
        }
    }

    /// <summary>두뇌가 보는 날아가는 탄 하나.</summary>
    public readonly struct KnockbackBotShot
    {
        public readonly string ShooterId;
        public readonly long FireTick;
        public readonly Vector2 Origin;
        public readonly Vector2 Direction;

        public KnockbackBotShot(string shooterId, long fireTick, Vector2 origin, Vector2 direction)
        {
            ShooterId = shooterId;
            FireTick = fireTick;
            Origin = origin;
            Direction = direction;
        }
    }

    /// <summary>두뇌가 한 틱에 보는 모든 것. 월드를 직접 만지지 않고 이 값만 보므로 두뇌는 순수하다.</summary>
    public struct KnockbackBotView
    {
        public string SelfId;
        public long Tick;
        public float TickInterval;
        public bool Started;
        public Vector2 Position;
        public Vector2 Velocity;
        public IReadOnlyList<KnockbackBotBody> Enemies;
        public IReadOnlyList<KnockbackBotShot> Shots;
        public (Vector2 center, float radius) Zone;
        public (Vector2 center, float radius) NextZone;
        public KnockbackConfig Config;
    }

    /// <summary>
    /// 넉백 시험용 봇의 머리. 실력은 봇마다 무작위(반응·앞질러 겨눔·조준 오차·피하기·거리·발사 간격)로 달라
    /// 판이 한 가지 패턴으로 굳지 않는다. 우선순위: 날아오는 탄 피하기 → 바닥이 줄어드는 가장자리에서 중심으로 → 적과 거리 유지·옆걸음.
    /// </summary>
    public sealed class KnockbackBotBrain
    {
        const float EdgeMargin = 3.5f;
        const float DodgeWindowSeconds = 0.8f;
        const float DodgeDistance = 1.3f;

        readonly System.Random rng;
        readonly float reactionSeconds;
        readonly float lead;
        readonly float aimError;
        readonly float dodgeSkill;
        readonly float preferredDistance;
        readonly float fireIntervalSeconds;

        long nextFireTick = long.MinValue;
        long nextStrafeFlipTick = long.MinValue;
        float strafeSign;

        string targetId;
        long nextRetargetTick = long.MinValue;

        /// <summary>지금 붙잡고 있는 적(없으면 null). 시험이 목표 유지를 확인하는 데 쓴다.</summary>
        public string TargetId => targetId;

        public KnockbackBotBrain(System.Random rng)
        {
            this.rng = rng;
            reactionSeconds = Range(0.12f, 0.32f);
            lead = Range(0.35f, 0.9f);
            aimError = Range(0.05f, 0.14f);
            dodgeSkill = Range(0.5f, 1.0f);
            preferredDistance = Range(6f, 8.5f);
            fireIntervalSeconds = Range(0.7f, 1.3f);
            strafeSign = rng.NextDouble() < 0.5 ? -1f : 1f;
        }

        float Range(float min, float max) => min + (float)rng.NextDouble() * (max - min);

        public InputCommand Think(KnockbackBotView view)
        {
            var command = new InputCommand { ViewTick = view.Tick };
            if (view.Started == false)
            {
                return command;
            }

            var target = HoldTarget(view, out float distance);

            //  하나만 고르지 않고 섞는다 — 갈림길마다 틱 단위로 갈아타면 몸이 떨린다.
            Vector2 move = Dodge(view) + Inward(view);
            if (target.HasValue)
            {
                move += KeepDistance(view, target.Value, distance);
            }

            if (move.sqrMagnitude > 1f)
            {
                move.Normalize();
            }
            command.Horizontal = move.x;
            command.Vertical = move.y;

            if (target.HasValue && distance <= view.Config.ShotRange)
            {
                TryFire(view, target.Value, distance, command);
            }
            return command;
        }

        // 목표는 0.6~1.2초마다, 또는 목표가 사라졌을 때만 가장 가까운 적으로 바꾼다.
        KnockbackBotBody? HoldTarget(KnockbackBotView view, out float distance)
        {
            distance = float.MaxValue;
            if (view.Enemies == null) return null;

            KnockbackBotBody? held = null;
            KnockbackBotBody? nearest = null;
            float nearestDistance = float.MaxValue;
            foreach (var enemy in view.Enemies)
            {
                if (enemy.Id == view.SelfId) continue;
                if (enemy.Id == targetId) held = enemy;
                float d = Vector2.Distance(enemy.Position, view.Position);
                if (d < nearestDistance)
                {
                    nearestDistance = d;
                    nearest = enemy;
                }
            }

            if (held.HasValue == false || view.Tick >= nextRetargetTick)
            {
                held = nearest;
                targetId = nearest?.Id;
                nextRetargetTick = view.Tick + Mathf.RoundToInt(Range(0.6f, 1.2f) / view.TickInterval);
            }
            if (held.HasValue) distance = Vector2.Distance(held.Value.Position, view.Position);
            return held;
        }

        // 날아오는 탄마다 진행 방향에 수직으로, 탄이 지나갈 쪽의 반대로 비킨다. 아슬아슬할수록 세게.
        Vector2 Dodge(KnockbackBotView view)
        {
            var sum = Vector2.zero;
            if (view.Shots == null) return sum;
            float speed = view.Config.ShotSpeed;
            float dt = view.TickInterval;
            foreach (var shot in view.Shots)
            {
                if (shot.ShooterId == view.SelfId) continue;
                //  쏜 지 반응 시간이 지나야 알아챈다.
                if ((view.Tick - shot.FireTick) * dt < reactionSeconds) continue;

                var at = shot.Origin + shot.Direction * (speed * (view.Tick - shot.FireTick) * dt);
                var toMe = view.Position - at;
                float along = Vector2.Dot(toMe, shot.Direction);
                if (along < 0f || along > speed * DodgeWindowSeconds) continue;
                float side = shot.Direction.x * toMe.y - shot.Direction.y * toMe.x;
                if (Mathf.Abs(side) > DodgeDistance) continue;

                // 탄이 정확히 몸 가운데를 향하면 어느 쪽이든 같다 — 옆걸음 방향을 따른다.
                float away = Mathf.Abs(side) < 0.05f ? strafeSign : Mathf.Sign(side);
                float weight = (DodgeDistance - Mathf.Abs(side)) / DodgeDistance * 2.6f * dodgeSkill;
                sum += new Vector2(-shot.Direction.y, shot.Direction.x) * away * weight;
            }
            return sum;
        }

        // 다음 원이 공개됐으면 그 안쪽, 아니면 지금 원 안쪽. 가장자리 여유선에 다가갈수록 서서히 세게 중심으로 당긴다.
        Vector2 Inward(KnockbackBotView view)
        {
            var circle = view.NextZone;
            if (circle.radius <= 0f) circle = view.Zone;
            var offset = circle.center - view.Position;
            float weight = Mathf.Clamp01((offset.magnitude - (circle.radius - EdgeMargin)) / 1.2f);
            return weight <= 0f ? Vector2.zero : offset.normalized * weight * 2f;
        }

        Vector2 KeepDistance(KnockbackBotView view, KnockbackBotBody enemy, float distance)
        {
            if (view.Tick >= nextStrafeFlipTick)
            {
                if (nextStrafeFlipTick != long.MinValue && rng.NextDouble() < 0.5) strafeSign = -strafeSign;
                nextStrafeFlipTick = view.Tick + Mathf.RoundToInt(Range(0.7f, 2.1f) / view.TickInterval);
            }
            var toEnemy = (enemy.Position - view.Position).normalized;
            float radial = distance > preferredDistance + 1f ? 1f : distance < preferredDistance - 1f ? -1f : 0f;
            var side = new Vector2(-toEnemy.y, toEnemy.x) * strafeSign * 0.7f;
            return toEnemy * radial + side;
        }

        void TryFire(KnockbackBotView view, KnockbackBotBody enemy, float distance, InputCommand command)
        {
            long interval = Math.Max(1, Mathf.RoundToInt(fireIntervalSeconds / view.TickInterval));
            if (nextFireTick == long.MinValue)
            {
                nextFireTick = view.Tick + interval;
            }
            if (view.Tick < nextFireTick) return;

            //  탄이 도착하는 동안 적이 걸어갈 만큼 앞을 겨눈다(얼마나 믿고 앞지를지는 봇마다 다르다).
            var aimPoint = enemy.Position + enemy.Velocity * (distance / view.Config.ShotSpeed) * lead;
            var to = aimPoint - view.Position;
            float yaw = Mathf.Atan2(to.x, to.y) + (float)(rng.NextDouble() + rng.NextDouble() - 1.0) * aimError;
            command.Fire = true;
            command.AimYaw = yaw * Mathf.Rad2Deg;
            nextFireTick = view.Tick + interval;
        }
    }
}
