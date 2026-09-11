using NUnit.Framework;

namespace LOP.Tests
{
    public class ArcheryWaveStateTests
    {
        [Test]
        public void 웨이브를_시작하면_아무것도_안_먹혔다()
        {
            var state = new ArcheryWaveState();

            state.BeginWave(0);

            Assert.AreEqual(0, state.WaveIndex);
            Assert.AreEqual(0, state.ConsumedMask);
            Assert.IsFalse(state.IsConsumed(0));
        }

        [Test]
        public void 먹으면_그_슬롯만_선다()
        {
            var state = new ArcheryWaveState();
            state.BeginWave(0);

            Assert.IsTrue(state.TryConsume(1));

            Assert.IsTrue(state.IsConsumed(1));
            Assert.IsFalse(state.IsConsumed(0));
            Assert.IsFalse(state.IsConsumed(2));
        }

        [Test]
        public void 같은_슬롯을_두_번_먹을_수_없다()
        {
            var state = new ArcheryWaveState();
            state.BeginWave(0);

            Assert.IsTrue(state.TryConsume(0));
            Assert.IsFalse(state.TryConsume(0));
        }

        [Test]
        public void 웨이브가_넘어가면_마스크가_0으로_돌아간다()
        {
            var state = new ArcheryWaveState();
            state.BeginWave(0);
            state.TryConsume(0);
            state.TryConsume(1);

            state.BeginWave(1);

            Assert.AreEqual(1, state.WaveIndex);
            Assert.AreEqual(0, state.ConsumedMask);
        }
    }
}
