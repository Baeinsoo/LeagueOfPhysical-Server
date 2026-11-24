namespace LOP
{
    /// <summary>
    /// 갈고리가 나가 있거나 기절한 사람의 이동 입력을 서버에서 0으로 만든다(서버 Begin 페이즈).
    /// 클라도 0을 보내지만 권위는 서버다. 입력 확정(ServerInputSystem)보다 앞이라 버퍼의 이번 틱 커맨드를 고쳐 둔다.
    /// 쏘기(Fire)·시퀀스 번호는 그대로 둔다.
    /// </summary>
    public sealed class TriGrabMoveGateSystem : GameFramework.Runner.ITickSystem
    {
        private readonly GameFramework.World.EntityRegistry registry;
        private readonly LOP.TriGrab.TriGrabMatch match;

        public TriGrabMoveGateSystem(GameFramework.World.EntityRegistry registry, LOP.TriGrab.TriGrabMatch match)
        {
            this.registry = registry;
            this.match = match;
        }

        public void Tick(long tick, float deltaTime)
        {
            foreach (var id in match.State.PlayerOrder)
            {
                if (IsLocked(id, tick) == false) continue;
                if (registry.TryGet(id, out var entity) == false) continue;
                var buffer = entity.Get<InputBuffer>();
                if (buffer == null) continue;

                if (buffer.Commands.TryGetValue(tick, out var command)) buffer.Commands[tick] = WithoutMove(command);
                //  이번 틱 입력이 유실되면 서버는 마지막 입력의 이동을 이어 쓴다 — 그 길로도 걷지 못하게 한다.
                if (buffer.LastReceived != null) buffer.LastReceived = WithoutMove(buffer.LastReceived);
            }
        }

        public bool IsLocked(string id, long tick) =>
            match.HookOf(id) != null
            || (match.State.Players.TryGetValue(id, out var p) && tick < p.StunUntilTick);

        private static InputCommand WithoutMove(InputCommand c) => new InputCommand
        {
            SequenceNumber = c.SequenceNumber,
            Horizontal = 0f,
            Vertical = 0f,
            Fire = c.Fire,
            AimYaw = c.AimYaw,
            ViewTick = c.ViewTick,
        };
    }
}
