using Cysharp.Threading.Tasks;
using GameFramework;
using GameFramework.Runner;
using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UniRx;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class LOPRoom : MonoBehaviour, IServerRoom
    {
        private const int HEARTBEAT_INTERVAL = 2;       //  sec
        private const double TICK_INTERVAL = 1 / 50d;   //  sec

        [Inject] private IGameFactory gameFactory;
        [Inject] private LOPNetworkManager networkManager;
        [Inject] private ISessionManager sessionManager;
        [Inject] private IRoomDataStore roomDataStore;
        [Inject] private NetworkMessageDispatcher dispatcher;

        public IRunner runner { get; private set; }

        public bool initialized { get; private set; }

        private async void Awake()
        {
            try
            {
                await InitializeAsync();
                await StartRoomServerAsync();
                await StartGameAsync();
            }
            catch (Exception e)
            {
                Debug.LogError(e);

                if (!EnvironmentSettings.active.Standalone)
                {
                    await WebAPI.UpdateRoomStatus(new UpdateRoomStatusRequest
                    {
                        roomId = roomDataStore.room.id,
                        status = RoomStatus.Error,
                    });
                }
            }
        }

        private async void OnDestroy()
        {
            await ShutdownRoomServerAsync();
            await DeinitializeAsync();
        }

        public async Task InitializeAsync()
        {
            runner = await gameFactory.CreateAsync();
            runner.onGameStateChanged += OnGameStateChanged;

            InvokeRepeating("SendHeartbeat", 0, HEARTBEAT_INTERVAL);

            await runner.InitializeAsync();

            if (!EnvironmentSettings.active.Standalone)
            {
                await WebAPI.UpdateRoomStatus(new UpdateRoomStatusRequest
                {
                    roomId = roomDataStore.room.id,
                    status = RoomStatus.Initializing,
                });
            }

            initialized = true;
        }

        public async Task DeinitializeAsync()
        {
            await runner.DeinitializeAsync();

            CancelInvoke("SendHeartbeat");

            runner.onGameStateChanged -= OnGameStateChanged;

            await gameFactory.DestroyAsync();
            runner = null;

            roomDataStore.Clear();

            initialized = false;
        }

        public async Task StartRoomServerAsync()
        {
            NetworkServer.RegisterHandler<CustomMirrorMessage>((conn, message) =>
            {
                //  RegisterHandler의 requireAuthentication 기본값이 true라 미인증 연결은 여기 오지 않는다.
                //  즉 authenticationData는 인증기가 채워 둔 값이 반드시 들어 있다.
                var customProperties = (CustomProperties)conn.authenticationData;
                dispatcher.Dispatch(customProperties.userId, message.payload);
            });

            networkManager.onServerConnect += OnPlayerConnect;
            networkManager.onServerDisconnect += OnPlayerDisconnect;
            networkManager.port = Blackboard.Read<ushort>("port", erase: true);
            networkManager.StartServer();

            await UniTask.WaitUntil(() => NetworkServer.active);
        }

        private async Task ShutdownRoomServerAsync()
        {
            networkManager.StopClient();

            await UniTask.WaitUntil(() => NetworkServer.active == false);
        }

        public async Task StartGameAsync()
        {
            if (EnvironmentSettings.active.Standalone)
            {
                await Task.CompletedTask;
            }
            else
            {
                await WebAPI.UpdateRoomStatus(new UpdateRoomStatusRequest
                {
                    roomId = roomDataStore.room.id,
                    status = RoomStatus.WaitingForPlayers,
                });
            }

            // 틱을 자기 시계에서 유도한다. 0을 넣으면 다음 프레임에 elapsedTime이 ServerNow(프로세스
            // 가동 시간)로 덮이면서 tick만 뒤처지고, 프레임당 8틱 상한 탓에 몇 초를 8배속으로 질주한다.
            // 그동안 tick과 elapsedTime이 서로 안 맞아 gameInfo가 자기모순인 값을 클라에 보낸다.
            double now = runner.networkTime.ServerNow;
            runner.Run((long)(now / TICK_INTERVAL), TICK_INTERVAL, now);
        }

        private void SendHeartbeat()
        {
            if (!EnvironmentSettings.active.Standalone)
            {
                //  결과를 기다리지 않는다 — 하트비트는 실패해도 다음 주기가 이어서 보낸다.
                WebAPI.Heartbeat(roomDataStore.room.id).Forget();
            }
        }

        private void OnGameStateChanged(RunnerState gameState)
        {
            switch (gameState)
            {
                case RunnerState.GameOver:
                    Debug.Log("Game Over");

                    // 룸을 닫으면 클라 연결이 끊겨 못 받는다 — 상태 갱신보다 반드시 먼저 보낸다.
                    foreach (var session in sessionManager.GetAllSessions())
                    {
                        session.Send(new MatchEndedToC());
                    }

                    if (!EnvironmentSettings.active.Standalone)
                    {
                        //  이벤트 핸들러라 await 할 수 없다 — 보내기만 하고 넘어간다.
                        WebAPI.UpdateRoomStatus(new UpdateRoomStatusRequest
                        {
                            roomId = roomDataStore.room.id,
                            status = RoomStatus.Closed,
                        }).Forget();
                    }
                    break;
            }
        }

        public void OnPlayerConnect(IConnectionData connectionData)
        {
            if (connectionData is not LOPConnectionData data)
            {
                throw new ArgumentException("Invalid connection data");
            }

            var conn = data.networkConnection;

            if (conn.authenticationData is not CustomProperties customProperties)
            {
                //  Mirror는 소켓이 붙는 즉시(인증 완료 전에도) 이 콜백을 부를 수 있다. 아직 신원이
                //  확인 안 된 연결이라 할 일이 없다 — 인증이 끝나면 그때 세션이 만들어진다.
                return;
            }

            Debug.Log($"[OnPlayerEnter] userId: {customProperties.userId}, identity: {conn.identity}");

            if (sessionManager.TryGetSessionByUserId<LOPSession>(customProperties.userId, out var session))
            {
                session.networkConnection = conn;
            }
            else
            {
                sessionManager.AddSession(new LOPSession(customProperties.userId, conn));
            }
        }

        public void OnPlayerDisconnect(IConnectionData connectionData)
        {
            if (connectionData is not LOPConnectionData data)
            {
                throw new ArgumentException("Invalid connection data");
            }

            var conn = data.networkConnection;

            if (conn.authenticationData is not CustomProperties customProperties)
            {
                //  인증되지 못한 채 끊긴 연결이다(로비 장애·타임아웃·만료 토큰·틀린 키 등 — 더 이상
                //  드문 예외가 아니라 흔히 오는 경로다). 세션을 만든 적이 없으니 더 할 일이 없다.
                return;
            }

            Debug.Log($"[OnPlayerLeave] userId: {customProperties.userId}, identity: {conn.identity}");

            if (sessionManager.TryGetSessionByUserId<LOPSession>(customProperties.userId, out var session) == false)
            {
                //  세션이 아직 없는데 끊긴 경우(예: 인증 성공 직후 세션 생성 전 경합). 더 할 일이 없다.
                return;
            }

            session.networkConnection = null;
        }
    }
}
