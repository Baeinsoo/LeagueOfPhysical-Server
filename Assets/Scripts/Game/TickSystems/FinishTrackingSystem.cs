using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// 시뮬이 적어 둔 통과 기록(<see cref="FinishState"/>)을 모아 순서를 들고 있는다.
    ///
    /// <para><b>왜 따로 들고 있나:</b> 완주한 사람이 나가면 그 몸이 사라지면서 컴포넌트의 기록도
    /// 같이 사라진다. 그러면 등수를 매길 때 그 사람이 "나간 사람"(최하위)으로 둔갑한다. 한 번
    /// 관측한 통과는 몸과 무관하게 남아야 한다.</para>
    ///
    /// <para>판정은 하지 않는다 — 옮겨 담기만 한다. 판정은 클·서 공통 시뮬의 몫이라, 클라도
    /// 자기 새가 언제 통과했는지 같은 규칙으로 즉시 안다.</para>
    /// </summary>
    public class FinishTrackingSystem : GameFramework.Runner.ITickSystem
    {
        private readonly GameFramework.World.EntityRegistry entityRegistry;

        private readonly FinishOrderTracker tracker = new FinishOrderTracker();
        private readonly List<string> watched = new List<string>();

        public FinishTrackingSystem(GameFramework.World.EntityRegistry entityRegistry)
        {
            this.entityRegistry = entityRegistry;
        }

        /// <summary>먼저 닿은 순. 같은 틱이면 깊이 넘은 쪽이 앞.</summary>
        public IReadOnlyList<FinishRecord> Ordered => tracker.Ordered;

        public bool HasFinished(string entityId) => tracker.HasFinished(entityId);

        public void Watch(string entityId) => watched.Add(entityId);

        public void Reset()
        {
            watched.Clear();
            tracker.Reset();
        }

        public void Tick(long tick, float deltaTime)
        {
            for (int i = 0; i < watched.Count; i++)
            {
                var state = entityRegistry.Get(watched[i])?.Get<FinishState>();
                if (state != null && state.Finished)
                {
                    //  이미 기록된 사람은 Observe가 알아서 무시한다.
                    tracker.Observe(watched[i], state.FinishedTick, state.Depth);
                }
            }
        }

        /// <summary>
        /// 남아 있는 사람이 전원 통과했나. <b>아무도 없으면 false</b> — 스폰 직전에 판이 끝나는 것을 막는다.
        /// </summary>
        public bool AllWatchedFinished
        {
            get
            {
                int alive = 0;
                for (int i = 0; i < watched.Count; i++)
                {
                    if (entityRegistry.Get(watched[i]) == null)
                    {
                        continue;   // 나간 사람은 세지 않는다. 세면 한 명 나간 판이 절대 안 끝난다
                    }
                    alive++;
                    if (tracker.HasFinished(watched[i]) == false)
                    {
                        return false;
                    }
                }
                return alive > 0;
            }
        }
    }
}
