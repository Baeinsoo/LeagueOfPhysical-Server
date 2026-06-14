using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 서버 디버그 HUD View. ViewModel에서 매 프레임 값을 pull해 라벨을 갱신한다
    /// (이벤트 없는 샘플링 값이라 R3 미사용 — 패널 스케줄러 폴링, 원본 TimeUI.Update() 대체).
    /// </summary>
    public class DebugHudView : UIView
    {
        private readonly DebugHudViewModel _viewModel;

        private Label _tickText;
        private Label _elapsedText;

        private IVisualElementScheduledItem _tick;

        public DebugHudView(DebugHudViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public override void OnOpen()
        {
            base.OnOpen();

            _tickText = Root.Q<Label>("tick-text");
            _elapsedText = Root.Q<Label>("elapsed-text");

            _tick = Root.schedule.Execute(Refresh).Every(0);
        }

        private void Refresh(TimerState _)
        {
            if (!_viewModel.IsRunning)
            {
                return;
            }

            _tickText.text = $"Tick: {_viewModel.Tick}";
            _elapsedText.text = $"elapsed: {_viewModel.ElapsedTime:F2}";
        }

        public override void Dispose()
        {
            _tick?.Pause();
            base.Dispose();
        }
    }
}
