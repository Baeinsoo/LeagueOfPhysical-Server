using System.Collections.Generic;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>룩 조회 결과(looks)에서 유저의 "그 판 룩"을 만드는 순수 규칙. 없는 유저·조회 실패는 기본값으로.</summary>
    public class PlayerLookResolverTests
    {
        static Dictionary<string, PlayerLookDto> Looks(params (string userId, PlayerLookDto dto)[] rows)
        {
            var d = new Dictionary<string, PlayerLookDto>();
            foreach (var (userId, dto) in rows) d[userId] = dto;
            return d;
        }

        [Test]
        public void 있는_유저는_조회된_슬롯_이름_레벨_그대로()
        {
            var dto = new PlayerLookDto
            {
                displayName = "용감한전사",
                level = 7,
                slots = new Dictionary<string, string> { ["hat"] = "hat_001", ["top"] = "top_002" },
            };
            var looks = Looks(("user-a", dto));

            var look = PlayerLookResolver.Resolve(looks, "user-a", 0);

            Assert.AreEqual("용감한전사", look.DisplayName);
            Assert.AreEqual(7, look.AccountLevel);
            Assert.AreEqual("hat_001", look.SlotOrNull("hat"));
            Assert.AreEqual("top_002", look.SlotOrNull("top"));
        }

        [Test]
        public void 없는_유저는_기본_이름_레벨1_빈_슬롯()
        {
            var looks = Looks(("user-a", new PlayerLookDto { displayName = "용감한전사", level = 7, slots = null }));

            var look = PlayerLookResolver.Resolve(looks, "user-b", 2);

            Assert.AreEqual("플레이어 3", look.DisplayName);
            Assert.AreEqual(1, look.AccountLevel);
            Assert.AreEqual(0, look.Slots.Count);
        }

        [Test]
        public void looks가_null이면_기본값()
        {
            var look = PlayerLookResolver.Resolve(null, "user-a", 0);

            Assert.AreEqual("플레이어 1", look.DisplayName);
            Assert.AreEqual(1, look.AccountLevel);
            Assert.AreEqual(0, look.Slots.Count);
        }

        [Test]
        public void 슬롯이_null인_dto도_빈_슬롯으로_정규화()
        {
            var looks = Looks(("user-a", new PlayerLookDto { displayName = "이름", level = 3, slots = null }));

            var look = PlayerLookResolver.Resolve(looks, "user-a", 0);

            Assert.AreEqual(0, look.Slots.Count);
            Assert.AreEqual("이름", look.DisplayName);
            Assert.AreEqual(3, look.AccountLevel);
        }

        //  백엔드가 유저 키는 있는데 값 자체가 null인 행을 보낼 수 있다(M1) — TryGetValue는 성공해도
        //  dto가 null이면 "조회 안 됨"과 같게 기본값으로 가야 한다. 그대로 쓰면 dto.displayName에서
        //  NullReferenceException이 난다.
        [Test]
        public void dto_자체가_null이면_기본값()
        {
            var looks = new Dictionary<string, PlayerLookDto> { ["user-a"] = null };

            var look = PlayerLookResolver.Resolve(looks, "user-a", 0);

            Assert.AreEqual("플레이어 1", look.DisplayName);
            Assert.AreEqual(1, look.AccountLevel);
            Assert.AreEqual(0, look.Slots.Count);
        }

        //  userId가 null이면(몬스터·심판 등) looks에 뭐가 있든 조회를 시도하지 않고 기본값으로 간다.
        [Test]
        public void userId가_null이면_looks에_값이_있어도_기본값()
        {
            var looks = Looks(("user-a", new PlayerLookDto { displayName = "용감한전사", level = 7, slots = null }));

            var look = PlayerLookResolver.Resolve(looks, null, 0);

            Assert.AreEqual("플레이어 1", look.DisplayName);
            Assert.AreEqual(1, look.AccountLevel);
            Assert.AreEqual(0, look.Slots.Count);
        }

        //  백엔드가 유저 행을 못 찾으면 displayName: ''(빈 문자열, null 아님)을 보낸다 — 그대로 쓰면
        //  빈 이름표가 뜬다. 슬롯·레벨은 조회된 값 그대로 두고 이름만 기본값으로 대체해야 한다.
        [TestCase("")]
        [TestCase(" ")]
        [TestCase(null)]
        public void 백엔드_이름이_비었으면_기본_이름으로_대체하되_슬롯_레벨은_유지(string blankName)
        {
            var dto = new PlayerLookDto
            {
                displayName = blankName,
                level = 5,
                slots = new Dictionary<string, string> { ["hat"] = "hat_009" },
            };
            var looks = Looks(("user-a", dto));

            var look = PlayerLookResolver.Resolve(looks, "user-a", 2);

            Assert.AreEqual("플레이어 3", look.DisplayName);
            Assert.AreEqual(5, look.AccountLevel);
            Assert.AreEqual("hat_009", look.SlotOrNull("hat"));
        }
    }
}
