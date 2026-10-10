using GameFramework;
using GameFramework.Http;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 룩 조회 성공 응답이 실제로 RoomDataStore.looks에 들어가는 길. 그동안 null(실패) 경로만
    /// 테스트됐다 — 백엔드가 보내는 리터럴을 그대로 역직렬화해 구독·배선(HandleGetMatchLooks)까지
    /// 실제로 통과시킨다. 브로커는 production이 쓰는 그대로(OrderedMessageBroker)를 쓴다 —
    /// 손으로 만든 가짜 ISubscriber는 production이 실제로 구독에 거는 Subscribe(Action&lt;T&gt;)
    /// 확장 메서드 경로를 타지 않아 배선 자체가 검증되지 않는다.
    /// </summary>
    public class RoomDataStoreLooksTests
    {
        static RoomDataStore NewStore(out OrderedMessageBroker<GetMatchLooksResponse> looksBroker)
        {
            looksBroker = new OrderedMessageBroker<GetMatchLooksResponse>();
            return new RoomDataStore(
                new OrderedMessageBroker<GetMatchResponse>(),
                looksBroker,
                new OrderedMessageBroker<GetRoomResponse>(),
                new OrderedMessageBroker<UpdateRoomStatusResponse>());
        }

        [Test]
        public void 성공_응답이_looks에_그대로_들어간다()
        {
            var store = NewStore(out var looksBroker);
            var response = HttpJson.DeserializeObject<GetMatchLooksResponse>(
                "{\"code\":200,\"looks\":{\"u\":{\"displayName\":\"Kim\",\"level\":3,\"slots\":{\"hat\":\"hat_cube_red\"}}}}");

            looksBroker.Publish(response);

            Assert.AreEqual("Kim", store.looks["u"].displayName);
            Assert.AreEqual(3, store.looks["u"].level);
            Assert.AreEqual("hat_cube_red", store.looks["u"].slots["hat"]);
        }

        [Test]
        public void 실패_코드면_looks는_null()
        {
            var store = NewStore(out var looksBroker);
            var response = HttpJson.DeserializeObject<GetMatchLooksResponse>("{\"code\":20000}");

            looksBroker.Publish(response);

            Assert.IsNull(store.looks);
        }

        [Test]
        public void Clear하면_looks는_null()
        {
            var store = NewStore(out var looksBroker);
            var response = HttpJson.DeserializeObject<GetMatchLooksResponse>(
                "{\"code\":200,\"looks\":{\"u\":{\"displayName\":\"Kim\",\"level\":3,\"slots\":{}}}}");
            looksBroker.Publish(response);

            store.Clear();

            Assert.IsNull(store.looks);
        }
    }
}
