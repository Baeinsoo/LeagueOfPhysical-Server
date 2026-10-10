using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// 로비에서 받은 looks 조회 결과에서 한 유저의 "그 판 룩"을 만든다. 조회에 없거나(마이그레이션 전 유저 등)
    /// 조회 자체가 실패해 looks가 null이어도 방은 열려야 하므로, 그런 경우는 빈 슬롯·기본 이름·레벨 1로 채운다.
    /// </summary>
    public static class PlayerLookResolver
    {
        public static PlayerLook Resolve(IReadOnlyDictionary<string, PlayerLookDto> looks, string userId, int rosterIndex)
        {
            if (looks != null && userId != null && looks.TryGetValue(userId, out var dto))
            {
                return new PlayerLook(dto.slots, dto.displayName, dto.level);
            }

            return new PlayerLook(null, $"플레이어 {rosterIndex + 1}", 1);
        }
    }
}
