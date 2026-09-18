using System.Collections.Generic;
using GameFramework.World;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class ArcheryHitSystemTests
    {
        const float TickInterval = 0.02f;
        const long StartTick = 1000;
        const ulong Seed = 0xABCDEFUL;

        sealed class FixedSeed : IMatchSeed
        {
            public ulong Value { get; set; }
        }

        //  간격을 숫자로 적어 넣으면 배포 데이터와 조용히 어긋난다(전에 1.4였는데 데이터는 1.2였다).
        //  기준은 종류에서 뽑는다 — 가장 큰 과녁 둘이 딱 맞닿는 거리 = 최대 반경 x 2.
        //  가장 작은 종류의 반지름(0.20)도 배포 `#ArcheryTarget.xlsx`의 최솟값과 같아야 한다 —
        //  안 맞으면 "한 틱에 반지름보다 적게 움직인다" 뚫림 검사가 실제보다 늦게 울린다.
        static ArcheryConfig Config()
        {
            var kinds = new[]
            {
                new ArcheryTargetKind(0.60f, 1, 50, false, ArcheryTargetShape.Sphere, null),
                new ArcheryTargetKind(0.40f, 2, 35, false, ArcheryTargetShape.Sphere, null),
                new ArcheryTargetKind(0.20f, 4, 15, false, ArcheryTargetShape.Sphere, null),
            };

            //  최대 반경은 설정이 스스로 계산한다 — 간격을 0으로 둔 설정을 한 번 만들어 빌려 온다.
            float touching = Build(kinds, 0f).MaxTargetRadius * 2f;
            return Build(kinds, touching);
        }

        static ArcheryConfig Build(ArcheryTargetKind[] kinds, float minSeparation)
            => new ArcheryConfig(
                wavePeriodTicks: 88, minTargets: 2, maxTargets: 3,
                spawnRadius: 2f, spawnMinY: 2f, spawnMaxY: 6f, minSeparation: minSeparation,
                trapRatioMin: 0f, trapRatioMax: 0f,
                shakeFreeSeconds: 1f, shakeRampSeconds: 2f, shakeMaxDegrees: 3f,
                riseHeightMin: 1.2f, riseHeightMax: 2.4f, staggerTicks: 12, restTicks: 20,
                kinds: kinds);

        sealed class Fixture
        {
            public ArcheryHitSystem System;
            public EntityRegistry Registry;
            public ArcheryWorld World;
            public ArcheryConfig Config;
            public ArcheryCourse Course;
            public ArcheryWaveState WaveState;

            public Entity Archer(string id)
            {
                var entity = new Entity(id);
                entity.Add(new GameFramework.World.Transform());
                entity.Add(new Velocity());
                entity.Add(new ArcheryScore());
                Registry.Add(entity);
                return entity;
            }

            //  사거리 판은 "누가 쏜 화살인가"를 userId로 가린다 — 몸에 주인을 박아 둬야 한다.
            public Entity Archer(string id, string userId)
            {
                var entity = Archer(id);
                entity.Add(new Ownership(userId));
                return entity;
            }

            public int ScoreOf(string id) => Registry.Get(id).Get<ArcheryScore>().Value;

            public List<ArcheryTarget> TargetsOfWave(int wave)
            {
                var targets = new List<ArcheryTarget>();
                Course.Fill(targets, wave, World.GameplayStartTick);
                return targets;
            }

            public int HitEventCount()
            {
                int count = 0;
                foreach (var e in World.EventBuffer.Snapshot)
                {
                    if (e is ArcheryTargetHitEvent) { count++; }
                }
                return count;
            }

            public int LastHitPoints()
            {
                int points = 0;
                foreach (var e in World.EventBuffer.Snapshot)
                {
                    if (e is ArcheryTargetHitEvent hit) { points = hit.points; }
                }
                return points;
            }
        }

        //  레인 몇 개와 그 앞 과녁 자리 하나로 이루어진 임시 씬. EditMode에서도 GameObject는 만들 수 있다.
        sealed class RangeScene : System.IDisposable
        {
            private readonly List<GameObject> spawned = new List<GameObject>();

            public ArcheryRangeLayout Layout { get; }

            public RangeScene(int laneCount, float distance)
            {
                var lanes = new List<ArcheryLane>();
                for (int i = 0; i < laneCount; i++)
                {
                    var root = new GameObject("lane" + i);
                    spawned.Add(root);
                    //  레인을 10m씩 떼어 놓는다 — 옆 레인 과녁이 실수로 선분에 걸리지 않게.
                    root.transform.position = new Vector3(i * 10f, 0f, 0f);

                    var lane = root.AddComponent<ArcheryLane>();
                    lane.Order = i;

                    var stand = new GameObject("stand0");
                    stand.transform.SetParent(root.transform);
                    stand.transform.localPosition = new Vector3(0f, 1.3f, distance);
                    lane.Stands = new[] { stand.transform };

                    lanes.Add(lane);
                }
                Layout = ArcheryRangeLayout.From(lanes);
            }

            public void Dispose()
            {
                foreach (var go in spawned) { Object.DestroyImmediate(go); }
            }
        }

        //  자리 하나짜리 사거리 설정. 과녁은 제자리에 서 있으므로(솟지 않으므로) 쏘기가 쉽다.
        static ArcheryConfig RangeConfig()
        {
            var face = new ArcheryTargetKind(0.61f, 5, 0, false, ArcheryTargetShape.Face, null);
            var stands = new[] { new ArcheryRangeStand(0, 20f, 200, 0f, 0f) };

            return new ArcheryConfig(
                wavePeriodTicks: 88, minTargets: 2, maxTargets: 3,
                spawnRadius: 2f, spawnMinY: 2f, spawnMaxY: 6f, minSeparation: 1.0f,
                trapRatioMin: 0f, trapRatioMax: 0f,
                shakeFreeSeconds: 1f, shakeRampSeconds: 2f, shakeMaxDegrees: 3f,
                riseHeightMin: 1.2f, riseHeightMax: 2.4f, staggerTicks: 12, restTicks: 20,
                kinds: new[] { face },
                courseKind: ArcheryCourseKind.Range, matchDurationTicks: 0,
                range: new ArcheryRangeSettings(face, stands, 25));
        }

        static Fixture BuildRange(long startTick, RangeScene scene, params string[] owners)
        {
            return Build(startTick, RangeConfig(), owners, () => scene.Layout);
        }

        static Fixture Build(long startTick) => Build(startTick, Config());

        static Fixture Build(long startTick, ArcheryConfig config,
                             string[] owners = null,
                             System.Func<ArcheryRangeLayout> layoutSource = null)
        {
            var registry = new EntityRegistry();
            var world = new ArcheryWorld(registry, new WorldEventBuffer(), new ArcheryAimSystem(config), TickInterval);
            world.GameplayStartTick = startTick;
            var waveState = new ArcheryWaveState();
            var course = new ArcheryCourse(
                config, new FixedSeed { Value = Seed },
                owners ?? new[] { "user-a" }, TickInterval,
                //  웨이브 판은 레인이 없다 — 빈 레이아웃이 정상이다.
                layoutSource ?? (() => ArcheryRangeLayout.From(new ArcheryLane[0])));

            return new Fixture
            {
                Registry = registry,
                World = world,
                Config = config,
                Course = course,
                WaveState = waveState,
                System = new ArcheryHitSystem(world, registry, world.EventBuffer, course, waveState, TickInterval),
            };
        }

        /// <summary>
        /// 과녁 한가운데를 정확히 지나가는 화살 한 발. 한 틱(0.02초)에 정확히 <paramref name="distance"/>
        /// 만큼 나아가게 속도를 잡아, 그 틱의 선분이 −z 쪽 <paramref name="distance"/>에서 시작해
        /// 과녁 중심에서 끝나게 한다. 멀리서 출발할수록 선분 위에서 늦게 닿는다.
        /// </summary>
        static ArcheryShot ShotThrough(string shooterId, long fireTick, ArcheryTarget target, float distance)
        {
            Vector3 origin = target.Origin + new Vector3(0f, 0f, -distance);
            return new ArcheryShot(shooterId, fireTick, origin, new Vector3(0f, 0f, distance / TickInterval));
        }

        /// <summary>
        /// 그 틱에 과녁이 있을 자리를 정확히 지나가는 화살. 과녁이 움직이므로 "어디로 쏘나"가
        /// 아니라 "언제 어디에 있을 것인가"를 먼저 풀어야 한다.
        /// </summary>
        static ArcheryShot ShotThroughMoving(string shooterId, long fireTick, in ArcheryTarget target,
                                             long hitTick, float distance)
        {
            //  판정이 재는 것과 같은 시각(구간 가운데)의 자리를 노린다.
            Vector3 at = ArcheryTargetMotion.PositionAt(target, hitTick - 0.5, TickInterval);
            Vector3 origin = at + new Vector3(0f, 0f, -distance);
            float seconds = (hitTick - fireTick) * TickInterval;
            //  한 틱 만에 닿게 잡으면 위 선분이 정확히 그 자리에서 끝난다.
            return new ArcheryShot(shooterId, fireTick, origin, new Vector3(0f, 0f, distance / seconds));
        }

        //  함정만 든 판. 비율을 1로 두고 함정 종류를 하나만 넣으면 뜨는 과녁이 전부 그것이다.
        static ArcheryConfig TrapOnlyConfig()
        {
            var kinds = new[] { new ArcheryTargetKind(0.50f, -5, 100, true, ArcheryTargetShape.Sphere, null) };
            return new ArcheryConfig(
                wavePeriodTicks: 88, minTargets: 2, maxTargets: 3,
                spawnRadius: 2f, spawnMinY: 2f, spawnMaxY: 6f, minSeparation: 1.0f,
                trapRatioMin: 1f, trapRatioMax: 1f,
                shakeFreeSeconds: 1f, shakeRampSeconds: 2f, shakeMaxDegrees: 3f,
                riseHeightMin: 1.2f, riseHeightMax: 2.4f, staggerTicks: 12, restTicks: 20,
                kinds: kinds);
        }

        //  함정 점수를 양수로 적어 둔 데이터. ArcheryHitRules가 부호를 정규화하므로 결과는
        //  음수로 적었을 때와 같아야 한다 — 이 설정이 "그냥 Points를 그대로 싣는" 옛 방식과
        //  진짜 위임을 갈라 준다(음수 데이터로는 둘이 같은 값을 내서 안 갈린다).
        static ArcheryConfig PositiveTrapConfig()
        {
            var kinds = new[] { new ArcheryTargetKind(0.50f, 5, 100, true, ArcheryTargetShape.Sphere, null) };
            return new ArcheryConfig(
                wavePeriodTicks: 88, minTargets: 2, maxTargets: 3,
                spawnRadius: 2f, spawnMinY: 2f, spawnMaxY: 6f, minSeparation: 1.0f,
                trapRatioMin: 1f, trapRatioMax: 1f,
                shakeFreeSeconds: 1f, shakeRampSeconds: 2f, shakeMaxDegrees: 3f,
                riseHeightMin: 1.2f, riseHeightMax: 2.4f, staggerTicks: 12, restTicks: 20,
                kinds: kinds);
        }

        [Test]
        public void 과녁을_지나간_화살은_점수가_된다()
        {
            var f = Build(StartTick);
            f.Archer("a");
            var target = f.TargetsOfWave(0)[0];

            f.World.IngestRemoteShot(ShotThrough("a", StartTick, target, 1.0f));
            f.System.Tick(StartTick + 1, TickInterval);

            Assert.AreEqual(target.Points, f.ScoreOf("a"));
        }

        [Test]
        public void 적중은_사건으로도_남는다()
        {
            var f = Build(StartTick);
            f.Archer("a");
            var target = f.TargetsOfWave(0)[0];

            f.World.IngestRemoteShot(ShotThrough("a", StartTick, target, 1.0f));
            f.System.Tick(StartTick + 1, TickInterval);

            Assert.AreEqual(1, f.HitEventCount());
        }

        [Test]
        public void 먹힌_과녁은_웨이브_상태에_남는다()
        {
            //  이 마스크가 곧 클라에 나가는 값이다 — 사건을 놓친(재접속한) 사람은 이것만 보고
            //  어느 과녁이 사라졌는지 안다.
            var f = Build(StartTick);
            f.Archer("a");
            var target = f.TargetsOfWave(0)[0];

            f.World.IngestRemoteShot(ShotThrough("a", StartTick, target, 1.0f));
            f.System.Tick(StartTick + 1, TickInterval);

            Assert.AreEqual(0, f.WaveState.WaveIndex);
            Assert.IsTrue(f.WaveState.IsConsumed(target.SlotIndex));
        }

        [Test]
        public void 웨이브가_넘어가면_먹힌_기록이_비워진다()
        {
            var f = Build(StartTick);
            f.Archer("a");
            var target = f.TargetsOfWave(0)[0];

            f.World.IngestRemoteShot(ShotThrough("a", StartTick, target, 1.0f));
            f.System.Tick(StartTick + 1, TickInterval);
            //  다음 웨이브로 넘긴다(주기 88틱).
            f.System.Tick(StartTick + 88, TickInterval);

            Assert.AreEqual(1, f.WaveState.WaveIndex);
            Assert.AreEqual(0, f.WaveState.ConsumedMask);
        }

        [Test]
        public void 먹힌_과녁은_두_번_먹히지_않는다()
        {
            var f = Build(StartTick);
            f.Archer("a");
            f.Archer("b");
            var target = f.TargetsOfWave(0)[0];

            f.World.IngestRemoteShot(ShotThrough("a", StartTick, target, 1.0f));
            f.System.Tick(StartTick + 1, TickInterval);

            //  b가 한 틱 뒤에 같은 자리를 지나가도 이미 사라진 과녁이다.
            f.World.IngestRemoteShot(ShotThrough("b", StartTick + 1, target, 1.0f));
            f.System.Tick(StartTick + 2, TickInterval);

            Assert.AreEqual(target.Points, f.ScoreOf("a"));
            Assert.AreEqual(0, f.ScoreOf("b"));
        }

        [Test]
        public void 같은_틱에_두_발이_닿으면_먼저_닿은_쪽이_먹는다()
        {
            var f = Build(StartTick);
            f.Archer("near");
            f.Archer("far");
            var target = f.TargetsOfWave(0)[0];

            //  둘 다 이번 틱에 과녁 중심에서 끝나지만, 가까이서 출발한 쪽이 선분 위에서 먼저 닿는다
            //  (1 − r/d 가 d가 커질수록 크다).
            f.World.IngestRemoteShot(ShotThrough("far", StartTick, target, 2.0f));
            f.World.IngestRemoteShot(ShotThrough("near", StartTick, target, 1.0f));
            f.System.Tick(StartTick + 1, TickInterval);

            Assert.AreEqual(target.Points, f.ScoreOf("near"));
            Assert.AreEqual(0, f.ScoreOf("far"));
        }

        [Test]
        public void 맞은_화살은_다른_과녁을_또_맞히지_않는다()
        {
            var f = Build(StartTick);
            f.Archer("a");
            var targets = f.TargetsOfWave(0);

            //  첫 과녁을 먹은 화살이, 다음 틱에 둘째 과녁 자리에 있어도 다시 먹지 않는다.
            //  (실제로 두 과녁을 잇는 궤적을 만들기 어려우므로, 같은 화살을 두 틱 굴려
            //   점수가 한 번만 오르는 것으로 확인한다.)
            f.World.IngestRemoteShot(ShotThrough("a", StartTick, targets[0], 1.0f));
            f.System.Tick(StartTick + 1, TickInterval);
            f.System.Tick(StartTick + 2, TickInterval);

            Assert.AreEqual(targets[0].Points, f.ScoreOf("a"));
        }

        [Test]
        public void 빗나간_화살은_아무_일도_안_만든다()
        {
            var f = Build(StartTick);
            f.Archer("a");
            var target = f.TargetsOfWave(0)[0];

            //  과녁보다 100m 위를 지나간다.
            var origin = target.Origin + new Vector3(0f, 100f, -1f);
            f.World.IngestRemoteShot(new ArcheryShot("a", StartTick, origin, new Vector3(0f, 0f, 50f)));
            f.System.Tick(StartTick + 1, TickInterval);

            Assert.AreEqual(0, f.ScoreOf("a"));
            Assert.AreEqual(0, f.HitEventCount());
        }

        [Test]
        public void 출발_전에는_판정하지_않는다()
        {
            var f = Build(long.MaxValue);       // 아직 출발 틱을 모른다
            f.Archer("a");

            //  웨이브가 없으므로 과녁 자리도 없다 — 원점을 지나는 화살을 넣어 본다.
            f.World.IngestRemoteShot(new ArcheryShot("a", 0, new Vector3(0f, 3f, -1f), new Vector3(0f, 0f, 50f)));
            f.System.Tick(10, TickInterval);

            Assert.AreEqual(0, f.ScoreOf("a"));
            Assert.AreEqual(0, f.HitEventCount());
        }

        [Test]
        public void 몸이_사라진_사람의_화살도_판을_죽이지_않는다()
        {
            var f = Build(StartTick);           // 쏜 사람을 등록하지 않는다(나간 사람)
            var target = f.TargetsOfWave(0)[0];

            f.World.IngestRemoteShot(ShotThrough("gone", StartTick, target, 1.0f));

            Assert.DoesNotThrow(() => f.System.Tick(StartTick + 1, TickInterval));
            Assert.AreEqual(1, f.HitEventCount());   // 과녁은 먹힌다 — 점수만 갈 데가 없을 뿐
        }

        [Test]
        public void 함정을_맞히면_점수가_깎인다()
        {
            var f = Build(StartTick, TrapOnlyConfig());
            f.Archer("a");
            var target = f.TargetsOfWave(0)[0];
            Assert.IsTrue(target.IsTrap, "함정만 든 설정인데 성한 과녁이 떴다");

            f.World.IngestRemoteShot(ShotThrough("a", StartTick, target, 1.0f));
            f.System.Tick(StartTick + 1, TickInterval);

            Assert.AreEqual(-5, f.ScoreOf("a"));
            Assert.AreEqual(0, f.Registry.Get("a").Get<ArcheryScore>().Gained);
            Assert.AreEqual(5, f.Registry.Get("a").Get<ArcheryScore>().Lost);
        }

        //  연출용 값도 부호가 맞아야 화면에 "-5"로 뜬다.
        [Test]
        public void 함정_적중_사건은_음수를_싣는다()
        {
            var f = Build(StartTick, TrapOnlyConfig());
            f.Archer("a");
            var target = f.TargetsOfWave(0)[0];

            f.World.IngestRemoteShot(ShotThrough("a", StartTick, target, 1.0f));
            f.System.Tick(StartTick + 1, TickInterval);

            Assert.AreEqual(1, f.HitEventCount());
            Assert.AreEqual(-5, f.LastHitPoints());
        }

        [Test]
        public void 함정_점수를_양수로_적어도_벌점이_되고_사건은_음수를_싣는다()
        {
            var f = Build(StartTick, PositiveTrapConfig());
            f.Archer("a");
            var target = f.TargetsOfWave(0)[0];
            Assert.IsTrue(target.IsTrap, "함정만 든 설정인데 성한 과녁이 떴다");
            Assert.AreEqual(5, target.Points, "이 판의 함정은 점수를 양수로 적어 둔 것이어야 한다");

            f.World.IngestRemoteShot(ShotThrough("a", StartTick, target, 1.0f));
            f.System.Tick(StartTick + 1, TickInterval);

            Assert.AreEqual(-5, f.ScoreOf("a"));
            Assert.AreEqual(0, f.Registry.Get("a").Get<ArcheryScore>().Gained);
            Assert.AreEqual(5, f.Registry.Get("a").Get<ArcheryScore>().Lost);
            Assert.AreEqual(-5, f.LastHitPoints());
        }

        //  성한 과녁은 예전 그대로여야 한다 — 규칙 함수를 끼우면서 획득이 벌점 칸으로 새면
        //  합계는 맞고 결과 화면의 내역만 틀린다(눈에 안 띈다).
        [Test]
        public void 성한_과녁은_획득_칸에만_쌓인다()
        {
            var f = Build(StartTick);
            f.Archer("a");
            var target = f.TargetsOfWave(0)[0];

            f.World.IngestRemoteShot(ShotThrough("a", StartTick, target, 1.0f));
            f.System.Tick(StartTick + 1, TickInterval);

            Assert.AreEqual(target.Points, f.Registry.Get("a").Get<ArcheryScore>().Gained);
            Assert.AreEqual(0, f.Registry.Get("a").Get<ArcheryScore>().Lost);
        }

        //  과녁이 움직이므로 "지금 있는 자리"로 쏘면 빗나간다 — 닿을 때 있을 자리를 노려야 한다.
        [Test]
        public void 움직이는_과녁도_맞힐_수_있다()
        {
            var f = Build(StartTick);
            f.Archer("a");
            var target = f.TargetsOfWave(0)[0];

            //  솟는 도중의 한 시점을 노린다.
            long hitTick = target.SpawnTick + 20;
            f.World.IngestRemoteShot(ShotThroughMoving("a", hitTick - 1, target, hitTick, 1.0f));
            f.System.Tick(hitTick, TickInterval);

            Assert.AreEqual(target.Points, f.ScoreOf("a"));
        }

        //  솟기 전 과녁은 아직 무대에 박혀 있다 — 그 자리를 쏴도 맞으면 안 된다.
        //
        //  **첫 슬롯으로는 이걸 잴 수 없다.** 슬롯 0은 웨이브가 시작하는 그 틱에 솟으므로
        //  "솟기 전"이라는 시점 자체가 없다. 뒤 슬롯은 12틱씩 밀려 솟으니 그 사이를 노린다.
        [Test]
        public void 솟기_전_과녁은_못_맞힌다()
        {
            var f = Build(StartTick);
            f.Archer("a");
            var targets = f.TargetsOfWave(0);
            Assert.Greater(targets.Count, 1, "뒤 슬롯이 있어야 '솟기 전'을 잴 수 있다");

            var target = targets[1];
            long earlyTick = target.SpawnTick - 5;
            Assert.Greater(earlyTick, StartTick, "노린 시점이 웨이브 시작보다 앞서면 안 된다");

            //  아직 안 솟았으므로 과녁은 출발점에 있다. 그 자리를 정확히 지나가게 쏜다.
            f.World.IngestRemoteShot(ShotThrough("a", earlyTick - 1, target, 1.0f));
            f.System.Tick(earlyTick, TickInterval);

            Assert.AreEqual(0, f.ScoreOf("a"));
        }

        //  떨어진 과녁도 마찬가지다. 수명이 지나면 무대 아래로 사라진 것이다.
        [Test]
        public void 떨어진_과녁은_못_맞힌다()
        {
            var f = Build(StartTick);
            f.Archer("a");
            var target = f.TargetsOfWave(0)[0];

            long lateTick = target.SpawnTick + Mathf.CeilToInt(target.LifetimeSeconds / TickInterval) + 5;
            f.World.IngestRemoteShot(ShotThroughMoving("a", lateTick - 1, target, lateTick, 1.0f));
            f.System.Tick(lateTick, TickInterval);

            Assert.AreEqual(0, f.ScoreOf("a"));
        }

        //  "한 틱 동안 과녁이 정지한 것으로 봐도 된다"는 근사에 기대고 있다. 그 전제는
        //  과녁이 한 틱에 자기 반지름보다 적게 움직인다는 것이다 — 높이를 올리면 깨진다.
        //  가장 높이 솟는 경우로 재야 한다(그게 제일 빠르다).
        [Test]
        public void 과녁은_한_틱에_자기_반지름보다_적게_움직인다()
        {
            var config = Config();
            float perTick = ArcheryTargetMotion.RiseSpeedFor(config.RiseHeightMax) * TickInterval;

            float smallest = float.MaxValue;
            for (int i = 0; i < config.Kinds.Count; i++)
            {
                smallest = Mathf.Min(smallest, config.Kinds[i].Radius);
            }

            Assert.Less(perTick, smallest,
                $"가장 높이 솟는 과녁이 한 틱에 {perTick:F3}m 움직이는데 가장 작은 과녁 반지름이 "
                + $"{smallest:F3}m다 — 판정이 과녁을 뚫고 지나갈 수 있다. rise_height_max를 낮추거나 "
                + "가장 작은 과녁을 키워야 한다");
        }

        //  판정이 "어디에 맞았나"를 채점까지 흘려보내야 동심원 과녁의 띠가 갈린다.
        //  지금 배포 과녁은 띠가 하나라 점수가 안 바뀌므로, 여기서만 판 과녁을 만들어 확인한다.
        [Test]
        public void 판_과녁은_맞은_자리에_따라_점수가_갈린다()
        {
            var bands = new System.Collections.Generic.List<ArcheryRingBand>
            {
                new ArcheryRingBand(0.25f, 10),
                new ArcheryRingBand(1.0f, 3),
            };

            var target = new ArcheryTarget(
                waveIndex: 0, slotIndex: 0,
                origin: Vector3.zero, riseSpeed: 0f, spawnTick: 0L,
                radius: 0.4f, points: 0, isTrap: false,
                shape: ArcheryTargetShape.Face, bands: bands,
                facing: new Vector3(0f, 0f, -1f),
                lifetimeSeconds: 10f, ownerUserId: string.Empty,
                lateralSpan: 0f, lateralPeriod: 0f);

            //  정중앙을 지나는 선분과, 가장자리 쪽을 지나는 선분.
            ArcheryHitTest.SegmentHitsTarget(
                new Vector3(0f, 0f, -1f), new Vector3(0f, 0f, 1f),
                Vector3.zero, target, out _, out float centerOffset);
            ArcheryHitTest.SegmentHitsTarget(
                new Vector3(0.3f, 0f, -1f), new Vector3(0.3f, 0f, 1f),
                Vector3.zero, target, out _, out float edgeOffset);

            Assert.AreEqual(10, ArcheryHitRules.Resolve(target, centerOffset).Gained);
            Assert.AreEqual(3, ArcheryHitRules.Resolve(target, edgeOffset).Gained);
        }

        //  판정이 구한 '맞은 자리'가 채점까지 실제로 흘러가는지를 **시스템을 지나가며** 잰다.
        //  다른 테스트들은 판정 함수와 채점 함수를 각각 직접 불러서, 둘이 배선됐는지는 못 본다.
        //
        //  공은 겉면에 맞으므로 맞은 자리가 늘 가장자리(1에 가깝다)다. 그래서 띠를 둘 주면
        //  **바깥 띠**가 나와야 한다 — 배선이 끊겨 0이 넘어가면 안쪽 띠가 나온다.
        [Test]
        public void 시스템이_맞은_자리를_채점까지_넘긴다()
        {
            const int Inner = 100;   // 배선이 끊기면 이 값이 나온다
            const int Outer = 7;     // 제대로 흘러가면 이 값이 나온다

            var bands = new List<ArcheryRingBand>
            {
                new ArcheryRingBand(0.5f, Inner),
                new ArcheryRingBand(1.0f, Outer),
            };
            var kinds = new[]
            {
                new ArcheryTargetKind(0.5f, Inner, 100, false, ArcheryTargetShape.Sphere, bands),
            };

            var f = Build(StartTick, Build(kinds, minSeparation: 1.5f));
            f.Archer("a");
            var target = f.TargetsOfWave(0)[0];

            long hitTick = target.SpawnTick + 10;
            f.World.IngestRemoteShot(ShotThroughMoving("a", hitTick - 1, target, hitTick, 1.0f));
            f.System.Tick(hitTick, TickInterval);

            Assert.AreEqual(Outer, f.ScoreOf("a"),
                            "안쪽 띠 점수가 나오면 맞은 자리가 채점에 안 넘어간 것이다");
        }

        [Test]
        public void 남의_과녁은_맞혀도_아무_일이_없다()
        {
            using (var scene = new RangeScene(laneCount: 2, distance: 20f))
            {
                var f = BuildRange(StartTick, scene, "user-a", "user-b");
                f.Archer("e-a", "user-a");

                //  슬롯 1은 user-b의 과녁이다. user-a의 화살이 한가운데를 지나가게 쏜다.
                var theirs = f.TargetsOfWave(0)[1];
                f.World.IngestRemoteShot(ShotThrough("e-a", StartTick, theirs, 1.0f));
                f.System.Tick(StartTick + 1, TickInterval);

                Assert.AreEqual(0, f.ScoreOf("e-a"), "남의 과녁으로 점수가 났다");
                Assert.IsFalse(f.WaveState.IsConsumed(theirs.SlotIndex),
                    "남의 화살에 과녁이 사라졌다 — 태워 버리는 방해가 열린다");
            }
        }

        [Test]
        public void 주인이_맞히면_점수가_나고_과녁이_사라진다()
        {
            using (var scene = new RangeScene(laneCount: 2, distance: 20f))
            {
                var f = BuildRange(StartTick, scene, "user-a", "user-b");
                f.Archer("e-b", "user-b");

                //  바로 위 시험과 **같은 판, 같은 과녁, 같은 화살**이고 쏜 사람만 다르다.
                var theirs = f.TargetsOfWave(0)[1];
                f.World.IngestRemoteShot(ShotThrough("e-b", StartTick, theirs, 1.0f));
                f.System.Tick(StartTick + 1, TickInterval);

                Assert.Greater(f.ScoreOf("e-b"), 0, "주인이 맞혔는데 점수가 안 났다");
                Assert.IsTrue(f.WaveState.IsConsumed(theirs.SlotIndex));
            }
        }
    }
}
