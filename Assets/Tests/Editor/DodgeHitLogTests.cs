using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class DodgeHitLogTests
    {
        [Test]
        public void 스테이지와_경기_시작_뒤_초를_적는다()
        {
            var at = new DodgeStagePoint(2, false, 0, 100, 0.5f, 1f, DodgeStageTable.AllKinds, 50);
            Assert.AreEqual("[Dodge] hit stage=3 t=12.5 lives=2", DodgeHitLog.Format(at, 1625, 1000, 2));
        }

        [Test]
        public void 서든데스는_SD()
        {
            var at = new DodgeStagePoint(5, true, 0, long.MaxValue, 1f, 2f, DodgeStageTable.AllKinds, 30);
            StringAssert.StartsWith("[Dodge] hit stage=SD", DodgeHitLog.Format(at, 200, 0, 1));
        }

        [Test]
        public void 맞으면_콜백이_불린다()
        {
            var c = DodgeSimTables.Config();
            var registry = new GameFramework.World.EntityRegistry();
            var state = new DodgeMatchState();
            var e = new GameFramework.World.Entity("1");
            e.Add(new GameFramework.World.Transform());
            registry.Add(e);
            state.Players["1"] = new DodgePlayerLife { Lives = c.Lives };
            (string id, long tick, int lives) got = default;
            var sys = new DodgeHazardSystem(state, registry, c, _ => { }, (id, t, l) => got = (id, t, l));
            state.Patterns.Add(new DodgePattern(1, DodgePatternKind.Bomb, 11 - c.WarnTicks, 0, 0f, 0f, 2f, 0f));
            sys.Tick(10, 0.02f);
            sys.Tick(11, 0.02f);
            Assert.AreEqual(("1", 11L, c.Lives - 1), got);
        }
    }
}
