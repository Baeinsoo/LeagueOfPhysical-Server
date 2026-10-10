using System.Collections.Generic;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 그 판의 룩(슬롯·이름·레벨)이 생성 데이터를 타고 월드 엔티티 → 와이어까지 가는 길.
    ///
    /// <para><b>사람이 아닌 몸은 룩이 없다.</b> <see cref="PlayerLookAttach"/>는 룩이 null이면
    /// 아무 일도 하지 않는다 — 몬스터·심판에 "플레이어 N" 이름표가 붙으면 안 되기 때문이다.</para>
    /// </summary>
    public class CreationDataLookTests
    {
        [Test]
        public void 룩이_있으면_엔티티에_붙고_true를_돌려준다()
        {
            var entity = new GameFramework.World.Entity("e1");
            var look = new PlayerLook(new Dictionary<string, string> { ["hat"] = "hat_cube_red" }, "Kim", 7);

            bool attached = PlayerLookAttach.Attach(entity, look);

            Assert.IsTrue(attached);
            Assert.AreSame(look, entity.Get<PlayerLook>());
        }

        [Test]
        public void 룩이_null이면_아무것도_붙이지_않고_false를_돌려준다()
        {
            var entity = new GameFramework.World.Entity("e1");

            bool attached = PlayerLookAttach.Attach(entity, null);

            Assert.IsFalse(attached);
            Assert.IsNull(entity.Get<PlayerLook>());
        }

        [Test]
        public void 생성_데이터가_룩을_와이어_필드로_그대로_옮긴다()
        {
            var entity = new GameFramework.World.Entity("e1");
            entity.Add(new GameFramework.World.Transform());
            entity.Add(new GameFramework.World.Velocity());
            entity.Add(new Appearance("visual"));
            entity.Add(new PlayerLook(
                new Dictionary<string, string> { ["hat"] = "hat_cube_red", ["top"] = "top_tint_blue" },
                "Kim", 7));

            var creationData = new CharacterCreationDataCreator().Create(entity);

            //  슬롯을 하나만 단언하면 "한 슬롯만 옮기고 나머지는 빠뜨리는" 구현도 통과한다 —
            //  두 슬롯 다 확인해야 전체 Slots를 순회해 옮기는지가 드러난다.
            Assert.AreEqual(2, creationData.CharacterCreationData.Look.Count);
            Assert.AreEqual("hat_cube_red", creationData.CharacterCreationData.Look["hat"]);
            Assert.AreEqual("top_tint_blue", creationData.CharacterCreationData.Look["top"]);
            Assert.AreEqual("Kim", creationData.CharacterCreationData.DisplayName);
            Assert.AreEqual(7, creationData.CharacterCreationData.AccountLevel);
        }

        /// <summary>
        /// 아바타가 없는 모드(판치기)의 실제 크리에이터를 거쳐도 같은 규칙이 지켜지는지 —
        /// 마스터데이터가 필요 없는 유일한 크리에이터라 실제 클래스로 검증할 수 있다.
        /// 나머지 네 크리에이터(CharacterCreator/ArcheryPlayerCreator/FlappyBirdCreator/
        /// SkydivePlayerCreator)는 마스터데이터 로드가 있어야 실제 경로를 못 만든다 — A4 리뷰
        /// 라운드1 보고서의 "우려 사항" 참고.
        /// </summary>
        [Test]
        public void 판치기_크리에이터는_룩이_있으면_실제로_붙인다()
        {
            var entityRegistry = new GameFramework.World.EntityRegistry();
            var creator = new PanchigiPlayerCreator(entityRegistry);
            var look = new PlayerLook(new Dictionary<string, string>(), "Kim", 3);

            creator.Create(new CharacterCreationData
            {
                userId = "user-a",
                entityId = "e3",
                visualId = "",
                characterCode = "",
                look = look,
            });

            Assert.AreSame(look, entityRegistry.Get("e3").Get<PlayerLook>());
        }

        [Test]
        public void 판치기_크리에이터는_룩이_없으면_안_붙인다()
        {
            var entityRegistry = new GameFramework.World.EntityRegistry();
            var creator = new PanchigiPlayerCreator(entityRegistry);

            creator.Create(new CharacterCreationData
            {
                userId = null,
                entityId = "e4",
                visualId = "",
                characterCode = "",
            });

            Assert.IsNull(entityRegistry.Get("e4").Get<PlayerLook>());
        }

        //  로비 응답이 슬롯 값에 null을 실어 보낼 수 있다(M1) — PlayerLook 생성자가 그 값을
        //  그대로 복사해 들여오므로(Dictionary<string,string>은 null 값을 허용), 그 자리를
        //  proto MapField.Add(key, null)로 그대로 옮기면 null을 거부하는 MapField가 예외를 던진다.
        //  그런 슬롯은 "장착 없음"으로 보고 건너뛰어야 한다.
        [Test]
        public void 슬롯_값이_null이면_건너뛴다()
        {
            var entity = new GameFramework.World.Entity("e5");
            entity.Add(new GameFramework.World.Transform());
            entity.Add(new GameFramework.World.Velocity());
            entity.Add(new Appearance("visual"));
            entity.Add(new PlayerLook(
                new Dictionary<string, string> { ["hat"] = "hat_cube_red", ["top"] = null },
                "Kim", 7));

            var creationData = new CharacterCreationDataCreator().Create(entity);

            Assert.AreEqual(1, creationData.CharacterCreationData.Look.Count);
            Assert.AreEqual("hat_cube_red", creationData.CharacterCreationData.Look["hat"]);
        }

        [Test]
        public void 룩이_없는_엔티티는_빈_맵_빈_이름_레벨0을_내보낸다()
        {
            var entity = new GameFramework.World.Entity("e2");
            entity.Add(new GameFramework.World.Transform());
            entity.Add(new GameFramework.World.Velocity());
            entity.Add(new Appearance("visual"));

            var creationData = new CharacterCreationDataCreator().Create(entity);

            Assert.AreEqual(0, creationData.CharacterCreationData.Look.Count);
            Assert.AreEqual("", creationData.CharacterCreationData.DisplayName);
            Assert.AreEqual(0, creationData.CharacterCreationData.AccountLevel);
        }
    }
}
