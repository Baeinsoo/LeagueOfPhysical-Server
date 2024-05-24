using GameFramework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public class WebAPI
    {
        public static WebRequest<string> Heartbeat(string roomId)
        {
            return new WebRequestBuilder<string>()
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

        #region Match
        public static WebRequest<GetMatchResponse> GetMatch(string matchId)
        {
            return new WebRequestBuilder<GetMatchResponse>()
                .SetUri($"{EnvironmentSettings.active.roomBaseURL}/match/{matchId}")
                .SetMethod(HttpMethod.GET)
                .Build();
        }

        public static WebRequest<MatchStartResponse> MatchStart(MatchStartRequest request)
        {
            return new WebRequestBuilder<MatchStartResponse>()
                .SetUri($"{EnvironmentSettings.active.roomBaseURL}/match/match-start")
                .SetMethod(HttpMethod.PUT)
                .SetRequestBody(request)
                .Build();
        }

        public static WebRequest<MatchEndResponse> MatchEnd(MatchEndRequest request)
        {
            return new WebRequestBuilder<MatchEndResponse>()
                .SetUri($"{EnvironmentSettings.active.roomBaseURL}/match/match-end")
                .SetMethod(HttpMethod.PUT)
                .SetRequestBody(request)
                .Build();
        }
        #endregion
    }
}
