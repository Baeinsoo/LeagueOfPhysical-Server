namespace LOP
{
    /// <summary>
    /// 매 틱 끝에 모두의 자리·머리·속도를 적어 둔다(서버). 래그 보상이 "쏜 사람이 보던 틱"의 자리를 여기서 꺼낸다.
    /// 서버는 되감아 다시 굴리지 않으므로 이 기록은 읽기 전용으로만 쓰인다.
    /// </summary>
    public sealed class MotionHistorySystem : GameFramework.Runner.ITickSystem
    {
        private readonly GameFramework.World.IWorld world;

        public MotionHistorySystem(GameFramework.World.IWorld world)
        {
            this.world = world;
        }

        public void Tick(long tick, float deltaTime) => world.SaveState(tick);
    }
}
