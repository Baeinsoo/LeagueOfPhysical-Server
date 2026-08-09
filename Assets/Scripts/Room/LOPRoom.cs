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
                if (TryGetSession(conn, out ISession session) == false)
                {
                    return;
                }

                dispatcher.Dispatch(session, message.payload);
            });

            networkManager.onServerConnect += OnPlayerConnect;
            networkManager.onServerDisconnect += OnPlayerDisconnect;
            networkManager.port = Blackboard.Read<ushort>("port", erase: true);
            networkManager.StartServer();

            await UniTask.WaitUntil(() => NetworkServer.active);
        }

        //  연결 → 세션. 계정 id를 거치지 않는다 — 같은 계정이 여러 연결을 가질 수 있어서,
        //  계정으로 찾으면 "어느 연결이 보냈나"에 답하지 못한다.
        private bool TryGetSession(NetworkConnectionToClient conn, out ISession session)
        {
            session = null;

            if (conn.authenticationData is not ConnectionIdentity identity || string.IsNullOrEmpty(identity.SessionId))
            {
                return false;
            }

            if (sessionManager.TryGetSessionById(identity.SessionId, out session) == false || session is not LOPSession found)
            {
                return false;
            }

            if (ReferenceEquals(found.networkConnection, conn) == false)
            {
                //  이 세션은 이미 다른(더 새) 연결의 것이다. 해제 경로와 같은 규율 — 옛 연결이 산 플레이어를
                //  조종하거나 그쪽으로 응답이 가게 만들지 못한다.
                session = null;
                return false;
            }

            return true;
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

            runner.Run(0, TICK_INTERVAL, 0);
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

            if (conn.authenticationData is not ConnectionIdentity identity)
            {
                //  Mirror는 소켓이 붙는 즉시(인증 완료 전에도) 이 콜백을 부를 수 있다. 아직 신원이
                //  확인 안 된 연결이라 할 일이 없다 — 인증이 끝나면 그때 세션이 만들어진다.
                return;
            }

            Debug.Log($"[OnPlayerEnter] userId: {identity.UserId}, identity: {conn.identity}");

            if (sessionManager.TryGetSessionByUserId<LOPSession>(identity.UserId, out LOPSession session) == false)
            {
                session = new LOPSession(identity.UserId, conn);
                sessionManager.AddSession(session);
            }
            else
            {
                session.networkConnection = conn;
            }

            //  연결이 자기 세션을 가리키게 한다. 이후 수신·해제는 이 값으로 세션을 찾는다.
            identity.SessionId = session.sessionId;
        }

        public void OnPlayerDisconnect(IConnectionData connectionData)
        {
            if (connectionData is not LOPConnectionData data)
            {
                throw new ArgumentException("Invalid connection data");
            }

            var conn = data.networkConnection;

            if (conn.authenticationData is not ConnectionIdentity identity || string.IsNullOrEmpty(identity.SessionId))
            {
                //  인증되지 못한 채 끊긴 연결이다(로비 장애·타임아웃·만료 토큰·틀린 키 등 — 더 이상
                //  드문 예외가 아니라 흔히 오는 경로다). 세션을 만든 적이 없으니 더 할 일이 없다.
                return;
            }

            Debug.Log($"[OnPlayerLeave] userId: {identity.UserId}, identity: {conn.identity}");

            if (sessionManager.TryGetSessionById(identity.SessionId, out ISession found) == false || found is not LOPSession session)
            {
                return;
            }

            //  이미 새 연결로 갈아탄 세션이면 건드리지 않는다. Mirror의 해제 감지는 타임아웃이라
            //  옛 연결의 해제가 재접속보다 늦게 도착할 수 있는데, 그때 세션을 끄면 방금 다시 들어온
            //  플레이어가 아무 조작도 못 하게 된다.
            if (ReferenceEquals(session.networkConnection, conn) == false)
            {
                return;
            }

            session.networkConnection = null;
        }
    }
}
