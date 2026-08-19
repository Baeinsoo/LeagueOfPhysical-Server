using Cysharp.Threading.Tasks;
using GameFramework.Http;
using MessagePipe;
using System.Threading;

namespace LOP
{
    public class WebAPI
    {
        //  게임서버가 백엔드에 거는 모든 호출은 서비스 간 호출이다 — 키를 한 곳에서 붙인다.
        //  호출부마다 헤더를 손으로 넣으면 새 API를 추가할 때 빠뜨린다.
        private static readonly HttpClient httpClient = new HttpClient(
            new ApiKeyHandler(new UnityWebRequestHandler(), "X-Internal-Api-Key",
                () => System.Environment.GetEnvironmentVariable("INTERNAL_API_KEY")));

        //  응답을 역직렬화한 뒤 전역 발행까지 한다 — 데이터 스토어가 이걸 구독해 상태를 채운다.
        private static async UniTask<T> SendAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            T response = await httpClient.SendAsync<T>(request, cancellationToken);
            GlobalMessagePipe.GetPublisher<T>().Publish(response);
            return response;
        }

        #region Room
        public static UniTask<HttpResponse> Heartbeat(string roomId, CancellationToken cancellationToken = default)
            => SendAsync<HttpResponse>(
                HttpRequestMessage.Put($"{EnvironmentSettings.active.roomBaseURL}/internal/room/heartbeat/{roomId}"), cancellationToken);


        public static UniTask<UpdateRoomStatusResponse> UpdateRoomStatus(UpdateRoomStatusRequest request, CancellationToken cancellationToken = default)
            => SendAsync<UpdateRoomStatusResponse>(
                HttpRequestMessage.Put($"{EnvironmentSettings.active.roomBaseURL}/internal/room/status", request), cancellationToken);

        public static UniTask<GetRoomResponse> GetRoom(string roomId, CancellationToken cancellationToken = default)
            => SendAsync<GetRoomResponse>(
                HttpRequestMessage.Get($"{EnvironmentSettings.active.roomBaseURL}/internal/room/{roomId}"), cancellationToken);
        #endregion

        #region Match
        public static UniTask<GetMatchResponse> GetMatch(string matchId, CancellationToken cancellationToken = default)
            => SendAsync<GetMatchResponse>(
                HttpRequestMessage.Get($"{EnvironmentSettings.active.matchmakingBaseURL}/match/{matchId}"), cancellationToken);

        //  결과는 lobby가 받는다 — 레이팅과 유저 데이터의 주인이고, 확정 세 가지(매치 상태·참가자·
        //  점수)가 거기서 한 트랜잭션에 들어간다.
        public static UniTask<ReportMatchResultResponse> ReportMatchResult(string matchId, ReportMatchResultRequest request, CancellationToken cancellationToken = default)
            => SendAsync<ReportMatchResultResponse>(
                HttpRequestMessage.Post($"{EnvironmentSettings.active.lobbyBaseURL}/internal/match/{matchId}/result", request), cancellationToken);
        #endregion

        #region Auth
        //  전역 발행(SendAsync<T>)을 쓰지 않는다 — 구독자가 없는데 GlobalMessagePipe.GetPublisher<T>를
        //  도는 것은 IL2CPP에서 open generic 미지원으로 터질 수 있고, 브로커를 등록할 이유도 없다.
        public static UniTask<IntrospectResponse> Introspect(string accessToken, CancellationToken cancellationToken = default)
        {
            var request = HttpRequestMessage.Post(
                $"{EnvironmentSettings.active.lobbyBaseURL}/internal/auth/introspect",
                new IntrospectRequest { token = accessToken });

            return httpClient.SendAsync<IntrospectResponse>(request, cancellationToken);
        }
        #endregion
    }
}
