using GameFramework;

namespace LOP.UI
{
    /// <summary>
    /// 서버 디버그 HUD ViewModel. tick·경과시간은 변경 통지 이벤트가 없는 샘플링 값이라
    /// R3(push) 대신 평범한 getter로 노출하고 View가 매 프레임 pull한다.
    /// RTT(클라→서버 지연)는 서버에서 의미가 없어 tick/elapsed만 노출한다.
    /// </summary>
    public class DebugHudViewModel
    {
        private readonly IRunner runner;

        public DebugHudViewModel(IRunner runner)
        {
            this.runner = runner;
        }

        public bool IsRunning => runner.gameState >= RunnerState.Playing;

        public long Tick => runner.tickUpdater.tick;

        public double ElapsedTime => runner.tickUpdater.elapsedTime;
    }
}
