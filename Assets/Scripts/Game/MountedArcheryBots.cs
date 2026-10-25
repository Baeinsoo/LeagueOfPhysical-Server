using System;

namespace LOP
{
    /// <summary>시험용 봇의 이름 규칙과 "몇 명 세울까" 계산. 봇은 세션이 없고 아이디 접두(`bot-`)로만 사람과 가른다.</summary>
    public static class MountedArcheryBots
    {
        public const string IdPrefix = "bot-";

        /// <summary>이 모드의 최대 인원. 스위치 값이 이보다 커도 여기까지만 채운다.</summary>
        public const int MaxPlayers = 8;

        public static bool IsBot(string userId)
            => userId != null && userId.StartsWith(IdPrefix, StringComparison.Ordinal);

        public static string UserIdFor(int n) => IdPrefix + n;

        public static string DisplayNameFor(int n) => "봇 " + n;

        public static int CountToAdd(int fillTo, int humans)
            => Math.Max(0, Math.Min(fillTo, MaxPlayers) - humans);
    }
}
