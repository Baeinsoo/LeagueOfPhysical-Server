using GameFramework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using System.Threading.Tasks;

namespace LOP
{
    public class LOPGame : MonoBehaviour, IGame
    {
        public IGameEngine gameEngine { get; private set; }
        [Inject] private RoomNetwork roomNetwork;

        private float originalFixedDeltaTime;
        private bool originalAutoSyncTransforms;

        public bool initialized { get; private set; }

        public async Task InitializeAsync()
        {
            roomNetwork.RegisterHandler<GameInfoRequest>(OnGameInfoRequest);

            Physics.simulationMode = SimulationMode.Script;

            originalAutoSyncTransforms = Physics.autoSyncTransforms;
            Physics.autoSyncTransforms = true;

            originalFixedDeltaTime = UnityEngine.Time.fixedDeltaTime;

            gameEngine = GetComponentInChildren<IGameEngine>();

            await gameEngine.InitializeAsync();

            initialized = true;
        }

        public async Task DeinitializeAsync()
        {
            await gameEngine.DeinitializeAsync();

            Physics.simulationMode = SimulationMode.FixedUpdate;
            Physics.autoSyncTransforms = originalAutoSyncTransforms;

            UnityEngine.Time.fixedDeltaTime = originalFixedDeltaTime;

            initialized = false;
        }

        public void Run(long tick, double interval, double elapsedTime)
        {
            gameEngine.Run(tick, interval, elapsedTime);
        }

        public void Stop()
        {
            gameEngine.Stop();
        }

        private void OnGameInfoRequest(int id, GameInfoRequest gameInfoRequest)
        {
            Debug.Log($"gameInfoRequest: {gameInfoRequest}");

            var gameInfoResponse = new GameInfoResponse
            {
                EntityId = 1,
                GameInfo = new GameInfo
                {
                    Tick = 1,
                    Interval = 0.1,
                    ElapsedTime = 5,
                },
            };

            roomNetwork.Send(gameInfoResponse, id);
        }
    }
}
