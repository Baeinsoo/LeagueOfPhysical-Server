using GameFramework.World;
using LOP.Event.Entity;
using MessagePipe;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// Flappy Race 종료 여운(10-26 결정) — 전원 완주(또는 전원 정리) 조건이 참이 된 틱부터
    /// 3초(150틱, 50Hz)가 지나야 판이 끝난다. 그 전에 바로 끝내면 막 결승선을 넘은 사람이 자기
    /// FINISH 도장·등수 도장·기록 줄·NEW BEST!(완주 뒤 최대 2.55초 뒤에 뜬다)를 하나도 못 보고
    /// 결과 화면으로 넘어간다. MountedArcheryRuleSystem의 탈락 여운과 같은 자리·같은 이름이다.
    /// </summary>
    public class FlappyRaceRuleSystemTests
    {
        private sealed class FakeCharacterCreator : ICharacterCreator
        {
            public void Create(CharacterCreationData creationData) { }
        }

        private sealed class FakePublisher<T> : IPublisher<T>
        {
            public void Publish(T message) { }
        }

        private sealed class FakeRoomDataStore : IRoomDataStore
        {
            public Room room { get; set; }
            public Match match { get; set; }
            public MatchOutcome outcome { get; set; }
            public System.Collections.Generic.IReadOnlyDictionary<string, PlayerLookDto> looks { get; set; }
            public void Clear() { }
        }

        //  ChaserSystem이 보는 것은 GameplayStartTick뿐이다 — FlappyChaserSystemTests와 같은 가짜.
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

        //  대시·낙하와 무관해 자리채움 값으로 채운다 — 이 테스트는 추격자를 아예 안 쓴다(FlappyMapRulesField 기본값이 꺼짐).
        private static FlappyConfig Config()
            => new FlappyConfig(forwardSpeed: 11f, flapImpulse: 23f, gravity: 70f, maxFallSpeed: 30f,
                                bodyRadius: 0.45f, bodyHeight: 0.9f, restitution: 0.35f,
                                stunTime: 0.8f, invulnTime: 0.6f,
                                dashMult: 2f, dashDuration: 0.2f, dashChargeBase: 0f, dashChargeDive: 0f,
                                chaserStartX: 0f, chaserInitialSpeed: 10f, chaserAcceleration: 0f, chaserMaxSpeed: 10f);

        private static EntitySpawner Spawner(EntityRegistry registry)
            => new EntitySpawner(
                sessionManager: null,
                entityRegistry: registry,
                characterCreator: new FakeCharacterCreator(),
                itemCreator: null,
                coinCreator: null,
                entityCreatedPublisher: new FakePublisher<EntityCreated>(),
                entityDestroyedPublisher: new FakePublisher<EntityDestroyed>());

        private sealed class Fixture
        {
            public EntityRegistry Registry;
            public FinishTrackingSystem Finish;
            public FlappyRaceRuleSystem Rule;
        }

        //  전원 정리(EveryoneAccountedFor) 쪽은 chaserSystem.EliminatedOrder가 비어 있으면
        //  곧바로 거짓이라(해당 분기는 || 단락 평가로 건너뛴다) — 이 테스트들은 추격자 없이도
        //  AllWatchedFinished 경로 하나로 여운 로직을 그대로 가린다.
        private static Fixture Build()
        {
            var registry = new EntityRegistry();
            var finish = new FinishTrackingSystem(registry);
            var chaser = new FlappyChaserSystem(
                registry, new FakeWorld(), finish, Spawner(registry),
                new FinishLineBounds(FinishAxis.X), Config(), new FlappyMapRulesField());
            var rule = new FlappyRaceRuleSystem(
                new FakeRoomDataStore { match = new Match { playerList = new string[0] } },
                Spawner(registry), registry, finish, chaser, new FlappyComboTallySystem(registry));
            return new Fixture { Registry = registry, Finish = finish, Rule = rule };
        }

        private static Entity FinishedBird(EntityRegistry registry, string id, long finishedTick)
        {
            var e = new Entity(id);
            e.Add(new FinishState { FinishedTick = finishedTick });
            registry.Add(e);
            return e;
        }

        [Test]
        public void 전원_완주하고_3초가_되기_전에는_안_끝난다()
        {
            var f = Build();
            FinishedBird(f.Registry, "bird", finishedTick: 100);
            f.Finish.Watch("bird");

            f.Finish.Tick(100, 0.02f);
            Assert.IsTrue(f.Finish.AllWatchedFinished, "전원 완주가 100틱에 참이 된다");
            Assert.IsFalse(f.Rule.IsMatchOver, "참이 된 그 틱 그대로는 안 끝난다");

            f.Finish.Tick(100 + FlappyRaceRuleSystem.FinishGraceTicks - 1, 0.02f);
            Assert.IsFalse(f.Rule.IsMatchOver, "여운(3초=150틱)이 끝나기 1틱 전");
        }

        [Test]
        public void 전원_완주하고_3초가_지나면_끝난다()
        {
            var f = Build();
            FinishedBird(f.Registry, "bird", finishedTick: 100);
            f.Finish.Watch("bird");

            f.Finish.Tick(100, 0.02f);
            Assert.IsFalse(f.Rule.IsMatchOver);

            f.Finish.Tick(100 + FlappyRaceRuleSystem.FinishGraceTicks, 0.02f);
            Assert.IsTrue(f.Rule.IsMatchOver, "여운이 끝나는 바로 그 틱부터 끝난다");
        }

        [Test]
        public void 조건이_한_틱_깜빡여도_처음_참이_된_틱을_그대로_기억한다()
        {
            var f = Build();
            FinishedBird(f.Registry, "fast", finishedTick: 100);
            f.Finish.Watch("fast");

            //  100틱에 전원 완주 — 래치가 여기(100)에 걸려야 한다.
            f.Finish.Tick(100, 0.02f);
            Assert.IsFalse(f.Rule.IsMatchOver);

            //  뒤늦게 안 들어온 새가 끼어들어 조건이 거짓으로 돌아간다(깜빡임).
            var late = new Entity("late");
            late.Add(new FinishState { FinishedTick = FinishState.NotFinished });
            f.Registry.Add(late);
            f.Finish.Watch("late");
            f.Finish.Tick(105, 0.02f);
            Assert.IsFalse(f.Finish.AllWatchedFinished, "깜빡임 확인 — 조건이 다시 거짓이다");
            Assert.IsFalse(f.Rule.IsMatchOver);

            //  뒤늦은 새도 110틱에 들어와 다시 참이 된다 — 그래도 끝나는 기준은 처음 틱인 100이어야 한다.
            late.Get<FinishState>().FinishedTick = 110;
            f.Finish.Tick(100 + FlappyRaceRuleSystem.FinishGraceTicks, 0.02f);
            Assert.IsTrue(f.Rule.IsMatchOver,
                "래치가 100틱을 기억했다면 지금(100+여운)은 이미 끝나 있어야 한다 — " +
                "다시 참이 된 틱(110)으로 갱신됐다면 아직 안 끝난 걸로 잘못 나온다");
        }
    }
}
