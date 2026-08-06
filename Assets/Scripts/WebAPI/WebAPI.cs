using Cysharp.Threading.Tasks;
using GameFramework.Http;
using MessagePipe;
using System.Threading;

namespace LOP
{
    public class WebAPI
    {
        private static readonly HttpClient httpClient = new HttpClient(new UnityWebRequestHandler());

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
                HttpRequestMessage.Put($"{EnvironmentSettings.active.roomBaseURL}/room/heartbeat/{roomId}"), cancellationToken);


        public static UniTask<UpdateRoomStatusResponse> UpdateRoomStatus(UpdateRoomStatusRequest request, CancellationToken cancellationToken = default)
            => SendAsync<UpdateRoomStatusResponse>(
                HttpRequestMessage.Put($"{EnvironmentSettings.active.roomBaseURL}/room/status", request), cancellationToken);

        public static UniTask<GetRoomResponse> GetRoom(string roomId, CancellationToken cancellationToken = default)
            => SendAsync<GetRoomResponse>(
                HttpRequestMessage.Get($"{EnvironmentSettings.active.roomBaseURL}/room/{roomId}"), cancellationToken);
        #endregion

        #region Match
        public static UniTask<GetMatchResponse> GetMatch(string matchId, CancellationToken cancellationToken = default)
            => SendAsync<GetMatchResponse>(
                HttpRequestMessage.Get($"{EnvironmentSettings.active.matchmakingBaseURL}/match/{matchId}"), cancellationToken);
        #endregion
    }
}
