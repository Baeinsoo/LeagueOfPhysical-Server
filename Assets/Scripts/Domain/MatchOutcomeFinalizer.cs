using System;
using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// 등수 확정(<see cref="LeaverRanking"/>)과 플레이 시간 기록(<see cref="PlayedSeconds"/>)을 한 곳에 묶는다.
    /// 둘 다 로비가 보상을 계산하는 유일한 입력이라, LOPRunner.EndMatch에 흩어 두면 한쪽을 지워도
    /// 아무 테스트도 안 깨진다 — 여기 한 곳으로 모아 테스트로 지킨다.
    /// </summary>
    public static class MatchOutcomeFinalizer
    {
        public static MatchOutcome Finalize(
            MatchOutcome raw,
            IReadOnlyList<string> leftLatestFirst,
            IReadOnlyList<string> neverJoined,
            Func<string, bool> isSettled,
            long endTick,
            long startTick,
            double interval)
        {
            MatchOutcome outcome = LeaverRanking.Apply(raw, leftLatestFirst, neverJoined, isSettled);

            int playedSeconds = PlayedSeconds.Compute(endTick, startTick, interval);
            foreach (var placement in outcome.placements)
            {
                placement.playedSeconds = playedSeconds;
            }

            return outcome;
        }
    }
}
