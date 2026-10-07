using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// 이 방의 참가자 중 누가 들어왔고, 누가 판 도중 끊겼나. 롤처럼 끊겨도 판이 끝나기 전에 돌아오면 이어서 하고,
    /// 끝까지 안 돌아오면 꼴찌다 — 그 판단과 "나가 있는 사람은 기다리지 않는다"의 근거가 된다.
    /// 끊긴 순서는 틱이 아니라 순번으로 센다(순서만 필요하다).
    /// </summary>
    public class PlayerPresence
    {
        private readonly HashSet<string> roster = new HashSet<string>();
        private readonly HashSet<string> joined = new HashSet<string>();
        private readonly Dictionary<string, long> leftOrder = new Dictionary<string, long>();
        private long nextOrder;
        private bool matchStarted;

        /// <summary>판이 출발했다. 그 전엔 <see cref="IsAway"/>가 늘 false — 기다리지 않기를 시작 전엔 안 건다.</summary>
        public void MarkMatchStarted() => matchStarted = true;

        public void Begin(IReadOnlyList<string> playerList)
        {
            roster.Clear();
            joined.Clear();
            leftOrder.Clear();
            nextOrder = 0;
            matchStarted = false;
            foreach (var userId in playerList) roster.Add(userId);
        }

        /// <summary>접속(재접속 포함). 나감 기록을 지운다 — 돌아온 사람은 평소대로 등수를 받는다.</summary>
        public void MarkJoined(string userId)
        {
            if (roster.Contains(userId) == false) return;
            joined.Add(userId);
            leftOrder.Remove(userId);
        }

        /// <summary>들어왔던 사람이 끊김. 안 들어온 채인 사람은 "안 들어옴"으로 남는다.</summary>
        public void MarkLeft(string userId)
        {
            if (joined.Contains(userId) == false) return;
            leftOrder[userId] = nextOrder++;
        }

        /// <summary>
        /// 들어왔다가 지금 끊겨 있나 — 판이 이 사람을 기다리지 말아야 하는지. 한 번도 안 들어온 사람은 아니다:
        /// 그들까지 빼면 시작 전(아무도 안 들어온 순간)에 "남은 사람 없음"으로 판이 끝난다.
        /// </summary>
        public bool IsAway(string userId) => matchStarted && leftOrder.ContainsKey(userId);

        /// <summary>지금 끊겨 있는 사람, 늦게 나간 순(등수가 위인 순).</summary>
        public IReadOnlyList<string> LeftLatestFirst
        {
            get
            {
                var list = new List<string>(leftOrder.Keys);
                list.Sort((x, y) => leftOrder[y].CompareTo(leftOrder[x]));
                return list;
            }
        }

        /// <summary>명단에 있는데 한 번도 들어온 적 없는 사람.</summary>
        public IReadOnlyList<string> NeverJoined
        {
            get
            {
                var list = new List<string>();
                foreach (var userId in roster)
                {
                    if (joined.Contains(userId) == false) list.Add(userId);
                }
                list.Sort(string.CompareOrdinal);
                return list;
            }
        }
    }
}
