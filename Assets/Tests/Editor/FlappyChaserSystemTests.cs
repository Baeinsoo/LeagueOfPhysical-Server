using GameFramework.World;
using LOP.Event.Entity;
using MessagePipe;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 추격자(뒤에서 오는 벽)가 누구를 잡는지. <b>맵이 추격자를 꺼 두면(FlappyMapRules.Chaser ==
    /// false) 아무도 안 잡혀야 한다</b> — 마커가 없는 맵(지금의 라이브 맵)이 이 상태다.
    ///
    /// <para>EntitySpawner는 세션·크리에이터 의존이 무거워 <see cref="EntitySpawnerTests"/>와
    /// 같은 가짜로 구성한다. Despawn은 registry에서 바로 빼지 않고 "지울 목록"에 넣기만 하므로,
    /// 탈락 여부는 <see cref="FlappyChaserSystem.IsEliminated"/>로 본다.</para>
    /// </summary>
    public class FlappyChaserSystemTests
    {
        private sealed class FakeCharacterCreator : ICharacterCreator
        {
            public void Create(CharacterCreationData creationData) { }
        }

        private sealed class FakePublisher<T> : IPublisher<T>
        {
            public void Publish(T message) { }
        }

        //  ChaserSystem이 보는 것은 GameplayStartTick뿐이다 — 나머지는 인터페이스를 채우기만 한다.
        private sealed class FakeWorld : IWorld
        {
            public EntityRegistry EntityRegistry { get; } = new EntityRegistry();
            public WorldEventBuffer EventBuffer { get; } = new WorldEventBuffer();
            public long GameplayStartTick { get; set; } = 0;
            public long? FirstSavedTick => null;
            public long? LatestSavedTick => null;

            public void Tick(long tick, float deltaTime) { }
            public void SaveState(long tick) { }
            public bool LoadState(long tick) => false;
            public bool TryGetSavedMotion(long tick, string entityId, out GameFramework.Netcode.EntitySnapshot motion)
            {
                motion = default;
                return false;
            }
        }

        //  대시·낙하와 무관한 테스트라 자리채움 값으로 채운다. 추격자 값만 실제로 쓰인다.
        private static FlappyConfig Config()
            => new FlappyConfig(forwardSpeed: 11f, flapImpulse: 23f, gravity: 70f, maxFallSpeed: 30f,
                                bodyRadius: 0.45f, bodyHeight: 0.9f, restitution: 0.35f,
                                stunTime: 0.8f, invulnTime: 0.6f,
                                dashMult: 2f, dashDuration: 0.2f, dashChargeBase: 0f, dashChargeDive: 0f,
                                chaserStartX: 0f, chaserInitialSpeed: 10f, chaserAcceleration: 0f, chaserMaxSpeed: 10f);

        private static EntitySpawner Spawner()
            => new EntitySpawner(
                sessionManager: null,
                entityRegistry: new EntityRegistry(),
                characterCreator: new FakeCharacterCreator(),
                itemCreator: null,
                coinCreator: null,
                entityCreatedPublisher: new FakePublisher<EntityCreated>(),
                entityDestroyedPublisher: new FakePublisher<EntityDestroyed>());

        //  이름을 System으로 두면 System.Numerics 같은 네임스페이스 참조가 멤버 이름에 가려
        //  컴파일이 깨진다 — 짧게 쓰고 싶어도 피해야 한다.
        private static FlappyChaserSystem MakeSystem(EntityRegistry registry, FakeWorld world, FlappyMapRulesField rules,
                                                  FlappyConfig config, EntitySpawner spawner = null)
            => new FlappyChaserSystem(
                registry, world, new FinishTrackingSystem(registry), spawner ?? Spawner(),
                new FinishLineBounds(FinishAxis.X), config, rules);

        private static Entity BirdFarBehindWall(EntityRegistry registry, string id)
        {
            var bird = new Entity(id);
            bird.Add(new Transform { Position = new System.Numerics.Vector3(-1000f, 0f, 0f) });
            registry.Add(bird);
            return bird;
        }

        [Test]
        public void 추격자가_꺼지면_한참_뒤처진_새도_탈락하지_않는다()
        {
            var registry = new EntityRegistry();
            var world = new FakeWorld { GameplayStartTick = 0 };
            var rules = new FlappyMapRulesField();   // Set을 안 불렀으니 기본값(꺼짐)
            var system = MakeSystem(registry, world, rules, Config());
            BirdFarBehindWall(registry, "뒤처진새");
            system.Watch("뒤처진새");

            //  벽이 한참 전진할 시간을 준다 — 꺼져 있으면 아무리 틱을 돌려도 안 잡혀야 한다.
            for (long tick = 0; tick < 10000; tick++)
            {
                system.Tick(tick, 0.02f);
            }

            Assert.IsFalse(system.IsEliminated("뒤처진새"));
            Assert.AreEqual(0, system.EliminatedOrder.Count);
        }

        [Test]
        public void 추격자가_켜지면_한참_뒤처진_새는_탈락한다()
        {
            //  대조군 — 꺼짐 테스트가 "원래부터 안 잡히는 설정"이 아니었음을 보인다.
            var registry = new EntityRegistry();
            var world = new FakeWorld { GameplayStartTick = 0 };
            var rules = new FlappyMapRulesField();
            rules.Set(chaser: true, manualDash: false);
            var system = MakeSystem(registry, world, rules, Config());
            BirdFarBehindWall(registry, "뒤처진새");
            system.Watch("뒤처진새");

            for (long tick = 0; tick < 10000; tick++)
            {
                system.Tick(tick, 0.02f);
            }

            Assert.IsTrue(system.IsEliminated("뒤처진새"));
        }
    }
}
