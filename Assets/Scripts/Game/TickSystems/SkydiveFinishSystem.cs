using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 누가 먼저 바닥에 닿았는지 매 틱 지켜보고 순서를 적어 둔다.
    /// 룰(<see cref="SkydiveRuleSystem"/>)이 이걸 읽어 종료와 등수를 답한다 — 순서를 세는 일과
    /// 판을 끝내는 일을 나눈 이유는 룰에는 틱이 없어서다(판치기의 룰/턴 짝과 같은 구조).
    /// </summary>
    public class SkydiveFinishSystem : GameFramework.Runner.ITickSystem
    {
        private readonly GameFramework.World.EntityRegistry entityRegistry;

        private readonly List<string> watched = new List<string>();
        private readonly List<string> finishedOrder = new List<string>();
        private readonly HashSet<string> finishedSet = new HashSet<string>();

        // 결승 고도는 맵이 정한다. 맵 씬은 나중에 로드되므로 생성자에서 찾으면 못 찾는다 —
        // 첫 틱까지 미뤘다가 그때 한 번만 찾는다.
        private SkydiveProgress progress;

        public SkydiveFinishSystem(GameFramework.World.EntityRegistry entityRegistry)
        {
            this.entityRegistry = entityRegistry;
        }

        public IReadOnlyList<string> FinishedOrder => finishedOrder;

        public void Watch(string entityId) => watched.Add(entityId);

        public void Reset()
        {
            watched.Clear();
            finishedOrder.Clear();
            finishedSet.Clear();
            progress = null;
        }

        public void Tick(long tick, float deltaTime)
        {
            EnsureProgress();

            for (int i = 0; i < watched.Count; i++)
            {
                string entityId = watched[i];
                if (finishedSet.Contains(entityId))
                {
                    continue;   // 등수는 처음 통과한 순간이 정답이다
                }

                // 나간 사람의 몸은 이미 없다
                var entity = entityRegistry.Get(entityId);
                if (entity == null)
                {
                    continue;
                }

                if (progress.HasFinished(entity.Get<GameFramework.World.Transform>().Position.Y))
                {
                    finishedOrder.Add(entityId);
                    finishedSet.Add(entityId);
                }
            }
        }

        /// <summary>
        /// 남아 있는 사람이 전원 내려왔나. <b>아무도 없으면 false</b> — 스폰 직전에 판이 끝나는 것을 막는다.
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
                    if (finishedSet.Contains(watched[i]) == false)
                    {
                        return false;
                    }
                }
                return alive > 0;
            }
        }

        private void EnsureProgress()
        {
            if (progress != null)
            {
                return;
            }

            var markers = UnityEngine.Object.FindObjectsByType<FinishLine>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (markers.Length == 1)
            {
                progress = new SkydiveProgress(markers[0].transform.position.y);
                return;
            }

            // Flappy는 같은 상황에서 예외를 던지지만 여기서는 판이 이미 굴러가는 중이다 —
            // 던지면 방 전체가 죽으므로, 크게 알리고 바닥을 결승선으로 삼아 판은 끝나게 둔다.
            Debug.LogError($"[Skydive] 맵에 FinishLine 마커가 정확히 하나 있어야 한다 (발견: {markers.Length}개). 바닥(y=0)을 결승선으로 쓴다");
            progress = new SkydiveProgress(0f);
        }
    }
}
