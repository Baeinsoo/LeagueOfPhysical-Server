using System;

namespace LOP
{
    /// <summary>로비 <c>PUT /internal/user/location/game-room-ended</c> 본문 — 각 사람이 그 방에 있을 때만 위치를 비운다.</summary>
    [Serializable]
    public class GameRoomEndedRequest
    {
        public GameRoomEndedEntry[] entries;
    }

    [Serializable]
    public class GameRoomEndedEntry
    {
        public string userId;
        public string roomId;
    }
}
