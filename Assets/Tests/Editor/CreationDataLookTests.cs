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
            entity.Add(new PlayerLook(new Dictionary<string, string> { ["hat"] = "hat_cube_red" }, "Kim", 7));

            var creationData = new CharacterCreationDataCreator().Create(entity);

            Assert.AreEqual("hat_cube_red", creationData.CharacterCreationData.Look["hat"]);
            Assert.AreEqual("Kim", creationData.CharacterCreationData.DisplayName);
            Assert.AreEqual(7, creationData.CharacterCreationData.AccountLevel);
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
