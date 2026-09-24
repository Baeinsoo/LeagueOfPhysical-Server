using System.Collections.Generic;
using GameFramework.World;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class ArcheryRoundSystemTests
    {
        sealed class Seed : IMatchSeed { public ulong Value => 1UL; }
        const float TickInterval = 0.02f;
        const long Start = 1000;

        //  라운드 둘: 250틱 노출 + 200틱 간격, 둘째는 ×2. 순서 계산에 씬이 필요 없다.
        static ArcheryCourse Course()
        {
            var stands = new List<ArcheryRangeStand>
            {
                new ArcheryRangeStand(0, 10f, 250, 0f, 0f),
                new ArcheryRangeStand(0, 10f, 250, 0f, 0f, 0f, 0f, pointsMultiplier: 2),
            };
            var range = new ArcheryRangeSettings(default, stands, 200);
            var config = new ArcheryConfig(120, 3, 5, 3.5f, 0.3f, 0.6f, 1.2f, 0f, 1f,
                                           1.2f, 2.5f, 0f, 1.2f, 2.4f, 12, 20,
                                           new List<ArcheryTargetKind>(), ArcheryCourseKind.ShootOff, 0, range);
            return new ArcheryCourse(config, new Seed(), new string[0], TickInterval, () => null);
        }

        sealed class Fixture
        {
            public EntityRegistry Registry = new EntityRegistry();
            public WorldEventBuffer Events = new WorldEventBuffer();
            public ArcheryRoundLog Log = new ArcheryRoundLog();
            public ArcheryCourse Course = Course();
            public ArcheryRoundSystem System;
            public long StartTick = Start;

            public Fixture(params string[] archers)
            {
                foreach (var id in archers)
                {
                    var e = new Entity(id);
                    e.Add(new ArcheryScore());
                    Registry.Add(e);
                }
                System = new ArcheryRoundSystem(() => StartTick, Registry, Events, Course, Log);
            }

            public int Score(string id) => Registry.Get(id).Get<ArcheryScore>().Value;

            public List<ArcheryRoundResultEvent> Results()
            {
                var list = new List<ArcheryRoundResultEvent>();
                foreach (var e in Events.Snapshot)
                {
                    if (e is ArcheryRoundResultEvent r) { list.Add(r); }
                }
                return list;
            }
        }

        [Test]
        public void 마감_틱_전에는_아무_일도_없다()
        {
            var f = new Fixture("a", "b");
            f.Log.Record(0, "a", Vector2.zero, 0.1f);
            f.System.Tick(Start + 249, TickInterval);
            Assert.AreEqual(0, f.Results().Count);
            Assert.AreEqual(0, f.Score("a"));
        }

        [Test]
        public void 마감_틱에_순위_점수를_주고_사건을_낸다()
        {
            var f = new Fixture("a", "b", "c");
            f.Log.Record(0, "a", new Vector2(0.2f, 0f), 0.2f);
            f.Log.Record(0, "b", new Vector2(0.05f, 0f), 0.05f);
            f.System.Tick(Start + 250, TickInterval);

            Assert.AreEqual(1, f.Score("a"));
            Assert.AreEqual(2, f.Score("b"));
            Assert.AreEqual(0, f.Score("c"));
            var results = f.Results();
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(0, results[0].roundIndex);
            Assert.AreEqual(3, results[0].placements.Count);
            Assert.AreEqual("b", results[0].placements[0].ShooterId);
        }

        [Test]
        public void 같은_라운드는_한_번만_닫는다()
        {
            var f = new Fixture("a", "b");
            f.Log.Record(0, "a", Vector2.zero, 0.1f);
            f.System.Tick(Start + 250, TickInterval);
            f.System.Tick(Start + 250, TickInterval);
            f.System.Tick(Start + 251, TickInterval);
            Assert.AreEqual(1, f.Score("a"));
            Assert.AreEqual(1, f.Results().Count);
        }

        [Test]
        public void 마감_틱을_건너뛰어도_밀린_라운드를_모두_닫는다()
        {
            var f = new Fixture("a", "b");
            f.Log.Record(0, "a", Vector2.zero, 0.1f);
            f.Log.Record(1, "a", Vector2.zero, 0.1f);
            //  라운드 1 마감 = 1000 + 450 + 250 = 1700. 그 뒤 틱 하나로 둘 다 닫혀야 한다.
            f.System.Tick(Start + 800, TickInterval);
            Assert.AreEqual(1 + 2, f.Score("a"));
            Assert.AreEqual(2, f.Results().Count);
            Assert.AreEqual(2, f.Results()[1].multiplier);
        }

        [Test]
        public void 판_도중_나간_사람은_순위에서_빠진다()
        {
            var f = new Fixture("a", "b", "c");
            f.Log.Record(0, "a", Vector2.zero, 0.1f);
            f.Registry.Remove("c");
            f.System.Tick(Start + 250, TickInterval);
            //  인원 2명 → 1등 1점.
            Assert.AreEqual(1, f.Score("a"));
            Assert.AreEqual(2, f.Results()[0].placements.Count);
        }

        [Test]
        public void 출발_전이면_아무_일도_없다()
        {
            var f = new Fixture("a");
            f.StartTick = long.MaxValue;
            f.System.Tick(5000, TickInterval);
            Assert.AreEqual(0, f.Results().Count);
        }
    }
}
