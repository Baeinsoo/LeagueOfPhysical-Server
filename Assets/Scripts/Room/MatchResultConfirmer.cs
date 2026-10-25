using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 판 결과를 확정한다. 봇이 낀 연습 판은 백엔드에 보고하지 않는다 — 백엔드가 모르는 bot- 아이디가
    /// 랭크·재화 계산으로 흘러가면 안 된다. 보고 함수를 주입받아 시험에서 호출 여부를 셀 수 있다.
    /// </summary>
    public static class MatchResultConfirmer
    {
        public static async UniTask<ConfirmedParticipantDto[]> ConfirmAsync(
            MatchOutcome outcome, Func<MatchOutcome, UniTask<ReportMatchResultResponse>> report, bool standalone = false)
        {
            if (MatchEndedMessageBuilder.IsPractice(outcome))
            {
                return MatchEndedMessageBuilder.PracticeParticipants(outcome);
            }

            //  단독 실행은 백엔드가 없다 — 사람만 있는 판은 보고할 곳이 없으니 빈 결과로 둔다.
            if (standalone) return null;

            var response = await report(outcome);

            //  거절은 HTTP 200에 body의 code로 온다.
            if (response.code != 200)
            {
                Debug.LogError($"Match result report rejected by backend. code={response.code}");
                return null;
            }
            return response.participants;
        }
    }
}
