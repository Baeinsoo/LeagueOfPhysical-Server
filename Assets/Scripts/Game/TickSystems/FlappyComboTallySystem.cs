using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// 새마다 연속 날갯짓(<see cref="FlappyFlapCombo"/>)이 고비를 넘은 횟수를 판 내내 센다. 판 끝에 결과 자루로 로비에 가서 코인이 된다(spec 2026-10-28). 가장 긴 연속과 기절 횟수도 함께 센다 — 로비 퀘스트가 읽는다(spec 2026-10-30 §4).
    ///
    /// <para><b>왜 몸 밖에 두나:</b> 추격자에게 잡히면 몸이 사라져 컴포넌트도 같이 사라진다. 센 것은 남아야 한다
    /// (<see cref="FinishTrackingSystem"/>이 완주 기록을 몸 밖에 두는 이유와 같다).</para>
    ///
    /// <para><b>왜 서버만:</b> 서버는 되감지 않아 매 틱 한 번 보면 된다. 클라는 이 수가 필요 없다 — 시뮬·되감기 사진·스냅은 그대로 둔다.</para>
    /// </summary>
    public class FlappyComboTallySystem : GameFramework.Runner.ITickSystem
    {
        private readonly GameFramework.World.EntityRegistry entityRegistry;

        private readonly List<string> watched = new List<string>();
        private readonly Dictionary<string, string> userOf = new Dictionary<string, string>();
        private readonly Dictionary<string, int> lastCount = new Dictionary<string, int>();
        //  사람별(userId) — 몸이 사라져도, 혹시 같은 사람이 새 몸을 받아도 한 줄로 모인다.
        private readonly Dictionary<string, int[]> crossings = new Dictionary<string, int[]>();
        private readonly Dictionary<string, bool> lastStunned = new Dictionary<string, bool>();   //  entityId
        private readonly Dictionary<string, int> best = new Dictionary<string, int>();             //  userId
        private readonly Dictionary<string, int> stuns = new Dictionary<string, int>();            //  userId

        public FlappyComboTallySystem(GameFramework.World.EntityRegistry entityRegistry)
        {
            this.entityRegistry = entityRegistry;
        }

        public void Watch(string entityId, string userId)
        {
            if (userOf.ContainsKey(entityId))
            {
                return;
            }
            watched.Add(entityId);
            userOf[entityId] = userId;
            lastCount[entityId] = 0;
            lastStunned[entityId] = false;
            if (crossings.ContainsKey(userId) == false)
            {
                crossings[userId] = new int[FlappyComboStatKeys.Thresholds.Length];
                best[userId] = 0;
                stuns[userId] = 0;
            }
        }

        public void Reset()
        {
            watched.Clear();
            userOf.Clear();
            lastCount.Clear();
            crossings.Clear();
            lastStunned.Clear();
            best.Clear();
            stuns.Clear();
        }

        public void Tick(long tick, float deltaTime)
        {
            for (int i = 0; i < watched.Count; i++)
            {
                string entityId = watched[i];
                var entity = entityRegistry.Get(entityId);
                if (entity == null)
                {
                    continue;   //  잡혀 몸이 없다 — 센 것은 그대로 둔다
                }
                string userId = userOf[entityId];
                var combo = entity.Get<FlappyFlapCombo>();
                if (combo != null)
                {
                    FlappyComboTally.Observe(lastCount[entityId], combo.Count, FlappyComboStatKeys.Thresholds, crossings[userId]);
                    lastCount[entityId] = combo.Count;
                    if (combo.Count > best[userId]) best[userId] = combo.Count;
                }
                //  기절은 "들어간 틱"을 센다. 연속이 줄어드는 것으로 세면 연속 0에서 기절했을 때 놓친다.
                //  기절 1.2초 + 무적이라 한 틱에 두 번 들어갈 수 없다.
                var stun = entity.Get<FlappyStun>();
                bool stunned = stun != null && stun.StunRemaining > 0f;
                if (stunned && lastStunned[entityId] == false) stuns[userId]++;
                lastStunned[entityId] = stunned;
            }
        }

        /// <summary>지켜본 사람의 등수 줄마다 고비 키 넷 + 가장 긴 연속 + 기절 횟수를 싣는다(0 포함 — "서버가 0이라고 했다"와 "옛 서버라 안 보냈다"가 갈리게). 다른 키는 그대로 둔다.</summary>
        public void AttachTo(MatchOutcome outcome)
        {
            foreach (var placement in outcome.placements)
            {
                if (placement.userId == null || crossings.TryGetValue(placement.userId, out var counts) == false)
                {
                    continue;
                }
                if (placement.stats == null)
                {
                    placement.stats = new Dictionary<string, int>();
                }
                for (int i = 0; i < counts.Length; i++)
                {
                    placement.stats[FlappyComboStatKeys.For(FlappyComboStatKeys.Thresholds[i])] = counts[i];
                }
                placement.stats[FlappyComboStatKeys.Best] = best[placement.userId];
                placement.stats[FlappyComboStatKeys.Stuns] = stuns[placement.userId];
            }
        }
    }
}
