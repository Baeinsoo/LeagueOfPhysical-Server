using GameFramework;          // SceneInjectMonoBehaviour
using GameFramework.Runner;   // IRunner, RunnerState
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 서버가 자기 틱 속도를 지키고 있는지 주기적으로 남긴다. 기본 꺼짐 — 진단할 때만 켠다.
    /// 밀림 = 기대 틱(벽시계 기준) − 실제 틱. 이 값이 0에서 안 떨어지면 서버는 건강하다.
    /// </summary>
    [SceneInjectMonoBehaviour]
    public class TickHealthLogger : MonoBehaviour
    {
        [SerializeField] private bool logEnabled;
        [SerializeField] private float logIntervalSeconds = 2f;

        [Inject] private IRunner runner;
        [Inject] private GameFramework.World.EntityRegistry entityRegistry;

        private float nextLogTime;
        private float maxFrameMs;

        private void Update()
        {
            if (logEnabled == false || runner.tickUpdater == null || runner.gameState < RunnerState.Playing)
            {
                return;
            }

            // 프레임 최대치를 창 안에서 모은다 — 평균은 한 번씩 크게 튀는 프레임을 가려 버린다.
            float frameMs = Time.unscaledDeltaTime * 1000f;
            if (frameMs > maxFrameMs)
            {
                maxFrameMs = frameMs;
            }

            if (Time.unscaledTime < nextLogTime)
            {
                return;
            }
            nextLogTime = Time.unscaledTime + logIntervalSeconds;

            var tickUpdater = runner.tickUpdater;
            long expected = tickUpdater.processibleTick;
            Debug.Log($"[TickHealth] tick={tickUpdater.tick} expected={expected} lag={expected - tickUpdater.tick}" +
                      $" frameMaxMs={maxFrameMs:F1} budgetMs={tickUpdater.interval * 1000:F1}" +
                      $" entities={entityRegistry.Count}");
            maxFrameMs = 0f;
        }
    }
}
