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
            //  dto 자체가 null일 수 있다(백엔드가 유저 키는 보냈는데 값이 null인 경우) — 그런 자리는
            //  "조회 안 됨"과 똑같이 기본값으로 가야 한다. 아니면 dto.displayName에서 예외가 난다.
            if (looks != null && userId != null && looks.TryGetValue(userId, out var dto) && dto != null)
            {
                //  백엔드가 유저 행을 못 찾으면 displayName: ''을 보낸다 — 그걸 그대로 쓰면 빈 이름표가
                //  뜬다. 슬롯·레벨은 조회된 값 그대로 두고 이름만 기본값으로 대체한다.
                string displayName = string.IsNullOrWhiteSpace(dto.displayName)
                    ? $"플레이어 {rosterIndex + 1}"
                    : dto.displayName;
                return new PlayerLook(dto.slots, displayName, dto.level);
            }

            return new PlayerLook(null, $"플레이어 {rosterIndex + 1}", 1);
        }
    }
}
