using System;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>봇이 보는 남의 몸 하나(살아 있는 몸만).</summary>
    public readonly struct BotBody
    {
        public readonly string Id;
        public readonly Vector3 Position;
        public readonly float HeadingDeg;
        public readonly Vector3 Velocity;

        public BotBody(string id, Vector3 position, float headingDeg, Vector3 velocity)
        {
            Id = id;
            Position = position;
            HeadingDeg = headingDeg;
            Velocity = velocity;
        }
    }

    /// <summary>날고 있는 화살 하나의 지금 자리와 속도.</summary>
    public readonly struct BotArrow
    {
        public readonly string ShooterId;
        public readonly Vector3 Position;
        public readonly Vector3 Velocity;

        public BotArrow(string shooterId, Vector3 position, Vector3 velocity)
        {
            ShooterId = shooterId;
            Position = position;
            Velocity = velocity;
        }
    }

    /// <summary>봇 두뇌가 한 틱에 보는 것. 서버의 봇 시스템이 매 틱 채운다.</summary>
    public struct BotView
    {
        public string SelfId;
        public long Tick;
        public float TickInterval;
        /// <summary>출발했나. 출발 전엔 말이 안 움직여 "막혔다"로 오해하지 않게 한다.</summary>
        public bool Started;

        public Vector3 Position;
        public float HeadingDeg;
        public Vector3 Velocity;
        /// <summary>시뮬이 들고 있는 당김 상태(지난 틱 끝). 놓는 틱의 화살 힘이 바로 이 값이다.</summary>
        public bool AimDrawing;
        public float AimDrawRatio;

        /// <summary>지금 원. 중심은 (x, z).</summary>
        public (Vector2 center, float radius) Zone;
        /// <summary>공개된 다음 원(없으면 지금 원과 같다).</summary>
        public (Vector2 center, float radius) NextZone;

        /// <summary>살아 있는 몸 전부 — 자기 몸이 끼어 있어도 된다.</summary>
        public IReadOnlyList<BotBody> Bodies;
        /// <summary>날고 있는 화살 전부 — 자기 화살이 끼어 있어도 된다.</summary>
        public IReadOnlyList<BotArrow> Arrows;
        /// <summary>눈에서 그 점까지 지형에 가리지 않으면 참.</summary>
        public Func<Vector3, Vector3, bool> Visible;

        public MountedArcheryConfig Config;
    }

    /// <summary>
    /// 시험용 봇 한 명의 머릿속. 매 틱 보이는 것으로 사람과 같은 <see cref="InputCommand"/>를 만든다 — 몸은 직접 만지지 않는다.
    /// 다리(어디로 달릴까)는 피하기 &gt; 원 지키기 &gt; 싸우기(적 옆을 돌기) &gt; 돌아다니기 순으로 고르고,
    /// 손(쏠까)은 따로 돈다 — 원 쪽으로 달리면서도 보이는 적이 있으면 쏜다.
    /// </summary>
    public sealed class MountedArcheryBotBrain
    {
        public enum Mode { Wander, Fight, Zone, Dodge, Unstick }

        /// <summary>할 일을 다시 고르는 주기(초).</summary>
        public const float DecisionSeconds = 0.1f;
        /// <summary>이만큼 당겼으면 놓는다 — 만작(1)에 닿은 틱.</summary>
        public const float ReleaseDraw = 0.98f;
        /// <summary>몸 가운데 높이. 기수(2.1)와 말 몸(1.4) 사이라 위아래로 조금 빗나가도 어딘가에 걸린다.</summary>
        public const float AimHeight = 1.6f;

        // 화살이 이 시간 안에 내 몸에 닿을 길이면 위험으로 본다.
        private const float ThreatHorizonSeconds = 2f;
        // 피하기 시작하면 이만큼 옆으로 튼다.
        private const float DodgeSeconds = 0.5f;
        // 이 각(도)만큼 어긋나면 스틱을 끝까지 꺾는다. 작을수록 급하게 돈다.
        private const float FullTurnDeg = 20f;
        // 원 안쪽으로 이만큼(반지름 비율, 최대 5 m)은 들어가 있어야 안전하다고 본다.
        private const float ZoneMarginRatio = 0.2f;
        private const float ZoneMarginMax = 5f;
        // 싸울 때 적과 이 사이 거리를 지키며 돈다.
        private const float OrbitNear = 20f;
        private const float OrbitFar = 40f;
        // 돌아다니기 목표에 이만큼 다가가면 새로 고른다.
        private const float WanderArrive = 6f;
        // 달리라고 했는데 이 속도 아래로 이 시간 동안 머물면 바위·벽에 막힌 것으로 본다.
        private const float StuckSpeed = 1.5f;
        private const float StuckSeconds = 0.75f;
        private const float UnstickSeconds = 0.6f;

        private readonly System.Random random;
        private readonly float reactionSeconds;

        private Mode mode = Mode.Wander;
        private long nextDecisionTick = long.MinValue;
        private string targetId;
        private Vector2 wanderGoal;
        private bool hasWanderGoal;
        private int orbitSide = 1;

        private long dodgeStartTick = -1;
        private long dodgeEndTick = -1;
        private float dodgeBearing;

        private long stuckSinceTick = -1;
        private long unstickEndTick = -1;
        private float unstickTurn = 1f;
        private float lastThrottle;

        public MountedArcheryBotBrain(MountedArcheryConfig config, System.Random random)
        {
            this.random = random;
            reactionSeconds = Mathf.Lerp(config.BotReactionMinSeconds, config.BotReactionMaxSeconds,
                                         (float)random.NextDouble());
        }

        public Mode CurrentMode => mode;
        public float ReactionSeconds => reactionSeconds;

        public InputCommand Think(in BotView view)
        {
            var command = new InputCommand { ViewTick = view.Tick };

            NoticeThreat(view);
            if (view.Tick >= nextDecisionTick)
            {
                Decide(view);
                nextDecisionTick = view.Tick + Math.Max(1, Mathf.RoundToInt(DecisionSeconds / view.TickInterval));
            }

            Vector2 stick = Steer(view);
            command.Horizontal = stick.x;
            command.Vertical = stick.y;
            lastThrottle = stick.y;

            Shoot(view, command);
            return command;
        }

        // ── 다리 ─────────────────────────────────────────────

        private void Decide(in BotView view)
        {
            targetId = NearestVisibleEnemy(view);

            if (view.Tick < unstickEndTick || (view.Tick >= dodgeStartTick && view.Tick < dodgeEndTick))
            {
                return;   // 빠져나오기·피하기는 정한 시간만큼 끝까지 한다
            }

            Vector2 me = Flat(view.Position);
            if (OutsideSafe(me, view.Zone) || OutsideSafe(me, view.NextZone))
            {
                mode = Mode.Zone;
                return;
            }

            if (targetId != null)
            {
                if (mode != Mode.Fight)
                {
                    //  돌 방향은 싸움을 시작할 때 한 번 고른다 — 매번 고르면 좌우로 흔들린다.
                    orbitSide = random.Next(2) == 0 ? -1 : 1;
                }
                mode = Mode.Fight;
                return;
            }

            if (mode != Mode.Wander || hasWanderGoal == false
                || Vector2.Distance(me, wanderGoal) < WanderArrive
                || OutsideSafe(wanderGoal, view.NextZone))
            {
                PickWanderGoal(view);
            }
            mode = Mode.Wander;
        }

        private Vector2 Steer(in BotView view)
        {
            if (view.Tick >= dodgeStartTick && view.Tick < dodgeEndTick)
            {
                mode = Mode.Dodge;
                return Toward(view.HeadingDeg, dodgeBearing);
            }

            if (view.Tick < unstickEndTick)
            {
                //  곧바로 아래 + 옆 = 브레이크 뒤 돌면서 후진. 막힌 앞을 벗어난다.
                return new Vector2(unstickTurn, -1f);
            }

            if (CheckStuck(view))
            {
                mode = Mode.Unstick;
                unstickTurn = random.Next(2) == 0 ? -1f : 1f;
                unstickEndTick = view.Tick + SecondsToTicks(UnstickSeconds, view.TickInterval);
                hasWanderGoal = false;   // 같은 바위로 다시 달려가지 않게
                return new Vector2(unstickTurn, -1f);
            }

            switch (mode)
            {
                case Mode.Zone:
                {
                    var goal = OutsideSafe(Flat(view.Position), view.NextZone) ? view.NextZone.center : view.Zone.center;
                    return Toward(view.HeadingDeg, BearingTo(view.Position, goal));
                }
                case Mode.Fight when TryFind(view, targetId, out var target):
                {
                    float bearing = BearingTo(view.Position, Flat(target.Position));
                    float distance = Vector2.Distance(Flat(view.Position), Flat(target.Position));
                    //  멀면 비스듬히 다가가고, 가까우면 비스듬히 벌린다 — 적은 늘 말 옆쪽에 남는다.
                    float offset = distance > OrbitFar ? 60f : distance < OrbitNear ? 120f : 90f;
                    return Toward(view.HeadingDeg, bearing + orbitSide * offset);
                }
                default:
                    if (hasWanderGoal == false)
                    {
                        PickWanderGoal(view);
                    }
                    return Toward(view.HeadingDeg, BearingTo(view.Position, wanderGoal));
            }
        }

        private bool CheckStuck(in BotView view)
        {
            float speed = new Vector2(view.Velocity.x, view.Velocity.z).magnitude;
            if (view.Started == false || lastThrottle < 0.5f || speed >= StuckSpeed)
            {
                stuckSinceTick = -1;
                return false;
            }
            if (stuckSinceTick < 0)
            {
                stuckSinceTick = view.Tick;
                return false;
            }
            if (view.Tick - stuckSinceTick < SecondsToTicks(StuckSeconds, view.TickInterval))
            {
                return false;
            }
            stuckSinceTick = -1;
            return true;
        }

        private void PickWanderGoal(in BotView view)
        {
            var circle = view.NextZone;
            float radius = Mathf.Max(0f, circle.radius * 0.6f);
            float distance = radius * Mathf.Sqrt((float)random.NextDouble());
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            wanderGoal = circle.center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
            hasWanderGoal = true;
        }

        private static bool OutsideSafe(Vector2 position, (Vector2 center, float radius) circle)
        {
            float margin = Mathf.Min(ZoneMarginMax, circle.radius * ZoneMarginRatio);
            return Vector2.Distance(position, circle.center) > circle.radius - margin;
        }

        /// <summary>바라는 방향으로 말을 돌리며 끝까지 민다. 스틱은 말 기준(x = 돌기, y = 앞).</summary>
        private static Vector2 Toward(float headingDeg, float bearingDeg)
        {
            float delta = Mathf.DeltaAngle(headingDeg, bearingDeg);
            return new Vector2(Mathf.Clamp(delta / FullTurnDeg, -1f, 1f), 1f);
        }

        // ── 피하기 ───────────────────────────────────────────

        //  위험은 보이는 즉시 알아채지만 몸은 반응 지연 뒤에야 움직인다. 이미 피하는 중이면 새 화살은 보지 않는다.
        private void NoticeThreat(in BotView view)
        {
            if (view.Arrows == null || dodgeStartTick >= 0 && view.Tick < dodgeEndTick)
            {
                return;
            }

            for (int i = 0; i < view.Arrows.Count; i++)
            {
                var arrow = view.Arrows[i];
                if (arrow.ShooterId == view.SelfId || WillHit(view, arrow) == false)
                {
                    continue;
                }

                dodgeStartTick = view.Tick + SecondsToTicks(reactionSeconds, view.TickInterval);
                dodgeEndTick = dodgeStartTick + SecondsToTicks(DodgeSeconds, view.TickInterval);
                //  화살 길과 직각으로, 지금 내가 이미 치우친 쪽으로 빠진다.
                var along = new Vector2(arrow.Velocity.x, arrow.Velocity.z);
                var side = new Vector2(along.y, -along.x);
                var offset = Flat(view.Position) - Flat(arrow.Position);
                if (Vector2.Dot(side, offset) < 0f)
                {
                    side = -side;
                }
                dodgeBearing = Mathf.Atan2(side.x, side.y) * Mathf.Rad2Deg;
                return;
            }
        }

        private static bool WillHit(in BotView view, in BotArrow arrow)
        {
            //  싼 걸러내기 먼저: 위에서 내려다본 직선으로 가장 가까워지는 때가 앞쪽이고 3 m 안일 때만 자세히 본다.
            //  높이는 중력 때문에 직선으로 못 재므로 아래 궤적 계산에 맡긴다.
            Vector2 relative = Flat(view.Position) - Flat(arrow.Position);
            Vector2 closing = Flat(arrow.Velocity) - Flat(view.Velocity);
            float speedSq = closing.sqrMagnitude;
            if (speedSq < 1e-4f)
            {
                return false;
            }
            float closest = Vector2.Dot(relative, closing) / speedSq;
            if (closest <= 0f || closest > ThreatHorizonSeconds)
            {
                return false;
            }
            if ((relative - closing * closest).sqrMagnitude > 9f)
            {
                return false;
            }

            var shot = new ArcheryShot(arrow.ShooterId, 0, arrow.Position, arrow.Velocity);
            float dt = view.TickInterval;
            int steps = Mathf.CeilToInt(Mathf.Min(ThreatHorizonSeconds, closest + 0.3f) / dt);
            Vector3 previous = arrow.Position;
            for (int i = 1; i <= steps; i++)
            {
                float s = i * dt;
                Vector3 current = ArcheryTrajectory.PositionAt(shot, s);
                //  나는 지금 속도로 계속 달린다고 본다.
                if (MountedArcheryHitTest.SegmentHits(previous, current, view.Position + view.Velocity * s,
                                                      view.HeadingDeg, out _))
                {
                    return true;
                }
                previous = current;
            }
            return false;
        }

        // ── 손 ───────────────────────────────────────────────

        private void Shoot(in BotView view, InputCommand command)
        {
            command.AimYaw = view.HeadingDeg;
            if (targetId == null || TryFind(view, targetId, out var target) == false)
            {
                return;   // 겨눌 적이 없으면 당기지 않는다(당기던 것은 놓지 않고 푼다)
            }

            float speed = ArcheryAimSystem.SpeedFor(view.AimDrawRatio, view.Config.ArrowMinSpeed, view.Config.ArrowMaxSpeed);
            var (yaw, pitch) = SolveAim(view, target, speed);

            if (view.AimDrawing && view.AimDrawRatio >= ReleaseDraw)
            {
                //  사람처럼 빗나가게: 좌우·위아래에 정규 분포 오차를 발마다 새로 얹는다.
                float error = view.Config.BotAimErrorDeg;
                command.AimYaw = yaw + Gaussian() * error;
                command.AimPitch = pitch + Gaussian() * error;
                command.Release = true;
                command.Drawing = false;
                return;
            }

            command.AimYaw = yaw;
            command.AimPitch = pitch;
            command.Drawing = true;
            command.DrawRatio = 1f;
        }

        /// <summary>
        /// 이번 틱에 놓으면 맞도록 겨눌 좌우·위아래 각(도). 앞질러 겨누고 낙하만큼 올린다.
        /// 시간 맞춤: 쏘는 틱에 나도 적도 한 틱씩 움직인 뒤 화살이 떠나고, 판정은 화살이 s초 날았을 때 적의 (s + 한 틱) 뒤 자리로 잰다.
        /// </summary>
        public static (float yaw, float pitch) SolveAim(in BotView view, in BotBody target, float arrowSpeed)
        {
            float dt = view.TickInterval;
            Vector3 origin = view.Position + view.Velocity * dt + Vector3.up * view.Config.RiderEyeHeight;
            Vector3 aimPoint = target.Position + Vector3.up * AimHeight;
            Vector3 targetVelocity = new Vector3(target.Velocity.x, 0f, target.Velocity.z);

            float flight = Vector3.Distance(origin, aimPoint) / Mathf.Max(arrowSpeed, 1f);
            float pitch = 0f;
            Vector3 lead = aimPoint;
            //  비행시간과 앞지른 자리가 서로를 정한다 — 몇 번 되풀이하면 자리 잡는다.
            for (int i = 0; i < 4; i++)
            {
                lead = aimPoint + targetVelocity * (dt + flight);
                Vector3 delta = lead - origin;
                float horizontal = new Vector2(delta.x, delta.z).magnitude;
                pitch = LaunchPitch(horizontal, delta.y, arrowSpeed);
                float cos = Mathf.Cos(pitch * Mathf.Deg2Rad);
                flight = horizontal / Mathf.Max(arrowSpeed * cos, 1e-3f);
            }

            Vector3 d = lead - origin;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            return (yaw, pitch);
        }

        // 낮은 포물선 쪽 발사각. 닿지 못할 거리면 가장 멀리 가는 45도.
        private static float LaunchPitch(float horizontal, float height, float speed)
        {
            if (horizontal < 0.1f)
            {
                return height >= 0f ? 89f : -89f;
            }
            float g = ArcheryTrajectory.Gravity;
            float v2 = speed * speed;
            float disc = v2 * v2 - g * (g * horizontal * horizontal + 2f * height * v2);
            if (disc < 0f)
            {
                return 45f;
            }
            return Mathf.Atan((v2 - Mathf.Sqrt(disc)) / (g * horizontal)) * Mathf.Rad2Deg;
        }

        private string NearestVisibleEnemy(in BotView view)
        {
            if (view.Bodies == null)
            {
                return null;
            }

            Vector3 eye = view.Position + Vector3.up * view.Config.RiderEyeHeight;
            float best = view.Config.BotEngageRange;
            string found = null;
            for (int i = 0; i < view.Bodies.Count; i++)
            {
                var body = view.Bodies[i];
                if (body.Id == view.SelfId)
                {
                    continue;
                }
                float distance = Vector3.Distance(view.Position, body.Position);
                if (distance > best)
                {
                    continue;
                }
                if (view.Visible != null && view.Visible(eye, body.Position + Vector3.up * AimHeight) == false)
                {
                    continue;
                }
                best = distance;
                found = body.Id;
            }
            return found;
        }

        private static bool TryFind(in BotView view, string id, out BotBody body)
        {
            body = default;
            if (id == null || view.Bodies == null)
            {
                return false;
            }
            for (int i = 0; i < view.Bodies.Count; i++)
            {
                if (view.Bodies[i].Id == id)
                {
                    body = view.Bodies[i];
                    return true;
                }
            }
            return false;
        }

        // 표준 정규 분포(박스-뮬러).
        private float Gaussian()
        {
            double u1 = 1.0 - random.NextDouble();
            double u2 = random.NextDouble();
            return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
        }

        private static float BearingTo(Vector3 from, Vector2 to)
        {
            Vector2 d = to - Flat(from);
            return Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
        }

        private static Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);

        private static int SecondsToTicks(float seconds, float tickInterval)
            => Math.Max(1, Mathf.RoundToInt(seconds / tickInterval));
    }
}
