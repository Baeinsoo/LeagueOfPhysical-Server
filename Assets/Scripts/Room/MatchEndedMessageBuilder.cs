using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 수신자마다 다른 결과 메시지를 만든다. 등수는 전원 것을 담지만 점수 변화는 본인 것만 담는다 —
    /// UI에서 가리는 것으로는 부족하고, 남의 실력 점수가 애초에 그 사람 회선으로 나가면 안 된다.
    /// 등수마다 그 판 룩·이름·레벨을 함께 실어, 클라가 결과 단계 시상대에 선수들을 제 모습으로 세운다.
    /// </summary>
    public static class MatchEndedMessageBuilder
    {
        public static MatchEndedToC Build(ConfirmedParticipantDto[] confirmed, string userId,
            IReadOnlyDictionary<string, PlayerLookDto> looks, IReadOnlyList<string> roster)
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

            for (int i = 0; i < confirmed.Length; i++)
            {
                var participant = confirmed[i];
                var info = new MatchPlacementInfo
                {
                    UserId = participant.userId,
                    Placement = participant.placement,
                };

                //  자루는 점수 개념이 없는 모드에선 null로 온다 — 그때는 빈 map을 그대로 내보낸다.
                //  proto의 map 필드는 대입이 아니라 Add로만 채울 수 있다.
                if (participant.stats != null)
                {
                    foreach (var pair in participant.stats)
                    {
                        info.Stats.Add(pair.Key, pair.Value);
                    }
                }

                var look = PlayerLookResolver.Resolve(looks, participant.userId, RosterIndex(roster, participant.userId, i));
                info.DisplayName = look.DisplayName;
                info.AccountLevel = look.AccountLevel;
                info.CharacterAppearance = new CharacterAppearanceInfo
                {
                    SkinColorId = look.CharacterAppearance.SkinColorId,
                    HairColorId = look.CharacterAppearance.HairColorId,
                };
                foreach (var slot in look.Slots)
                {
                    if (slot.Value != null) info.Look.Add(slot.Key, slot.Value);
                }

                message.Placements.Add(info);

                if (participant.userId == userId)
                {
                    message.HasRatingChange = true;
                    message.MyMmrBefore = participant.mmrBefore;
                    message.MyMmrAfter = participant.mmrAfter;
                }
            }

            return message;
        }

        //  방을 열 때 룩을 만든 순번(명단 순서)과 같아야 기본 이름 "플레이어 N"이 경기 중 이름표와 같다.
        private static int RosterIndex(IReadOnlyList<string> roster, string userId, int fallback)
        {
            if (roster != null)
            {
                for (int i = 0; i < roster.Count; i++)
                {
                    if (roster[i] == userId) return i;
                }
            }
            return fallback;
        }
    }
}
