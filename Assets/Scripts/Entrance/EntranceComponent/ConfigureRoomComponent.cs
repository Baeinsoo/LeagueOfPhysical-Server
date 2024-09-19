using GameFramework;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;

namespace LOP
{
    public class ConfigureRoomComponent : IEntranceComponent
    {
        public async Task Execute()
        {
#if !UNITY_EDITOR
            var roomId = Environment.GetEnvironmentVariable("ROOM_ID");
            var matchId = Environment.GetEnvironmentVariable("MATCH_ID");
            var port = ushort.Parse(Environment.GetEnvironmentVariable("PORT"));
            var expectedPlayerList = new string[] { "375f9694a1e5c3af13ff9c75e11e1cb158f65521" };

            //var getMatch = LOPWebAPI.GetMatch(matchId);
            //yield return getMatch;

            //if (getMatch.isSuccess == false || getMatch.response.code != ResponseCode.SUCCESS)
            //{
            //    throw new System.Exception(getMatch.error);
            //}

            //var expectedPlayerList = getMatch.response.match.playerList;

            //SceneDataContainer.Get<MatchData>().matchId = getMatch.response.match.id;
            //SceneDataContainer.Get<MatchData>().matchSetting = new MatchSetting
            //{
            //    matchType = getMatch.response.match.matchType,
            //    subGameId = getMatch.response.match.subGameId,
            //    mapId = getMatch.response.match.mapId,
            //};
#else

            var roomId = "EditorTestRoom";
            var matchId = "EditorTestMatch";
            var port = (ushort)7777;
            var expectedPlayerList = new string[]
            {
                "375f9694a1e5c3af13ff9c75e11e1cb158f65521",
            };

            //SceneDataContainer.Get<MatchData>().matchId = "EditorTestMatch";
            //SceneDataContainer.Get<MatchData>().matchSetting = new MatchSetting
            //{
            //    matchType = MatchType.Friendly,
            //    subGameId = "FlapWang",
            //    mapId = "FlapWangMap",
            //};
#endif
            Blackboard.Write("roomInfo", (roomId, matchId, port, expectedPlayerList));
        }
    }
}
