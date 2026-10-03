namespace LOP
{
    /// <summary>시뮬용 빈 월드 — 진행기·판정 시스템이 읽는 레지스트리와 경기 시작 틱만 있다. 이동은 시뮬이 위치를 직접 쓴다.</summary>
    public sealed class DodgeSimWorld : GameFramework.World.IWorld
    {
        public GameFramework.World.EntityRegistry EntityRegistry { get; } = new GameFramework.World.EntityRegistry();
        public GameFramework.World.WorldEventBuffer EventBuffer { get; } = new GameFramework.World.WorldEventBuffer();
        public long GameplayStartTick { get; set; }
        public void Tick(long tick, float deltaTime) { }
        public void SaveState(long tick) { }
        public bool LoadState(long tick) => false;
        public long? FirstSavedTick => null;
        public long? LatestSavedTick => null;

        public bool TryGetSavedMotion(long tick, string entityId, out GameFramework.Netcode.EntitySnapshot motion)
        {
            motion = default;
            return false;
        }
    }
}
