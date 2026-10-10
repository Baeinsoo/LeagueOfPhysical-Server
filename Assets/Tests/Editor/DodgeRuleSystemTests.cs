using System.Collections.Generic;
using LOP.Event.Entity;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    //  몸을 어디에 세우나. 맵이 자리를 정하고, 자리가 없거나 모자라도 판이 멈추면 안 된다.
    public class DodgeRuleSystemTests
    {
        //  크리에이터가 실제로 무엇을 받았는지 기록만 한다 — EntitySpawnerTests의 Fake 패턴과 같되
        //  빈 Create 대신 받은 값을 보관해 룰 시스템이 넘긴 look을 들여다볼 수 있게 한다.
        private sealed class CapturingCharacterCreator : ICharacterCreator
        {
            public readonly List<CharacterCreationData> Created = new List<CharacterCreationData>();
            public void Create(CharacterCreationData creationData) => Created.Add(creationData);
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
            public IReadOnlyDictionary<string, PlayerLookDto> looks { get; set; }
            public void Clear() { }
        }

        private static DodgeConfig TestConfig() => new DodgeConfig(
            lives: 3, invulnerableSeconds: 1.5f, hitRadius: 0.3f, leadSeconds: 0.5f, arenaHalf: 9f,
            tileCount: 6, firstPatternDelaySeconds: 1f, patternIntervalSeconds: 2f, onlyKind: 0,
            warnSeconds: 1f, bulletSpeed: 10f, bulletRadius: 0.3f, bombRadius: 1f, bombActiveSeconds: 0.5f,
            laserWidth: 0.3f, laserOnSeconds: 0.3f, rockSpeed: 8f, rockRadius: 1f, tileOnSeconds: 1f);

        /// <summary>
        /// 룰 시스템이 스폰에 넘기는 <see cref="CharacterCreationData.look"/>을 들여다본다 — 이 단언이
        /// 없으면 <see cref="DodgeRuleSystem"/>의 "look = PlayerLookResolver.Resolve(...)" 줄이나
        /// 크리에이터의 <see cref="PlayerLookAttach"/> 호출이 지워져도 아무 테스트도 안 깨진다.
        /// </summary>
        [Test]
        public void 초기화하면_유저_몸은_기본_룩을_받고_심판은_룩이_없다()
        {
            var creator = new CapturingCharacterCreator();
            var spawner = new EntitySpawner(
                sessionManager: null,
                entityRegistry: new GameFramework.World.EntityRegistry(),
                characterCreator: creator,
                itemCreator: null,
                coinCreator: null,
                entityCreatedPublisher: new FakePublisher<EntityCreated>(),
                entityDestroyedPublisher: new FakePublisher<EntityDestroyed>());

            var roomDataStore = new FakeRoomDataStore
            {
                match = new Match { playerList = new[] { "user-a", "user-b" } },
                looks = null,   // 로비 조회 실패/미조회 — 전원 기본 룩으로 가야 한다
            };

            var rule = new DodgeRuleSystem(roomDataStore, spawner, new DodgeMatchState(), TestConfig());

            rule.Initialize();

            //  유저 둘 + 심판 하나.
            Assert.AreEqual(3, creator.Created.Count);

            var userA = creator.Created[0];
            Assert.AreEqual("user-a", userA.userId);
            Assert.IsNotNull(userA.look);
            Assert.AreEqual("플레이어 1", userA.look.DisplayName);
            Assert.AreEqual(1, userA.look.AccountLevel);

            var userB = creator.Created[1];
            Assert.AreEqual("user-b", userB.userId);
            Assert.IsNotNull(userB.look);
            Assert.AreEqual("플레이어 2", userB.look.DisplayName);   // rosterIndex가 제대로 넘어가는지도 함께 본다

            var referee = creator.Created[2];
            Assert.IsNull(referee.userId);
            Assert.IsNull(referee.look);   // 심판은 선수가 아니다 — 이름표가 붙으면 안 된다
        }

        /// <summary>
        /// 위 테스트는 looks가 null인 경로만 본다 — 로비 조회가 실제로 성공했을 때
        /// (<see cref="DodgeRuleSystem.Initialize"/>가 roomDataStore.looks를 그대로 넘기는 경로)는
        /// 그동안 아무 테스트도 덮지 않았다. 조회에 없는 유저는 여전히 기본값("플레이어 N")을 받는지도 같이 본다.
        /// </summary>
        [Test]
        public void 초기화하면_조회된_룩이_실제로_실린다()
        {
            var creator = new CapturingCharacterCreator();
            var spawner = new EntitySpawner(
                sessionManager: null,
                entityRegistry: new GameFramework.World.EntityRegistry(),
                characterCreator: creator,
                itemCreator: null,
                coinCreator: null,
                entityCreatedPublisher: new FakePublisher<EntityCreated>(),
                entityDestroyedPublisher: new FakePublisher<EntityDestroyed>());

            var looks = new Dictionary<string, PlayerLookDto>
            {
                ["user-a"] = new PlayerLookDto
                {
                    displayName = "Kim",
                    level = 3,
                    slots = new Dictionary<string, string> { ["hat"] = "hat_cube_red" },
                },
            };

            var roomDataStore = new FakeRoomDataStore
            {
                match = new Match { playerList = new[] { "user-a", "user-b" } },
                looks = looks,
            };

            var rule = new DodgeRuleSystem(roomDataStore, spawner, new DodgeMatchState(), TestConfig());

            rule.Initialize();

            var userA = creator.Created[0];
            Assert.AreEqual("Kim", userA.look.DisplayName);
            Assert.AreEqual(3, userA.look.AccountLevel);
            Assert.AreEqual("hat_cube_red", userA.look.SlotOrNull("hat"));

            //  조회 결과에 없는 유저는 기본값으로 — rosterIndex(1)에서 "플레이어 2".
            var userB = creator.Created[1];
            Assert.AreEqual("플레이어 2", userB.look.DisplayName);
        }

        [Test]
        public void 자리가_있으면_순서대로_쓴다()
        {
            var slots = new List<Vector3> { new Vector3(1, 0, 0), new Vector3(2, 0, 0) };
            Assert.AreEqual(slots[0], DodgeRuleSystem.SpawnPositionFor(slots, 0));
            Assert.AreEqual(slots[1], DodgeRuleSystem.SpawnPositionFor(slots, 1));
        }

        [Test]
        public void 사람이_자리보다_많으면_돌려_쓴다()
        {
            var slots = new List<Vector3> { new Vector3(1, 0, 0), new Vector3(2, 0, 0) };
            Assert.AreEqual(slots[0], DodgeRuleSystem.SpawnPositionFor(slots, 2));
        }

        [Test]
        public void 자리가_없으면_바닥에_겹치지_않게_줄세운다()
        {
            var slots = new List<Vector3>();
            Vector3 a = DodgeRuleSystem.SpawnPositionFor(slots, 0);
            Vector3 b = DodgeRuleSystem.SpawnPositionFor(slots, 1);
            Assert.AreEqual(0f, a.y);
            Assert.AreEqual(0f, b.y);
            Assert.Greater(Vector3.Distance(a, b), 1.5f);
        }

        // 한 발 승부와 같은 치비 — 클라가 같은 키로 원격 그룹에서 받는다.
        [Test]
        public void 선수는_한_발_승부와_같은_치비다() =>
            Assert.AreEqual("Assets/Characters/Chibi/Chibi.prefab", DodgeRuleSystem.BodyVisualId);

        // 마지막 탈락 뒤 여운 — 자막(2.5초)과 들것(2초)이 다 보인 뒤에 결과로 넘어간다. 바로 끝내면 2인 판에서는 탈락 연출을 못 본다.
        [Test]
        public void 마지막_탈락_뒤_여운이_지나야_끝난다()
        {
            Assert.IsFalse(DodgeRuleSystem.MatchOver(2, 1, 100, 100));
            Assert.IsFalse(DodgeRuleSystem.MatchOver(2, 1, 100, 100 + DodgeRuleSystem.EndGraceTicks - 1));
            Assert.IsTrue(DodgeRuleSystem.MatchOver(2, 1, 100, 100 + DodgeRuleSystem.EndGraceTicks));
            Assert.GreaterOrEqual(DodgeRuleSystem.EndGraceTicks, 125);   // 자막 한 줄 2.5초(50Hz)
        }

        [Test]
        public void 둘_이상_살아_있으면_안_끝난다()
        {
            Assert.IsFalse(DodgeRuleSystem.MatchOver(2, 2, -1, 10000));
        }

        // 혼자 하는 판(최소 인원 1) — 살아 있는 동안은 계속, 탈락하면 같은 여운 뒤에 끝난다.
        [Test]
        public void 혼자면_탈락하고_여운_뒤에_끝난다()
        {
            Assert.IsFalse(DodgeRuleSystem.MatchOver(1, 1, -1, 10000));
            Assert.IsFalse(DodgeRuleSystem.MatchOver(1, 0, 100, 100 + DodgeRuleSystem.EndGraceTicks - 1));
            Assert.IsTrue(DodgeRuleSystem.MatchOver(1, 0, 100, 100 + DodgeRuleSystem.EndGraceTicks));
        }
    }
}
