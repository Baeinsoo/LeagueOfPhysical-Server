using System.Collections.Generic;
using System.Linq;
using GameFramework;
using GameFramework.World;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class MountedArcheryHitSystemTests
    {
        const float Dt = 0.02f;
        const int DrawTicks = 25;   // 끝까지 당긴다 — 가장 빠른 화살(80 m/s)
        const float TargetZ = 30f;

        private sealed class NoopMotionBridge : IMotionBridge
        {
            public void SyncTransforms() { }
            public System.Numerics.Vector3 Depenetrate(Entity entity) => System.Numerics.Vector3.Zero;
            public void Separate(Entity entity) { }
            public void PushMotion(Entity entity) { }
        }

        private sealed class NeverHit : GameFramework.Physics.ICollisionQuery
        {
            public GameFramework.Physics.CollisionHit CapsuleCast(Vector3 p1, Vector3 p2, float radius,
                Vector3 direction, float distance, int layerMask)
                => GameFramework.Physics.CollisionHit.None;

            public GameFramework.Physics.CollisionHit Raycast(Vector3 origin, Vector3 direction,
                float distance, int layerMask)
                => GameFramework.Physics.CollisionHit.None;

            public GameFramework.Physics.CollisionHit[] OverlapSphere(Vector3 center, float radius, int layerMask)
                => System.Array.Empty<GameFramework.Physics.CollisionHit>();
        }

        static readonly MountSettings Mount = new MountSettings(
            maxSpeed: 13f, acceleration: 9f, deceleration: 16f,
            turnRateStoppedDeg: 172f, turnRateAtMaxDeg: 92f,
            sharpTurnDeg: 100f, sharpTurnSpeedFactor: 0.35f, drawSpeedFactor: 0.5f, reverseSpeed: 3f);

        static readonly MountedArcheryConfig Config
            = new MountedArcheryConfig(150f, 40f, 80f, 2.5f, Mount, 100, 34, 0.5f, 100f, 30f, null, 4f, 90f, 0.6f, 0.5f, 15f);

        sealed class Fx
        {
            public readonly EntityRegistry Registry = new EntityRegistry();
            public readonly WorldEventBuffer Events = new WorldEventBuffer();
            public readonly MountedArcheryMatchState State = new MountedArcheryMatchState();
            public readonly List<string> Despawned = new List<string>();
            public readonly MountedArcheryWorld World;
            public readonly MotionHistorySystem History;
            public readonly MountedArcheryHitSystem Hit;

            public System.Func<Vector3, Vector3, float?> Obstacle = (a, b) => null;
            public System.Action<long> BeforeEnd;
            public bool RecordHistory = true;
            public long Tick;

            public Fx()
            {
                World = new MountedArcheryWorld(Registry, Events,
                    new ArcheryAimSystem(Config.ToBowConfig(), Config.RiderEyeHeight, false),
                    new MountSystem(Config.Mount),
                    new KinematicMoveSystem(new NeverHit(), 0),
                    new NoopMotionBridge(), Dt)
                {
                    GameplayStartTick = 0,
                };
                History = new MotionHistorySystem(World);
                Hit = new MountedArcheryHitSystem(World, Registry, State, Config, new HealthSystem(), Events,
                    id => Despawned.Add(id), (a, b) => Obstacle(a, b), Dt);
            }

            //  몸 모양(CapsuleShape)을 안 붙인다 — 키네마틱 이동이 건너뛰어 중력으로 떨어지지 않는다.
            public Entity Rider(string id, Vector3 position, float headingDeg = 0f, int hp = 100)
            {
                var e = new Entity(id);
                e.Add(new GameFramework.World.Transform
                {
                    Position = position.ToNumerics(),
                    Rotation = Quaternion.Euler(0f, headingDeg, 0f).ToNumerics(),
                });
                e.Add(new Velocity());
                e.Add(new ArcheryAim());
                e.Add(new InputBuffer());
                e.Add(new Simulated());
                e.Add(new Mount());
                e.Add(new Health(hp));
                e.Add(new Ownership("user-" + id));
                Registry.Add(e);
                State.Add(id, "user-" + id, id);
                return e;
            }

            // 서버 한 틱과 같은 순서: 월드 → (몸 옮기기) → End: 기록 → 판정
            public void Step()
            {
                Tick++;
                World.Tick(Tick, Dt);
                BeforeEnd?.Invoke(Tick);
                if (RecordHistory)
                {
                    History.Tick(Tick, Dt);
                }
                Hit.Tick(Tick, Dt);
            }

            public void Run(int ticks)
            {
                for (int i = 0; i < ticks; i++) Step();
            }

            /// <summary>쏜 사람들이 같은 틱에 당겼다 뗀다. viewTickOf(발사틱) = 그 사람 화면 틱. 발사틱을 돌려준다.</summary>
            public long Fire(params (Entity shooter, float yaw, System.Func<long, long> viewTickOf)[] shooters)
            {
                for (int i = 0; i < DrawTicks; i++)
                {
                    foreach (var s in shooters)
                    {
                        s.shooter.Get<InputBuffer>().Current = new InputCommand
                        {
                            Drawing = true, DrawRatio = 1f, AimYaw = s.yaw,
                        };
                    }
                    Step();
                }

                long fireTick = Tick + 1;
                foreach (var s in shooters)
                {
                    s.shooter.Get<InputBuffer>().Current = new InputCommand
                    {
                        Release = true, DrawRatio = 1f, AimYaw = s.yaw, ViewTick = s.viewTickOf(fireTick),
                    };
                }
                Step();
                foreach (var s in shooters)
                {
                    s.shooter.Get<InputBuffer>().Current = null;
                }
                return fireTick;
            }

            public int HpOf(Entity e) => e.Get<Health>().Current;

            public List<DamageDealtEvent> Damage => Events.Snapshot.OfType<DamageDealtEvent>().ToList();
        }

        static void SetX(Entity e, float x)
        {
            var p = e.Get<GameFramework.World.Transform>().Position;
            e.Get<GameFramework.World.Transform>().Position = new System.Numerics.Vector3(x, p.Y, p.Z);
        }

        //  화살이 몸 바로 앞(z ≥ 28)에 처음 닿는 틱이 발사 뒤 몇 틱인가. 쏘는 모양이 늘 같아 값도 늘 같다 —
        //  몸을 옮기는 시각을 이 값에 맞춰 잡는다.
        static long ArrivalOffset()
        {
            var fx = new Fx();
            var a = fx.Rider("A", Vector3.zero);
            long fire = fx.Fire((a, 0f, f => f));
            var shot = fx.World.Shots[0];
            for (long k = 1; k < 200; k++)
            {
                if (ArcheryTrajectory.PositionAt(shot, k * Dt).z >= 28f) return k;
            }
            throw new System.InvalidOperationException("화살이 앞에 닿지 않는다");
        }

        //  B는 lastInLaneTick까지 화살 길(x=0)에 서 있다가 그다음부터 매 틱 옆으로 1 m씩 비킨다.
        static void LeaveLaneAfter(Fx fx, Entity b, long lastInLaneTick)
        {
            fx.BeforeEnd = t => SetX(b, Mathf.Max(0f, t - lastInLaneTick));
        }

        [Test]
        public void 정면으로_쏘면_맞고_체력이_깎인다()
        {
            var fx = new Fx();
            var a = fx.Rider("A", Vector3.zero);
            var b = fx.Rider("B", new Vector3(0f, 0f, TargetZ));

            fx.Fire((a, 0f, f => f));
            fx.Run(60);

            Assert.AreEqual(66, fx.HpOf(b));
            var damage = fx.Damage;
            Assert.AreEqual(1, damage.Count);
            Assert.AreEqual("B", damage[0].targetId);
            Assert.AreEqual("A", damage[0].attackerId);
            Assert.AreEqual(34, damage[0].amount);
        }

        static int HpAfterShot(long viewOffset, long leaveRelativeToArrival)
        {
            long arrival = ArrivalOffset();
            var fx = new Fx();
            var a = fx.Rider("A", Vector3.zero);
            var b = fx.Rider("B", new Vector3(0f, 0f, TargetZ));
            long fireTick = DrawTicks + 1;
            LeaveLaneAfter(fx, b, fireTick + arrival + leaveRelativeToArrival);

            fx.Fire((a, 0f, f => f - viewOffset));
            fx.Run(60);
            return fx.HpOf(b);
        }

        [Test]
        public void 쏜_사람이_보던_자리로_잰다()
        {
            //  닿는 틱엔 이미 5 m 넘게 비켜 있다 — 지금 자리로는 빗나간다. 10틱 전 화면에선 길 위에 있었다.
            Assert.AreEqual(66, HpAfterShot(viewOffset: 10, leaveRelativeToArrival: -5));
            Assert.AreEqual(100, HpAfterShot(viewOffset: 0, leaveRelativeToArrival: -5), "대조: 되감지 않으면 빗나간다");
        }

        [Test]
        public void ViewTick이_없으면_되감지_않는다()
        {
            //  0을 "아주 옛날"로 읽으면 상한(25틱)까지 되감아 길 위에 있던 B를 맞힌다.
            long arrival = ArrivalOffset();
            var fx = new Fx();
            var a = fx.Rider("A", Vector3.zero);
            var b = fx.Rider("B", new Vector3(0f, 0f, TargetZ));
            LeaveLaneAfter(fx, b, DrawTicks + 1 + arrival - 5);

            fx.Fire((a, 0f, f => 0));
            fx.Run(60);

            Assert.AreEqual(100, fx.HpOf(b));
        }

        static int HpWithOldView(long lastInLaneRelativeToArrival)
        {
            long arrival = ArrivalOffset();
            var fx = new Fx();
            var a = fx.Rider("A", Vector3.zero);
            var b = fx.Rider("B", new Vector3(0f, 0f, TargetZ));
            //  기록을 넉넉히 쌓아 둔다 — 100틱 전 기록도 남아 있어야 "상한이 자른다"를 잴 수 있다.
            const int warmup = 100;
            long fireTick = warmup + DrawTicks + 1;
            LeaveLaneAfter(fx, b, fireTick + arrival + lastInLaneRelativeToArrival);

            fx.Run(warmup);
            fx.Fire((a, 0f, f => f - 100));
            fx.Run(60);
            return fx.HpOf(b);
        }

        [Test]
        public void 상한보다_오래된_시각은_잘린다()
        {
            //  26틱 전까지만 길 위에 있었다 — 100틱을 다 되감으면 맞지만 상한 25틱이면 안 맞는다.
            Assert.AreEqual(100, HpWithOldView(lastInLaneRelativeToArrival: -26));
            Assert.AreEqual(66, HpWithOldView(lastInLaneRelativeToArrival: -20), "대조: 상한 안이면 맞는다");
        }

        [Test]
        public void 기록이_모자라면_가장_오래된_기록으로_잰다()
        {
            long arrival = ArrivalOffset();
            var fx = new Fx();
            var a = fx.Rider("A", Vector3.zero);
            var b = fx.Rider("B", new Vector3(0f, 0f, TargetZ));
            //  화면 틱이 0 이하면 "안 보냈다"로 읽히므로 판을 좀 굴린 뒤에 쏜다(이 동안은 기록하지 않는다).
            const int warmup = 40;
            long fireTick = warmup + DrawTicks + 1;
            long firstRecorded = fireTick + arrival - 2;
            LeaveLaneAfter(fx, b, firstRecorded);
            fx.RecordHistory = false;
            var before = fx.BeforeEnd;
            fx.BeforeEnd = t =>
            {
                before(t);
                fx.RecordHistory = t >= firstRecorded;   // 닿기 직전 세 틱만 적힌다
            };

            Assert.DoesNotThrow(() =>
            {
                fx.Run(warmup);
                fx.Fire((a, 0f, f => f - 30));   // 상한(25틱)만큼 되감고 싶지만 기록은 세 틱뿐
                fx.Run(60);
            });
            //  지금 자리(2 m 넘게 비킴)로는 빗나간다 — 가장 오래된 기록(길 위)으로 쟀기에 맞았다.
            Assert.AreEqual(66, fx.HpOf(b));
        }

        //  화살 길 위 z에 선 벽. 선분이 그 z를 지나면 그 비율을 돌려준다.
        static System.Func<Vector3, Vector3, float?> WallAtZ(float z)
            => (from, to) =>
            {
                if (from.z < z && to.z >= z) return (z - from.z) / (to.z - from.z);
                return null;
            };

        [Test]
        public void 바위가_먼저_막으면_안_맞는다()
        {
            var fx = new Fx();
            var a = fx.Rider("A", Vector3.zero);
            var b = fx.Rider("B", new Vector3(0f, 0f, TargetZ));
            fx.Obstacle = WallAtZ(28.5f);   // 몸(뒤쪽 끝 ≈ 28.7)보다 앞

            fx.Fire((a, 0f, f => f));
            fx.Run(60);

            Assert.AreEqual(100, fx.HpOf(b));
            Assert.AreEqual(0, fx.Damage.Count);
        }

        //  몸에 닿는 바로 그 선분 안에서 바위가 몸보다 앞(0.5배) 또는 뒤(남은 길의 절반)에 있다.
        static int HpWithRockInContactSegment(bool rockFirst)
        {
            var fx = new Fx();
            var a = fx.Rider("A", Vector3.zero);
            var b = fx.Rider("B", new Vector3(0f, 0f, TargetZ));
            var bodyAt = new Vector3(0f, 0f, TargetZ);
            fx.Obstacle = (from, to) =>
            {
                if (MountedArcheryHitTest.SegmentHits(from, to, bodyAt, 0f, out float body) == false) return null;
                return rockFirst ? body * 0.5f : body + (1f - body) * 0.5f;
            };

            fx.Fire((a, 0f, f => f));
            fx.Run(60);
            return fx.HpOf(b);
        }

        [Test]
        public void 같은_선분에서_바위가_더_가까우면_안_맞는다()
        {
            Assert.AreEqual(100, HpWithRockInContactSegment(rockFirst: true));
            Assert.AreEqual(66, HpWithRockInContactSegment(rockFirst: false), "대조: 몸이 먼저면 맞는다");
        }

        [Test]
        public void 바위가_몸_뒤에_있으면_맞는다()
        {
            var fx = new Fx();
            var a = fx.Rider("A", Vector3.zero);
            var b = fx.Rider("B", new Vector3(0f, 0f, TargetZ));
            fx.Obstacle = WallAtZ(32f);   // 몸(앞쪽 끝 ≈ 31.8)보다 뒤

            fx.Fire((a, 0f, f => f));
            fx.Run(60);

            Assert.AreEqual(66, fx.HpOf(b));
        }

        [Test]
        public void 화살_하나는_한_번만_맞는다()
        {
            var fx = new Fx();
            var a = fx.Rider("A", Vector3.zero);
            var b = fx.Rider("B", new Vector3(0f, 0f, TargetZ));
            var c = fx.Rider("C", new Vector3(0f, 0f, TargetZ + 1f));   // B 바로 뒤에 겹쳐 선다

            fx.Fire((a, 0f, f => f));
            fx.Run(60);

            Assert.AreEqual(66, fx.HpOf(b), "가까운 B만, 한 번만");
            Assert.AreEqual(100, fx.HpOf(c));
            Assert.AreEqual(1, fx.Damage.Count);
        }

        [Test]
        public void 자기_화살은_자기를_안_맞힌다()
        {
            var fx = new Fx();
            //  화살은 눈높이 2.5에서 앞으로 나가 자기 말 머리 구(높이 2.32, 반지름 0.21)를 스친다 — 자기를 빼지 않으면 바로 맞는다.
            var a = fx.Rider("A", Vector3.zero);
            fx.Rider("B", new Vector3(50f, 0f, 0f));

            fx.Fire((a, 0f, f => f));
            fx.Run(60);

            Assert.AreEqual(100, fx.HpOf(a));
            Assert.AreEqual(0, fx.Damage.Count);
        }

        [Test]
        public void 같은_틱에_서로_쓰러지면_둘_다_탈락()
        {
            var fx = new Fx();
            var a = fx.Rider("A", Vector3.zero, headingDeg: 0f, hp: 34);
            var b = fx.Rider("B", new Vector3(0f, 0f, TargetZ), headingDeg: 180f, hp: 34);

            fx.Fire((a, 0f, f => f), (b, 180f, f => f));
            fx.Run(60);

            var pa = fx.State.Players["A"];
            var pb = fx.State.Players["B"];
            Assert.GreaterOrEqual(pa.EliminatedTick, 0);
            Assert.AreEqual(pa.EliminatedTick, pb.EliminatedTick);
            Assert.AreEqual("B", pa.KillerEntityId);
            Assert.AreEqual("A", pb.KillerEntityId);
            Assert.AreEqual(2, fx.State.Eliminations.Count);
            Assert.AreEqual(pa.EliminatedTick, fx.State.LastEliminationTick);
            Assert.AreEqual(0, fx.State.AliveCount);
            CollectionAssert.AreEquivalent(new[] { "A", "B" }, fx.Despawned);
        }

        [Test]
        public void 쓰러진_몸은_안_맞는다()
        {
            var fx = new Fx();
            var a = fx.Rider("A", Vector3.zero);
            var b = fx.Rider("B", new Vector3(0f, 0f, TargetZ));
            fx.Step();
            fx.State.Eliminate("B", "", fx.Tick);   // 디스폰은 틱 끝에 일어나 몸은 아직 레지스트리에 남아 있다

            fx.Fire((a, 0f, f => f));
            fx.Run(60);

            Assert.AreEqual(100, fx.HpOf(b));
            Assert.AreEqual(0, fx.Damage.Count);
        }
    }
}
