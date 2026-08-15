using Cysharp.Threading.Tasks;
using GameFramework;
using GameFramework.Runner;
using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
        private const double CLOSE_TIMEOUT_SECONDS = 1.5;

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

                    CloseRoomAsync().Forget();
                    break;
            }
        }

        //  순서가 중요하다. 백엔드가 "이 방 끝났다"를 먼저 저장해야, 로비로 돌아간 클라가 자기
        //  위치를 물었을 때 방금 끝난 방으로 다시 끌려가지 않는다 — 그래서 이 저장을 기다린다.
        //  단, 저장이 끝나는 순간 이 방은 룸서버 정리 대상이 되고, 그 정리는 2초마다 돈다.
        //  그래서 우리는 정리 주기보다 짧게만 기다린다 — 기다리는 동안 파드가 지워질 가능성을
        //  일부러 줄이는 것이지, 지워지지 않는다고 보장하는 게 아니다.
        private async UniTaskVoid CloseRoomAsync()
        {
            //  하트비트부터 멈춘다. 아래 백엔드 호출이 실패/타임아웃해도(우리는 아직 살아있는데)
            //  하트비트가 계속 나가면 방이 영원히 "진행 중"으로 보여, 하트비트 만료 정리도
            //  로비 자가치유도 절대 발동하지 않는다 — 성공 경로에서는 이미 Closed라 안전하다.
            CancelInvoke("SendHeartbeat");

            if (!EnvironmentSettings.active.Standalone)
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(CLOSE_TIMEOUT_SECONDS));

                    await WebAPI.UpdateRoomStatus(new UpdateRoomStatusRequest
                    {
                        roomId = roomDataStore.room.id,
                        status = RoomStatus.Closed,
                    }, cts.Token);
                }
                catch (Exception e)
                {
                    //  실패해도 통보는 강행한다 — 클라를 끝난 방에 가둬 두는 쪽이 더 나쁘고,
                    //  그 경우는 룸서버의 하트비트 만료 정리가 받아 준다.
                    Debug.LogError($"Failed to close room. Notifying clients anyway. Error: {e.Message}");
                }
            }

            foreach (var session in sessionManager.GetAllSessions())
            {
                session.Send(new MatchEndedToC());
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
