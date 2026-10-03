using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 피하기 판정(서버 End 페이즈). 전원이 다 움직인 뒤, 사람마다 "지난 틱 → 이번 틱" 이동 경로를
    /// 이번 틱 위험 도형과 잰다. 맞으면 목숨 −1과 무적, 목숨이 다 떨어지면 탈락시키고 몸을 치운다.
    /// 위치·속도는 건드리지 않는다(스펙 §3.4).
    /// </summary>
    public class DodgeHazardSystem : GameFramework.Runner.ITickSystem
    {
        private readonly DodgeMatchState state;
        private readonly GameFramework.World.EntityRegistry registry;
        private readonly DodgeConfig config;
        private readonly System.Action<string> eliminate;
        private readonly Dictionary<string, Vector2> previous = new Dictionary<string, Vector2>();
        private readonly List<string> ids = new List<string>();

        public DodgeHazardSystem(DodgeMatchState state, GameFramework.World.EntityRegistry registry,
                                 DodgeConfig config, System.Action<string> eliminate)
        {
            this.state = state;
            this.registry = registry;
            this.config = config;
            this.eliminate = eliminate;
        }

        public void Tick(long tick, float deltaTime)
        {
            state.LastTick = tick;
            // 판이 갈렸다(여운 동안) — 이긴 사람은 더 맞지 않는다. 혼자 들어온 판은 계속 센다.
            if (state.Players.Count >= 2 && state.AliveCount <= 1)
            {
                return;
            }

            // 순서를 고정한다 — 같은 틱의 판정 결과가 딕셔너리 순서에 따라 달라지면 안 된다.
            ids.Clear();
            ids.AddRange(state.Players.Keys);
            ids.Sort(string.CompareOrdinal);

            foreach (var id in ids)
            {
                var life = state.Players[id];
                if (!life.Alive || !registry.TryGet(id, out var entity))
                {
                    continue;
                }

                var p = entity.Get<GameFramework.World.Transform>().Position;
                var to = new Vector2(p.X, p.Z);
                if (!previous.TryGetValue(id, out var from))
                {
                    previous[id] = to;
                    continue;
                }
                previous[id] = to;

                if (tick < life.InvulnerableUntilTick)
                {
                    continue;
                }

                foreach (var pattern in state.Patterns)
                {
                    if (!DodgeHazards.Hits(pattern, tick, from, to, config))
                    {
                        continue;
                    }

                    life.Lives--;
                    life.InvulnerableUntilTick = tick + config.InvulnerableTicks;
                    if (life.Lives <= 0)
                    {
                        life.EliminatedTick = tick;
                        state.Eliminations.Add((id, tick));
                        eliminate(id);
                    }
                    state.MarkChanged();
                    break;   // 한 틱엔 한 번만 깎인다
                }
            }
        }
    }
}
