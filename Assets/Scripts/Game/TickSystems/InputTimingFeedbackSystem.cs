using System.Collections.Generic;
using GameFramework;

namespace LOP
{
    /// <summary>구 LOPRunner.SendInputTimingFeedback 이동. ~0.5초마다 조종 엔티티별 입력 타이밍 요약을 그 세션에 전송(Phase 4). 활동 없으면 skip.</summary>
    public class InputTimingFeedbackSystem : GameFramework.Runner.ITickSystem
    {
        private const long InputTimingFeedbackIntervalTicks = 15;  // 틱레이트 기준 ~0.5초 — 필요시 조정

        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly ISessionManager sessionManager;

        public InputTimingFeedbackSystem(GameFramework.World.EntityRegistry entityRegistry, ISessionManager sessionManager)
        {
            this.entityRegistry = entityRegistry;
            this.sessionManager = sessionManager;
        }

        public void Tick(long tick, float deltaTime)
        {
            if (tick % InputTimingFeedbackIntervalTicks != 0)
            {
                return;
            }

            foreach (var worldEntity in new List<GameFramework.World.Entity>(entityRegistry.All))
            {
                var buffer = worldEntity.Get<InputBuffer>();
                if (buffer == null)
                {
                    continue;
                }

                var summary = buffer.TimingTracker.Summarize();
                if (summary.HasActivity == false)
                {
                    continue;
                }

                string userId = worldEntity.Get<GameFramework.World.Ownership>()?.OwnerId;
                ISession session = sessionManager.GetSessionByUserId(userId);
                if (session == null)
                {
                    continue;
                }

                session.Send(new InputTimingToC
                {
                    AvgD = summary.AvgD,
                    MaxD = summary.MaxD,
                    PruneCount = summary.PruneCount,
                    SeqGapCount = summary.SeqGapCount,
                    SampleCount = summary.SampleCount,
                }, reliable: false);
            }
        }
    }
}
