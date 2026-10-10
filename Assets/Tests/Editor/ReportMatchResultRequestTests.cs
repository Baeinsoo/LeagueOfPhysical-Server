using GameFramework.Http;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 백엔드 DTO가 정확히 이 JSON 키(playedSeconds)를 읽는다. 와이어 양쪽이 서로 다른 레포라
    /// 필드 이름이 바뀌어도(리네임 등) 컴파일 에러가 안 난다 — 직렬화 결과 문자열로 계약을 고정한다.
    /// </summary>
    public class ReportMatchResultRequestTests
    {
        [Test]
        public void 직렬화_결과에_playedSeconds_키와_값이_그대로_실린다()
        {
            var request = new ReportMatchResultRequest
            {
                participants = new[] { new MatchPlacement { userId = "u", placement = 1, playedSeconds = 20 } }
            };

            string json = HttpJson.SerializeObject(request);

            StringAssert.Contains("\"playedSeconds\": 20", json);
        }
    }
}
