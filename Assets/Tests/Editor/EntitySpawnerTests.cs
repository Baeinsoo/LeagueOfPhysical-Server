using LOP.Event.Entity;
using MessagePipe;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// "이 사람의 몸이 뭐지"를 물었을 때의 규칙.
    ///
    /// <para><b>없으면 null이어야 한다.</b> 예전엔 대괄호 조회라 없으면 그 자리에서 예외를 던졌는데,
    /// 이걸 매 틱 도는 스냅샷 시스템이 부르기 때문에 판 전체가 얼어붙었다. 추격자에게 잡힌 사람은
    /// 몸이 없는 채로 접속해 있으므로(관전), 그 상태가 정상임을 여기서 못박는다.</para>
    /// </summary>
    public class EntitySpawnerTests
    {
        private sealed class FakeCharacterCreator : ICharacterCreator
        {
            public void Create(CharacterCreationData creationData) { }
        }

        private sealed class FakePublisher<T> : IPublisher<T>
        {
            public void Publish(T message) { }
        }

        //  스폰과 조회만 보는 테스트라 나머지 의존은 쓰이지 않는다.
        private static EntitySpawner Spawner()
            => new EntitySpawner(
                sessionManager: null,
                entityRegistry: new GameFramework.World.EntityRegistry(),
                characterCreator: new FakeCharacterCreator(),
                itemCreator: null,
                coinCreator: null,
                entityCreatedPublisher: new FakePublisher<EntityCreated>(),
                entityDestroyedPublisher: new FakePublisher<EntityDestroyed>());

        [Test]
        public void 몸을_받은_사람은_그_몸을_찾을_수_있다()
        {
            var spawner = Spawner();
            spawner.Spawn(new CharacterCreationData { userId = "사람A", entityId = "7" });

            Assert.AreEqual("7", spawner.GetEntityIdByUserId("사람A"));
        }

        [Test]
        public void 몸이_없는_사람을_물으면_null이다()
        {
            //  터지면 안 된다. 이 한 줄이 서버를 통째로 얼렸던 자리다.
            Assert.IsNull(Spawner().GetEntityIdByUserId("몸없는사람"));
        }

        [Test]
        public void 아무도_안_준_이름을_물어도_null이다()
        {
            //  세션이 있는데 아직 스폰 전인 순간이 실제로 있다.
            var spawner = Spawner();
            spawner.Spawn(new CharacterCreationData { userId = "사람A", entityId = "7" });

            Assert.IsNull(spawner.GetEntityIdByUserId("사람B"));
        }

        [Test]
        public void 이름이_null이어도_터지지_않는다()
        {
            //  딕셔너리는 null 키로 찾는 것 자체를 거부한다 — 조회 함수가 먼저 막아야 한다.
            Assert.IsNull(Spawner().GetEntityIdByUserId(null));
        }
    }
}
