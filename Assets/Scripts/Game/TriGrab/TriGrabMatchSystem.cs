using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 삼색 그랩 판 진행(서버 End 페이즈). 모두 걷고 난 뒤: 시작 틱이 되면 판을 열고,
    /// 이번 틱에 쏘기 입력(Fire)이 있는 몸마다 갈고리를 쏘게 하고, 판 엔진을 한 틱 굴린다.
    /// </summary>
    public sealed class TriGrabMatchSystem : GameFramework.Runner.ITickSystem
    {
        private readonly TriGrabMatch match;
        private readonly GameFramework.World.EntityRegistry registry;
        private readonly GameFramework.World.IWorld world;
        private readonly TriGrabRunClock clock;
        private bool started;

        public TriGrabMatchSystem(TriGrabMatch match, GameFramework.World.EntityRegistry registry,
                                  GameFramework.World.IWorld world, TriGrabRunClock clock)
        {
            this.match = match;
            this.registry = registry;
            this.world = world;
            this.clock = clock;
        }

        public void Tick(long tick, float deltaTime)
        {
            clock.LastTick = tick;
            if (!started)
            {
                if (tick < world.GameplayStartTick) return;
                started = true;
                match.Start(tick);
            }

            foreach (var id in match.State.PlayerOrder)
            {
                if (registry.TryGet(id, out var entity) == false) continue;
                var input = entity.Get<InputBuffer>()?.Current;
                var transform = entity.Get<GameFramework.World.Transform>();
                if (input == null || input.Fire == false || transform == null) continue;
                match.TryFire(id, new Vector2(transform.Position.X, transform.Position.Z), input.AimYaw, tick);
            }

            match.Tick(tick);
            if (clock.OverSinceTick < 0 && match.State.Phase == TriGrabPhase.Over)
            {
                clock.OverSinceTick = tick;
            }
        }
    }
}
