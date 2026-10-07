using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// 게임 규칙이 낸 등수에 "끝까지 안 돌아온 사람은 꼴찌"를 덮는다. 다섯 게임의 등수 코드를 각각 고치지 않고
    /// 판이 끝나는 한 곳(LOPRunner.EndMatch)에서 적용한다.
    /// 남은 사람은 원래 순서·동점을 지킨 채 등수를 다시 매기고(빠진 자리만큼 당겨진다), 그 아래에 나간 사람을
    /// 늦게 나간 순으로 한 명씩, 맨 아래에 한 번도 안 들어온 사람을 같은 등수로 붙인다.
    /// 명단 전원이 정확히 한 번 들어간다 — 로비는 명단과 다르면 결과 전체를 거절한다.
    /// </summary>
    public static class LeaverRanking
    {
        public static MatchOutcome Apply(MatchOutcome outcome, IReadOnlyList<string> leftLatestFirst, IReadOnlyList<string> neverJoined)
        {
            var byUser = new Dictionary<string, MatchPlacement>();
            foreach (var p in outcome.placements) byUser[p.userId] = p;

            var away = new HashSet<string>(leftLatestFirst);
            foreach (var u in neverJoined) away.Add(u);

            var stayed = new List<MatchPlacement>();
            foreach (var p in outcome.placements)
            {
                if (away.Contains(p.userId) == false) stayed.Add(p);
            }
            stayed.Sort((x, y) => x.placement.CompareTo(y.placement));

            var result = new MatchOutcome();
            int place = 0;
            int previousOriginal = int.MinValue;
            for (int i = 0; i < stayed.Count; i++)
            {
                //  원래 동점이면 같은 등수, 아니면 앞에 선 사람 수 + 1(1·1·3에서 하나 빠지면 1·2).
                if (stayed[i].placement != previousOriginal) place = i + 1;
                previousOriginal = stayed[i].placement;
                result.placements.Add(Copy(stayed[i], place));
            }

            int next = stayed.Count;
            foreach (var userId in leftLatestFirst)
            {
                if (byUser.TryGetValue(userId, out var p)) result.placements.Add(Copy(p, ++next));
            }

            int last = next + 1;
            foreach (var userId in neverJoined)
            {
                //  두 목록은 PlayerPresence에서 서로 겹치지 않지만, 겹쳐 들어와도 한 사람이 두 번 보고되지 않게.
                if (Contains(leftLatestFirst, userId)) continue;
                if (byUser.TryGetValue(userId, out var p)) result.placements.Add(Copy(p, last));
            }

            return result;
        }

        private static bool Contains(IReadOnlyList<string> list, string value)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == value) return true;
            }
            return false;
        }

        private static MatchPlacement Copy(MatchPlacement p, int placement) =>
            new MatchPlacement { userId = p.userId, placement = placement, stats = p.stats };
    }
}
