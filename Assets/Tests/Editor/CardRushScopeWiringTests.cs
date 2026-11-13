using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Luban;
using LOP.Event.Entity;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;
using VContainer;

namespace LOP.Tests
{
    /// <summary>
    /// 등록↔소비 — 카드 러시 스코프가 넣은 것만으로 룰·월드·몸 생성기가 실제로 만들어지는지, 맵 마커가 규칙을 넣을 통로가
    /// 월드가 보는 것과 같은 하나인지 본다. 부모(Root·Room)와 공용 설치(GameplayInstaller)가 주는 것은 가짜로 채운다.
    /// 생성자에 새 의존을 더하고 등록을 빠뜨리면 여기서 Resolve가 터진다(실제로는 방이 열릴 때에야 터진다).
    /// </summary>
    public class CardRushScopeWiringTests
    {
        private const string Folder = "Packages/com.baegames.lop.masterdata.server/Runtime.Generated/StreamingAssets/MasterData/";

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

        private sealed class Seed : IMatchSeed { public ulong Value => 3UL; }

        //  LoadAsync는 UnityWebRequest라 EditMode에서 못 기다린다 — 패키지의 .bytes를 직접 읽어 같은 표를 만든다.
        //  봇 판 시험(CardRushBotMatchTests)도 출시 표 값을 여기서 읽는다.
        internal static LOP.MasterData.LOPMasterData RealMasterData()
        {
            var tables = new LOP.MasterData.Tables(file =>
                new ByteBuf(File.ReadAllBytes(Path.GetFullPath(Folder + file + ".bytes"))));
            var md = new LOP.MasterData.LOPMasterData();
            typeof(LOP.MasterData.LOPMasterData).GetProperty(nameof(LOP.MasterData.LOPMasterData.Tables))
                .SetValue(md, tables);
            return md;
        }

        private readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            spawned.Clear();
        }

        [Test]
        public void 설정은_마스터데이터_1행에서_온다()
        {
            var config = new CardRushConfigProvider(RealMasterData()).Get();
            Assert.AreEqual(6000L, config.MatchDurationTicks, "120초 × 50Hz");
        }

        //  열마다 다른 값을 넣은 1행 — 제공자가 인수 둘을 바꿔 넘기면(같은 float라 컴파일은 된다) 어느 칸이든 값이 어긋난다.
        private static readonly float[] DistinctRow =
        {
            111f, 150f, 10.5f, 16.5f, 0.31f, 27f, 47f, 1.9f, 1.3f, 1.1f, 2.6f, 1.4f,
            0.21f, 0.43f, 0.17f, 0.03f, 0.66f, 0.19f, 0.7f,
        };
        private const int DistinctLanes = 5;

        //  실제 표들은 그대로 읽고 tbcardrushconfig만 위 행 하나로 갈아 끼운다.
        private static LOP.MasterData.LOPMasterData MasterDataWithDistinctRow()
        {
            var tables = new LOP.MasterData.Tables(file =>
            {
                if (file != "tbcardrushconfig")
                    return new ByteBuf(File.ReadAllBytes(Path.GetFullPath(Folder + file + ".bytes")));
                var buf = new ByteBuf(256);
                buf.WriteSize(1);
                buf.WriteInt(1);
                buf.WriteFloat(DistinctRow[0]);
                buf.WriteInt(DistinctLanes);
                for (int i = 1; i < DistinctRow.Length; i++) buf.WriteFloat(DistinctRow[i]);
                return buf;
            });
            var md = new LOP.MasterData.LOPMasterData();
            typeof(LOP.MasterData.LOPMasterData).GetProperty(nameof(LOP.MasterData.LOPMasterData.Tables))
                .SetValue(md, tables);
            return md;
        }

        [Test]
        public void 설정_제공자는_표_열을_같은_이름의_칸으로_넘긴다()
        {
            var c = new CardRushConfigProvider(MasterDataWithDistinctRow()).Get();
            var got = new[]
            {
                c.MatchDurationSeconds, c.TrackLengthM, c.ObstacleGapMinM, c.ObstacleGapMaxM, c.BarricadeShare,
                c.ZoneGapMinM, c.ZoneGapMaxM, c.BarricadeWaitSeconds, c.RollKnockM, c.RollStunSeconds, c.ZoneKnockM, c.ZoneStunSeconds,
                c.BotReactMinSeconds, c.BotReactMaxSeconds, c.BotReactExtraSeconds, c.BotHesitateChance, c.BotFreeDashChance,
                c.PitShare, c.PitStunSeconds,
            };
            CollectionAssert.AreEqual(DistinctRow, got);
            Assert.AreEqual(DistinctLanes, c.MinLanes);
            Assert.AreEqual(0.19f, c.PitShare, "끝에서 둘째 열 = 구덩이 비율");
            Assert.AreEqual(0.7f, c.PitStunSeconds, "맨 끝 열 = 풍덩 기절");
        }

        [Test]
        public void 스코프_등록만으로_룰_월드_몸이_만들어진다()
        {
            var go = new GameObject("card-rush-scope-test");
            spawned.Add(go);
            var runner = go.AddComponent<LOPRunner>();
            var scope = go.AddComponent<CardRushLifetimeScope>();
            typeof(GameLifetimeScope).GetField("runner", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(scope, runner);

            var builder = new ContainerBuilder();
            //  부모(Root·Room)·공용 설치가 주는 것.
            var registry = new GameFramework.World.EntityRegistry();
            builder.RegisterInstance(RealMasterData());
            builder.RegisterInstance<IRoomDataStore>(new FakeRoomDataStore { match = new Match { playerList = new[] { "a" } } });
            builder.Register<PlayerPresence>(Lifetime.Singleton);
            builder.RegisterInstance(registry);
            builder.Register<GameFramework.World.WorldEventBuffer>(Lifetime.Singleton);
            builder.RegisterInstance<IMatchSeed>(new Seed());
            builder.RegisterInstance(new EntitySpawner(null, registry, new CardRushRunnerCreator(registry, new CardRushMapRulesField()), null, null,
                new FakePublisher<EntityCreated>(), new FakePublisher<EntityDestroyed>()));
            //  실제로는 GameplayInstaller(공용 설치)가 주는 것 — 봇 시스템(Task 7)이 받아 쓴다.
            builder.Register<InputBufferSystem>(Lifetime.Singleton);

            typeof(CardRushLifetimeScope).GetMethod("ConfigureGame", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(scope, new object[] { builder });
            var container = builder.Build();

            var rule = container.Resolve(typeof(IGameRuleSystem));
            Assert.IsInstanceOf<CardRushRuleSystem>(rule);
            Assert.IsNotNull(((CardRushRuleSystem)rule).Presence, "[Inject] PlayerPresence가 들어온다");
            Assert.IsInstanceOf<CardRushRunnerCreator>(container.Resolve(typeof(ICharacterCreator)));
            var world = container.Resolve(typeof(GameFramework.World.IWorld));
            Assert.IsInstanceOf<CardRushWorld>(world);
            Assert.AreSame(world, container.Resolve(typeof(CardRushWorld)), "IWorld와 CardRushWorld는 같은 하나");

            //  맵 마커가 주입받는 통로가 월드가 보는 것과 같은 하나여야 규칙이 월드에 닿는다.
            var markerGo = new GameObject("card-rush-rules-test");
            spawned.Add(markerGo);
            var marker = markerGo.AddComponent<CardRushMapRules>();
            container.Inject(marker);
            Assert.IsTrue(((CardRushWorld)world).Rules.HasRules, "마커가 넣은 규칙을 월드가 본다");

            //  도착 감시가 러너의 End 페이즈에 물렸다 — 빠지면 완주를 영영 못 세 판이 2분 상한까지 간다.
            var systems = (System.Collections.IDictionary)typeof(GameFramework.Runner.RunnerBase)
                .GetField("_tickSystems", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(runner);
            var end = (System.Collections.IList)systems[typeof(LOP.Event.LOPRunner.Update.End)];
            Assert.IsNotNull(end);
            Assert.Contains(container.Resolve(typeof(FinishTrackingSystem)), end);

            //  봇 시스템(Task 7)이 Begin 페이즈에 물렸다 — 빠지면 채운 봇이 영영 입력을 안 보내 판이 안 끝난다.
            var begin = (System.Collections.IList)systems[typeof(LOP.Event.LOPRunner.Update.Begin)];
            Assert.IsNotNull(begin);
            Assert.Contains(container.Resolve(typeof(CardRushBotSystem)), begin);
        }
    }
}
