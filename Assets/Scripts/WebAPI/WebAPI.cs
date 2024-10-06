using GameFramework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public class WebAPI
    {
        #region Room
        public static WebRequest<HttpResponse> Heartbeat(string roomId)
        {
            return new WebRequestBuilder<HttpResponse>()
                .SetUri($"{EnvironmentSettings.active.roomBaseURL}/room/heartbeat/{roomId}")
                .SetMethod(HttpMethod.PUT)
                .Build();
        }

        public static WebRequest<string> NotifyStartServer(NotifyStartServerRequest request)
        {
            return new WebRequestBuilder<string>()
                .SetUri($"{EnvironmentSettings.active.roomBaseURL}/room")
                .SetMethod(HttpMethod.PUT)
                .SetRequestBody(request)
                .Build();
        }

        public static WebRequest<string> NotifyStopServer(string roomId)
        {
            return new WebRequestBuilder<string>()
                .SetUri($"{EnvironmentSettings.active.roomBaseURL}/room/{roomId}")
                .SetMethod(HttpMethod.DELETE)
                .Build();
        }

        public static WebRequest<UpdateRoomStatusResponse> UpdateRoomStatus(UpdateRoomStatusRequest request)
        {
            return new WebRequestBuilder<UpdateRoomStatusResponse>()
                .SetUri($"{EnvironmentSettings.active.roomBaseURL}/room/status")
                .SetMethod(HttpMethod.PUT)
                .SetRequestBody(request)
                .Build();
        }

        public static WebRequest<GetRoomResponse> GetRoom(string roomId)
        {
            return new WebRequestBuilder<GetRoomResponse>()
                .SetUri($"{EnvironmentSettings.active.roomBaseURL}/room/{roomId}")
                .SetMethod(HttpMethod.GET)
                .Build();
        }
        #endregion

        #region Match
        public static WebRequest<GetMatchResponse> GetMatch(string matchId)
        {
            return new WebRequestBuilder<GetMatchResponse>()
                .SetUri($"{EnvironmentSettings.active.matchmakingBaseURL}/match/{matchId}")
                .SetMethod(HttpMethod.GET)
                .Build();
        }
        #endregion
    }
}
