using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 화살이 과녁에 닿았는지 매 틱 판정하고, 닿았으면 점수를 주고 그 과녁을 치운다.
    ///
    /// <para><b>서버에서만 돈다.</b> 클라는 적중을 예측하지 않는다 — 과녁이 두세 개뿐이라 남이 먼저
    /// 맞혔을 확률이 높고, 미리 띄운 점수가 눈앞에서 취소되는 것은 아예 안 보여 주는 것만 못하다
    /// (2026-07-12에 같은 이유로 클라 데미지 예측을 짓지 않기로 했다).</para>
    ///
    /// <para>과녁이 <b>뜨는</b> 것은 양쪽이 씨앗으로 계산하므로 통신하지 않는다. 여기서 통신하는 것은
    /// "누가 먼저 먹었나" 하나뿐이다.</para>
    /// </summary>
    public class ArcheryHitSystem : GameFramework.Runner.ITickSystem
    {
        private readonly ArcheryWorld world;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly GameFramework.World.WorldEventBuffer eventBuffer;
        private readonly ArcheryCourse course;
        private readonly float tickInterval;

        private readonly ArcheryWaveState waveState;

        // 이미 무언가를 맞힌 화살. 서버는 되감지 않으므로 그냥 필드다
        // (월드의 저장/복원에 넣으면 서버가 확정한 사실이 되감기에 되살아난다).
        private readonly HashSet<(string shooterId, long fireTick)> spentArrows
            = new HashSet<(string, long)>();

        private readonly List<ArcheryTarget> targets = new List<ArcheryTarget>();
        private readonly List<Candidate> candidates = new List<Candidate>();

        private readonly struct Candidate
        {
            public readonly float T;

            /// <summary>맞은 자리가 중심에서 얼마나 벗어났나(0~1). 채점이 띠를 찾는 데 쓴다.</summary>
            public readonly float Offset;

            public readonly string ShooterId;
            public readonly long FireTick;
            public readonly int Slot;

            public Candidate(float t, float offset, string shooterId, long fireTick, int slot)
            {
                T = t; Offset = offset; ShooterId = shooterId; FireTick = fireTick; Slot = slot;
            }
        }

        public ArcheryHitSystem(ArcheryWorld world,
                                GameFramework.World.EntityRegistry entityRegistry,
                                GameFramework.World.WorldEventBuffer eventBuffer,
                                ArcheryCourse course,
                                ArcheryWaveState waveState,
                                float tickInterval)
        {
            this.world = world;
            this.entityRegistry = entityRegistry;
            this.eventBuffer = eventBuffer;
            this.course = course;
            this.waveState = waveState;
            this.tickInterval = tickInterval;
        }

        public void Tick(long tick, float deltaTime)
        {
            int step = course.IndexAt(tick, world.GameplayStartTick);
            if (step < 0)
            {
                return;   // 아직 출발 전
            }
            //  사거리 코스는 순서가 끝나면 더 이상 과녁이 없다(웨이브 맵은 StepCount가 0이라 안 걸린다).
            if (course.StepCount > 0 && step >= course.StepCount)
            {
                return;
            }

            if (step != waveState.WaveIndex)
            {
                course.Fill(targets, step, world.GameplayStartTick);
                // 지난 묶음의 과녁은 이미 사라졌다 — 기록을 들고 있을 이유가 없다.
                waveState.BeginWave(step);
            }

            CollectCandidates(tick);
            ApplyCandidates();
            ForgetOldArrows(tick);
        }

        // 먼저 다 모은다 — 찾는 대로 바로 적용하면 엔티티 순회 순서가 승자를 정하게 된다.
        private void CollectCandidates(long tick)
        {
            candidates.Clear();

            var shots = world.Shots;
            for (int s = 0; s < shots.Count; s++)
            {
                var shot = shots[s];
                if (spentArrows.Contains((shot.ShooterId, shot.FireTick)))
                {
                    continue;
                }

                //  이번 틱에 지나온 선분. 쏜 바로 그 틱이면 출발점에서 한 틱만큼 나아간 구간이다.
                float toSeconds = (tick - shot.FireTick) * tickInterval;
                float fromSeconds = Mathf.Max(toSeconds - tickInterval, 0f);
                if (toSeconds < 0f)
                {
                    continue;   // 아직 떠나지 않은 화살(되감기 중에만 생긴다)
                }

                Vector3 from = ArcheryTrajectory.PositionAt(shot, fromSeconds);
                Vector3 to = ArcheryTrajectory.PositionAt(shot, toSeconds);

                //  누가 쏜 화살인가. 예약된 과녁은 주인만 가져간다 —
                //  엔티티 id가 아니라 userId로 비교한다(과녁은 매치 시작 명단으로 예약되므로).
                string shooterUserId = entityRegistry.Get(shot.ShooterId)
                    ?.Get<GameFramework.World.Ownership>()?.OwnerId ?? string.Empty;

                for (int i = 0; i < targets.Count; i++)
                {
                    if (waveState.IsConsumed(targets[i].SlotIndex))
                    {
                        continue;
                    }

                    //  남의 과녁이면 여기서 끝난다 — 점수도 없고 과녁도 안 사라진다.
                    //  (사라지게 두면 남의 과녁을 태워 버리는 방해가 열린다.)
                    if (ArcheryHitRules.CanTake(targets[i], shooterUserId) == false)
                    {
                        continue;
                    }

                    //  화살 선분은 [tick-1, tick] 구간이다. 과녁을 tick에서만 재면 반 틱 어긋나므로
                    //  구간 가운데를 그 구간의 대표 시각으로 삼는다. 한 틱에 과녁이 자기 반지름보다
                    //  적게 움직이므로 그 사이 정지한 것으로 봐도 된다(테스트가 그 조건을 지킨다).
                    //
                    //  있는 자리와 살아 있는지를 **같은 시각**으로 묻는다. 다른 시각으로 물으면
                    //  뜨고 지는 경계에서 "자리는 여기인데 이미 죽었다"가 되어, 반 틱(10ms)만큼
                    //  맞았는데 점수가 안 나는 구간이 생긴다.
                    double at = tick - 0.5;

                    //  아직 안 솟았거나 이미 떨어진 과녁은 없는 것이다.
                    if (ArcheryTargetMotion.IsAlive(targets[i], at, tickInterval) == false)
                    {
                        continue;
                    }

                    Vector3 targetAt = ArcheryTargetMotion.PositionAt(targets[i], at, tickInterval);

                    if (ArcheryHitTest.SegmentHitsTarget(from, to, targetAt, targets[i],
                                                         out float t, out float offset))
                    {
                        candidates.Add(new Candidate(t, offset, shot.ShooterId, shot.FireTick,
                                                     targets[i].SlotIndex));
                    }
                }
            }
        }

        private void ApplyCandidates()
        {
            if (candidates.Count == 0)
            {
                return;
            }

            //  먼저 닿은 화살이 먹는다. 완전히 같은 시각이면 쏜 사람 id로 갈라 결과를 못박는다
            //  (안 그러면 목록 순서, 즉 엔티티 순회 순서가 승자를 정한다).
            candidates.Sort((a, b) =>
            {
                int byTime = a.T.CompareTo(b.T);
                return byTime != 0 ? byTime : string.CompareOrdinal(a.ShooterId, b.ShooterId);
            });

            for (int i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (spentArrows.Contains((candidate.ShooterId, candidate.FireTick)))
                {
                    continue;   // 이 화살은 이 틱에 이미 다른 과녁을 먹었다
                }
                //  TryConsume이 거짓이면 이 틱에 더 먼저 닿은 화살이 이미 먹은 것이다.
                if (waveState.TryConsume(candidate.Slot) == false)
                {
                    continue;
                }

                //  무슨 일이 일어나는지는 여기서 정하지 않는다 — 공유 규칙 함수 하나가 정한다.
                var outcome = ArcheryHitRules.Resolve(TargetOfSlot(candidate.Slot), candidate.Offset);
                spentArrows.Add((candidate.ShooterId, candidate.FireTick));

                var score = entityRegistry.Get(candidate.ShooterId)?.Get<ArcheryScore>();
                if (score != null)
                {
                    score.Gained += outcome.Gained;
                    score.Lost += outcome.Lost;
                }

                eventBuffer.Append(new ArcheryTargetHitEvent(
                    candidate.ShooterId, candidate.FireTick, outcome.Delta));
            }
        }

        private ArcheryTarget TargetOfSlot(int slot)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i].SlotIndex == slot)
                {
                    return targets[i];
                }
            }
            return default;
        }

        // 수명이 다한 화살은 목록에서도 사라지므로 기록을 들고 있을 이유가 없다.
        private void ForgetOldArrows(long tick)
        {
            long lifetimeTicks = (long)(ArcheryTrajectory.LifetimeSeconds / tickInterval) + 1;
            spentArrows.RemoveWhere(a => tick - a.fireTick > lifetimeTicks);
        }
    }
}
