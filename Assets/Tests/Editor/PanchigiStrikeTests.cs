using System.Numerics;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 타격 힘 커널. 거리에 따라 얼마나 약해지는가가 이 게임의 손맛을 정한다 —
    /// 곡선을 건드릴 때 무엇이 달라졌는지 여기서 드러나야 한다.
    ///
    /// asmdef 없이 Assets/Tests/Editor에 둔다. 그러면 predefined Assembly-CSharp-Editor에
    /// 들어가고, 그건 Assembly-CSharp을 참조하므로 서버 런타임 클래스를 그대로 시험할 수 있다.
    /// </summary>
    public class PanchigiStrikeTests
    {
        //  세기 노브는 1로 두고 거리 효과만 본다. 수직 세기가 1이면 홀드 1초가 곧 임펄스 크기다.
        private static PanchigiStrike.StrikeTuning Tuning(float influenceRadius)
            => new PanchigiStrike.StrikeTuning(1f, 1f, influenceRadius);

        //  타격점은 원점, 동전은 x축으로 distance만큼 떨어진 곳에 샘플 하나.
        private static float ImpulseAt(float distance, float influenceRadius, int totalSamples = 1)
        {
            var input = new PanchigiStrike.StrikeInput(Vector3.Zero, Vector3.Zero, 1f);
            var samples = new[] { new Vector3(distance, 0f, 0f) };

            return PanchigiStrike.ComputeImpulse(input, Tuning(influenceRadius), samples, 1, totalSamples).Y;
        }

        [Test]
        public void 멀어질수록_약해진다()
        {
            Assert.Greater(ImpulseAt(0f, 0.4f), ImpulseAt(0.5f, 0.4f));
            Assert.Greater(ImpulseAt(0.5f, 0.4f), ImpulseAt(1.5f, 0.4f));
        }

        [Test]
        public void 살아남은_샘플이_없으면_힘이_없다()
        {
            var input = new PanchigiStrike.StrikeInput(Vector3.Zero, Vector3.One, 1f);
            var samples = new[] { Vector3.Zero };

            var impulse = PanchigiStrike.ComputeImpulse(input, Tuning(0.4f), samples, 0, 13);

            Assert.AreEqual(Vector3.Zero, impulse);
        }

        [Test]
        public void 샘플_개수를_늘려도_세기는_그대로다()
        {
            //  정밀도 노브(샘플 수)와 세기 노브가 갈려 있어야 한다 — 전체 샘플 수로 나누는 이유.
            var input = new PanchigiStrike.StrikeInput(Vector3.Zero, Vector3.Zero, 1f);

            var few = new[] { Vector3.Zero, Vector3.Zero };
            var many = new[] { Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero };

            float withFew = PanchigiStrike.ComputeImpulse(input, Tuning(0.4f), few, 2, 2).Y;
            float withMany = PanchigiStrike.ComputeImpulse(input, Tuning(0.4f), many, 4, 4).Y;

            Assert.AreEqual(withFew, withMany, 1e-5f);
        }

        [Test]
        public void 높이가_달라도_세기가_흔들리지_않는다()
        {
            //  감쇠는 판 위 평면 거리(XZ)로만 잰다 — 동전이 떠 있어도 같은 세기여야 한다.
            var input = new PanchigiStrike.StrikeInput(Vector3.Zero, Vector3.Zero, 1f);
            var onBoard = new[] { new Vector3(0.5f, 0f, 0f) };
            var lifted = new[] { new Vector3(0.5f, 3f, 0f) };

            float a = PanchigiStrike.ComputeImpulse(input, Tuning(0.4f), onBoard, 1, 1).Y;
            float b = PanchigiStrike.ComputeImpulse(input, Tuning(0.4f), lifted, 1, 1).Y;

            Assert.AreEqual(a, b, 1e-5f);
        }

        [Test]
        public void 영향_반경만큼_떨어지면_약_37퍼센트다()
        {
            //  e^-1 = 0.368. "반경"이라는 이름이 무엇을 뜻하는지를 이 테스트가 정의한다.
            float atCenter = ImpulseAt(0f, 0.4f);
            float atRadius = ImpulseAt(0.4f, 0.4f);

            Assert.AreEqual(0.368f, atRadius / atCenter, 0.01f);
        }

        [Test]
        public void 반경_네_배_거리에서는_거의_사라진다()
        {
            //  꼬리를 끊는 것이 이 곡선을 고른 이유다. 옛 곡선(1/(1+4d²))은 여기서 9%가 남았다.
            float atCenter = ImpulseAt(0f, 0.4f);
            float farAway = ImpulseAt(1.6f, 0.4f);

            Assert.Less(farAway / atCenter, 0.02f);
        }

        [Test]
        public void 옆_동전은_삼분의_일도_못_받는다()
        {
            //  동전 간격 0.5m. 한 점만 쳐서는 옆까지 확실히 못 넘기고, 손바닥을 벌려야 한다.
            float atCenter = ImpulseAt(0f, 0.4f);
            float neighbour = ImpulseAt(0.5f, 0.4f);

            Assert.Less(neighbour / atCenter, 0.33f);
        }

        [Test]
        public void 샘플을_원판에_고르게_깐다()
        {
            //  해바라기 배치 — 개수가 몇이든 성립하고 난수를 안 써서 늘 같은 자리가 나온다.
            var buffer = new Vector3[13];

            PanchigiStrike.BuildSamples(new Vector3(1f, 2f, 3f), 0.15f, buffer);

            foreach (var sample in buffer)
            {
                float dx = sample.X - 1f;
                float dz = sample.Z - 3f;
                Assert.LessOrEqual(System.MathF.Sqrt(dx * dx + dz * dz), 0.15f + 1e-4f);
                Assert.AreEqual(2f, sample.Y, 1e-5f, "샘플은 동전과 같은 높이에 깔린다");
            }
        }
    }
}
