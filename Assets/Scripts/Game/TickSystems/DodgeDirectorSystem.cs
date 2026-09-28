using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 진행기를 틱마다 돌린다(서버 End 페이즈, 판정보다 앞). 끝난 패턴을 빼고 새 패턴을 더한다.
    /// 산 사람 위치는 조준탄·발밑 폭탄이 노릴 자리다.
    /// </summary>
    public class DodgeDirectorSystem : GameFramework.Runner.ITickSystem
    {
        private readonly DodgeMatchState state;
        private readonly DodgeDirector director;
        private readonly GameFramework.World.IWorld world;
        private readonly DodgeConfig config;
        private readonly List<Vector2> alive = new List<Vector2>();
        private readonly List<DodgePattern> fresh = new List<DodgePattern>();
        private readonly List<string> ids = new List<string>();

        public DodgeDirectorSystem(DodgeMatchState state, DodgeDirector director,
                                   GameFramework.World.IWorld world, DodgeConfig config)
        {
            this.state = state;
            this.director = director;
            this.world = world;
            this.config = config;
        }

        public void Tick(long tick, float deltaTime)
        {
            var cfg = config;
            if (state.Patterns.RemoveAll(p => DodgeHazards.IsOver(p, tick, cfg)) > 0)
            {
                state.MarkChanged();
            }

            alive.Clear();
            ids.Clear();
            ids.AddRange(state.Players.Keys);
            ids.Sort(string.CompareOrdinal);
            foreach (var id in ids)
            {
                if (state.Players[id].Alive && world.EntityRegistry.TryGet(id, out var e))
                {
                    var p = e.Get<GameFramework.World.Transform>().Position;
                    alive.Add(new Vector2(p.X, p.Z));
                }
            }

            fresh.Clear();
            director.Next(tick, world.GameplayStartTick, alive, fresh);
            if (fresh.Count > 0)
            {
                state.Patterns.AddRange(fresh);
                state.MarkChanged();
            }
        }
    }
}
