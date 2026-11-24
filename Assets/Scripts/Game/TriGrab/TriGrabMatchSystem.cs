using Vector2 = System.Numerics.Vector2;

namespace LOP
{
    /// <summary>
    /// 삼색 그랩 판 진행(서버 End 페이즈). 모두 걷고 난 뒤: 시작 틱이 되면 판을 열고,
    /// 나간 사람을 판 엔진에 알리고, 이번 틱에 쏘기 입력(Fire)이 있는 몸마다 갈고리를 쏘게 하고, 판 엔진을 한 틱 굴린다.
    /// </summary>
    public sealed class TriGrabMatchSystem : GameFramework.Runner.ITickSystem
    {
        private readonly LOP.TriGrab.TriGrabMatch match;
        private readonly GameFramework.World.EntityRegistry registry;
        private readonly GameFramework.World.IWorld world;
        private readonly TriGrabRunClock clock;
        private bool started;

        /// <summary>판 도중 나가 있는 사람. 판 엔진에 알려 라운드가 그 사람을 기다리지 않게 한다. 시험 등에서 없으면 아무도 안 나간 것으로 본다.</summary>
        [VContainer.Inject] public PlayerPresence Presence { get; set; }

        public TriGrabMatchSystem(LOP.TriGrab.TriGrabMatch match, GameFramework.World.EntityRegistry registry,
                                  GameFramework.World.IWorld world, TriGrabRunClock clock)
        {
            this.match = match;
            this.registry = registry;
            this.world = world;
            this.clock = clock;
        }

        private Vector2 BodyXz(string id)
        {
            var transform = registry.Get(id)?.Get<GameFramework.World.Transform>();
            return transform == null ? Vector2.Zero : new Vector2(transform.Position.X, transform.Position.Z);
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
                //  봇(명단 밖)은 IsAbsent가 늘 false라 늘 활성이다.
                string userId = entity.Get<GameFramework.World.Ownership>()?.OwnerId;
                match.SetActive(id, Presence == null || userId == null || !Presence.IsAbsent(userId));
                var input = entity.Get<InputBuffer>()?.Current;
                var transform = entity.Get<GameFramework.World.Transform>();
                if (input == null || input.Fire == false || transform == null) continue;
                match.TryFire(id, new Vector2(transform.Position.X, transform.Position.Z), input.AimYaw, tick);
            }

            match.Tick(tick, BodyXz);
            if (clock.OverSinceTick < 0 && match.State.Phase == LOP.TriGrab.TriGrabPhase.Over)
            {
                clock.OverSinceTick = tick;
            }
        }
    }
}
