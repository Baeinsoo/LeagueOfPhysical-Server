using System.Collections.Generic;
using GameFramework.World;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>판 도중 나가 있는 사람은 판이 기다리지 않는다 — 완주 판정·판치기 차례에서 뺀다. 피하기는 몸이 금방 탈락하니 기다린다(10-07 결정).</summary>
    public class LeaverStallTests
    {
        static Entity Body(EntityRegistry registry, string id, bool finished)
        {
            var e = new Entity(id);
            e.Add(new FinishState { FinishedTick = finished ? 100 : FinishState.NotFinished });
            registry.Add(e);
            return e;
        }

        [Test]
        public void 완주_판정은_나가_있는_사람을_기다리지_않는다()
        {
            var registry = new EntityRegistry();
            Body(registry, "stay", finished: true);
            Body(registry, "gone", finished: false);
            var finish = new FinishTrackingSystem(registry);
            finish.Watch("stay");
            finish.Watch("gone");
            finish.Tick(101, 0.02f);

            Assert.IsFalse(finish.AllWatchedFinished, "나간 사람을 모르면 기다린다");

            finish.IsAwayEntity = id => id == "gone";
            Assert.IsTrue(finish.AllWatchedFinished, "나간 사람은 빼고 본다");
        }

        [Test]
        public void 전원_나가_있으면_완주로_치지_않는다()
        {
            var registry = new EntityRegistry();
            Body(registry, "a", finished: false);
            var finish = new FinishTrackingSystem(registry) { IsAwayEntity = _ => true };
            finish.Watch("a");
            Assert.IsFalse(finish.AllWatchedFinished, "남은 사람이 없으면 시간 상한으로 끝난다(즉시 끝내지 않는다)");
        }

        [Test]
        public void 판치기는_지금_차례가_나가_있으면_바로_넘긴다()
        {
            Assert.IsTrue(PanchigiTurnSystem.ShouldForfeitNow(PanchigiPhase.Aiming, "u1", u => u == "u1"));
            Assert.IsFalse(PanchigiTurnSystem.ShouldForfeitNow(PanchigiPhase.Aiming, "u1", _ => false));
            Assert.IsFalse(PanchigiTurnSystem.ShouldForfeitNow(PanchigiPhase.Settling, "u1", _ => true), "구르는 중엔 기다린다");
            Assert.IsFalse(PanchigiTurnSystem.ShouldForfeitNow(PanchigiPhase.Aiming, null, _ => true));
        }
    }
}
