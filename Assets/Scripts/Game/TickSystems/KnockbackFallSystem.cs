using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 떨어짐 판정(서버 End 페이즈). 바닥 칸이 없는 곳에서 몸이 낙하 깊이보다 내려가면 탈락시키고 몸을 치운다.
    /// </summary>
    public class KnockbackFallSystem : GameFramework.Runner.ITickSystem
    {
        private readonly KnockbackMatchState state;
        private readonly GameFramework.World.EntityRegistry registry;
        private readonly KnockbackConfig config;
        private readonly System.Action<string> despawn;

        public KnockbackFallSystem(KnockbackMatchState state, GameFramework.World.EntityRegistry registry,
                                   KnockbackConfig config, System.Action<string> despawn)
        {
            this.state = state;
            this.registry = registry;
            this.config = config;
            this.despawn = despawn;
        }

        public void Tick(long tick, float deltaTime)
        {
            state.LastTick = tick;
            foreach (var id in state.Players)
            {
                if (state.EliminatedTick.ContainsKey(id) || !registry.TryGet(id, out var entity))
                {
                    continue;
                }
                if (entity.Get<GameFramework.World.Transform>().Position.Y >= -config.FallDepth)
                {
                    continue;
                }

                state.EliminatedTick[id] = tick;
                state.Eliminations.Add((id, tick));
                Debug.Log($"[Knockback] fell {id} tick {tick}");
                despawn(id);
            }
        }
    }
}
