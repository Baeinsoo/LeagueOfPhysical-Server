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
        private const double DRAIN_TIMEOUT_SECONDS = 10;
        //  CLOSE_TIMEOUT_SECONDS와 달리 이 값은 룸서버 정리 주기가 아니라 백엔드 트랜잭션
        //  상한(Prisma 기본 5초)과 견줘 정해진다 — 그보다 짧으면 호출자가 서버 처리가 끝나기도
        //  전에 먼저 포기한다. 재시도 1회까지 최악 2×6=12초라도 하트비트 만료 임계값(60초)에
        //  한참 못 미쳐 방이 좀비로 판정되지 않는다.
        private const double REPORT_TIMEOUT_SECONDS = 6.0;
        private const int REPORT_MAX_ATTEMPTS = 2;   //  보고는 멱등(백엔드가 저장된 결과를 그대로 반환)이라 재시도가 안전하다

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
            //  보고가 성공했을 때만 채워진다. 실패하면 null로 남아 아래 통보가 빈 결과로 나간다.
            ConfirmedParticipantDto[] confirmed = null;

            //  하트비트부터 멈춘다. 아래 백엔드 호출이 실패/타임아웃해도(우리는 아직 살아있는데)
            //  하트비트가 계속 나가면 방이 영원히 "진행 중"으로 보여, 하트비트 만료 정리도
            //  로비 자가치유도 절대 발동하지 않는다 — 성공 경로에서는 이미 Closed라 안전하다.
            CancelInvoke("SendHeartbeat");

            //  방을 닫기 전에 보고한다. Closed가 저장되는 순간 이 파드는 룸서버 정리 대상이 되고
            //  정리는 2초마다 도니, 닫은 뒤에 보고하면 파드가 사라져 결과가 영영 안 나간다.
            //  실패해도 아래 닫기·통보·배수·종료는 그대로 간다 — 클라를 끝난 방에 가둬 두는 쪽이
            //  더 나쁘다. 그 판은 점수 무변화로 남는다.
            if (!EnvironmentSettings.active.Standalone)
            {
                try
                {
                    //  등수는 러너가 LOPRunner.EndMatch()에서 이미 채워 뒀다.
                    var outcome = roomDataStore.outcome;
                    if (outcome == null)
                    {
                        //  러너를 거치지 않고 방이 닫히는 경로(초기화 실패 등)가 있다. 보고할 등수가
                        //  없으면 조용히 건너뛴다 — 없는 결과를 지어내지 않는다.
                        Debug.LogWarning("No match outcome to report. Skipping.");
                    }
                    else
                    {
                        var response = await ReportMatchResultAsync(outcome);

                        //  거절(명단 불일치·매치 없음 등)은 HTTP 자체는 200으로 오고 실패를
                        //  body의 code로 알린다 — HTTP 상태만 보면 거절을 조용히 놓친다.
                        if (response.code != 200)
                        {
                            Debug.LogError($"Match result report rejected by backend. code={response.code}");
                        }
                        else
                        {
                            confirmed = response.participants;
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to report match result. Continuing to close the room. Error: {e.Message}");
                }
            }

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
                //  한 세션 전송이 실패해도 나머지에겐 계속 보내야 한다 — 여기서 예외가 새면
                //  아래 배수·종료까지 못 가서, 결과를 못 받은 클라 하나 때문에 파드가 안 죽는다.
                //  그 클라는 결과를 못 받았으니 로비로 못 돌아가지만, 백엔드의 위치 자가치유가
                //  구해 준다.
                try
                {
                    session.Send(BuildMatchEndedMessage(confirmed, session.userId));
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to send MatchEndedToC to session {session.sessionId}. Error: {e.Message}");
                }
            }

            //  클라는 결과를 받으면 로비 씬을 로드하고, 그때 스스로 연결을 끊는다. 그러니
            //  "다 나갔다"가 곧 "다 받았다"는 뜻이라, 이걸 기다렸다 끄는 게 가장 안전하다.
            //  고정 시간만 기다렸다 끄면 아직 못 받은 클라의 소켓이 죽는데, 클라에는 끊김을
            //  처리하는 곳이 없어서(onStopClient 미사용) 그 사람은 끝난 방에 갇힌다.
            try
            {
                using var drainCts = new CancellationTokenSource(TimeSpan.FromSeconds(DRAIN_TIMEOUT_SECONDS));
                await UniTask.WaitUntil(
                    () => sessionManager.GetAllSessions().All(session => session.isConnected == false),
                    cancellationToken: drainCts.Token);
            }
            catch (OperationCanceledException)
            {
                //  느린 클라 하나 때문에 파드가 영원히 안 죽는 것만 막는다. 남은 사람은
                //  백엔드의 위치 자가치유가 로비에서 풀어 준다.
                Debug.LogWarning($"Drain timed out after {DRAIN_TIMEOUT_SECONDS}s. Quitting anyway.");
            }
            catch (Exception e)
            {
                //  예상 못 한 예외라도 여기서 삼켜야 한다 — 새면 이 메서드 전체가 죽어
                //  아래 Quit()까지 못 가고, 그게 이 변경 전체가 없애려던 좀비 파드다.
                Debug.LogError($"Unexpected error while draining sessions. Quitting anyway. Error: {e}");
            }

            //  스스로 빠진다 — 백엔드가 파드를 지워 주기를 기다리지 않는다. 백엔드가 죽어 있어도
            //  포트와 파드가 즉시 반납된다. (에디터에서는 no-op이라 플레이 모드가 안 꺼진다.)
            //  같은 namespace(LOP)에 MonoSingleton `LOP.Application`이 있어 짧은 이름은 그쪽으로
            //  잡힌다 — UnityEngine.Application을 풀네임으로 명시해야 한다.
            UnityEngine.Application.Quit();
        }

        //  보고는 멱등이다 — 두 번째 시도는 백엔드가 계산을 다시 하지 않고 이미 저장된 결과를
        //  그대로 돌려준다. 그래서 첫 시도가 타임아웃/네트워크 오류로 실패해도 한 번 더 시도해
        //  볼 가치가 있다. 마지막 시도까지 실패하면 예외를 그대로 던져 호출자(CloseRoomAsync)의
        //  catch가 로그를 남기고 방 닫기를 이어가게 한다.
        private async UniTask<ReportMatchResultResponse> ReportMatchResultAsync(MatchOutcome outcome)
        {
            var request = new ReportMatchResultRequest { participants = outcome.placements.ToArray() };

            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    using var reportCts = new CancellationTokenSource(TimeSpan.FromSeconds(REPORT_TIMEOUT_SECONDS));

                    return await WebAPI.ReportMatchResult(roomDataStore.match.id, request, reportCts.Token);
                }
                catch (Exception) when (attempt < REPORT_MAX_ATTEMPTS)
                {
                    Debug.LogWarning($"Match result report attempt {attempt} failed. Retrying.");
                }
            }
        }

        //  수신자마다 다른 메시지를 만든다. 등수는 전원 것을 담지만 점수 변화는 본인 것만 담는다 —
        //  UI에서 가리는 것으로는 부족하고, 남의 실력 점수가 애초에 그 사람 회선으로 나가면 안 된다.
        private static MatchEndedToC BuildMatchEndedMessage(ConfirmedParticipantDto[] confirmed, string userId)
        {
            var message = new MatchEndedToC();

            //  보고가 실패했으면 확정 결과가 없다. 빈 결과로 보내고 화면은 "매치 종료"로 물러선다.
            if (confirmed == null)
            {
                return message;
            }

            //  등수는 1부터다. 0이 섞여 오면 확정이 안 된 판을 확정된 것처럼 받은 것이므로 통째로
            //  버린다 — "0등"을 화면에 그리는 것보다 결과가 없다고 하는 편이 정직하다.
            foreach (var participant in confirmed)
            {
                if (participant.placement < 1)
                {
                    Debug.LogError($"Confirmed result has an invalid placement ({participant.placement}). Reporting no result.");
                    return new MatchEndedToC();
                }
            }

            foreach (var participant in confirmed)
            {
                message.Placements.Add(new MatchPlacementInfo
                {
                    UserId = participant.userId,
                    Placement = participant.placement,
                });

                if (participant.userId == userId)
                {
                    message.HasRatingChange = true;
                    message.MyMmrBefore = participant.mmrBefore;
                    message.MyMmrAfter = participant.mmrAfter;
                }
            }

            return message;
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
