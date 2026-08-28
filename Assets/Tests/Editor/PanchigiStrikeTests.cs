using System;
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
        public void 영향_반경이_0이거나_음수면_거절한다()
        {
            //  0이면 0/0=NaN이 Exp를 거쳐 그대로 물리엔진까지 흘러간다(Vector3.Zero 가드로는 못
            //  거른다). 음수면 exp(+거리/반경)이 되어 먼 동전이 더 세게 맞는다. 둘 다 설정 실수이니
            //  조용히 굴러가지 말고 여기서 바로 터뜨린다.
            var input = new PanchigiStrike.StrikeInput(Vector3.Zero, Vector3.Zero, 1f);
            var samples = new[] { Vector3.Zero };

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                PanchigiStrike.ComputeImpulse(input, Tuning(0f), samples, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                PanchigiStrike.ComputeImpulse(input, Tuning(-0.4f), samples, 1, 1));
        }

        [Test]
        public void 드래그의_X와_Z가_각자_임펄스_X와_Z로_간다()
        {
            //  DragDelta.X → 임펄스.X, DragDelta.Z → 임펄스.Z가 서로 안 섞인다는 것을 고정한다.
            //  X·Z를 뒤바꾸거나 수평 항을 통째로 지우면 이 값이 어긋나 깨진다 — 지운 수기 검증의
            //  "전부 생존+겹침 → (4, 5, 0)" 체크가 하던 역할.
            var tuning = new PanchigiStrike.StrikeTuning(0f, 2f, 0.4f);   // 세로는 꺼서 수평만 본다
            var input = new PanchigiStrike.StrikeInput(Vector3.Zero, new Vector3(3f, 0f, 1f), 0f);
            var samples = new[] { Vector3.Zero };   // 타격점과 겹쳐 coverage = 1

            var impulse = PanchigiStrike.ComputeImpulse(input, tuning, samples, 1, 1);

            Assert.AreEqual(3f * 2f, impulse.X, 1e-5f);
            Assert.AreEqual(1f * 2f, impulse.Z, 1e-5f);
        }

        [Test]
        public void 살아남은_샘플이_절반이면_세기도_절반이다()
        {
            //  liveCount만 절반으로 주고 totalSamples는 고정한다 — "판에 반만 닿았다"가 실제로
            //  반값을 낸다는 것을 고정한다. 지금까지는 liveCount가 0이거나 totalSamples와 같을 때만
            //  검증돼 그 사이(부분 덮임, 실제로 가장 흔한 상태)가 안 지나갔다.
            var input = new PanchigiStrike.StrikeInput(Vector3.Zero, Vector3.Zero, 1f);
            var samples = new[] { Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero };

            float full = PanchigiStrike.ComputeImpulse(input, Tuning(0.4f), samples, 4, 4).Y;
            float half = PanchigiStrike.ComputeImpulse(input, Tuning(0.4f), samples, 2, 4).Y;

            Assert.AreEqual(full * 0.5f, half, 1e-5f);
        }

        [Test]
        public void 한_점보다_넓게_벌려야_가장_약한_동전이_더_세게_맞는다()
        {
            //  이 브랜치의 존재 이유 — "한 점으로는 안 되고, 손바닥을 벌려야 판 전체가 움직인다."
            //  동전 4개를 실제 간격 0.5m로 z축에 나란히 놓는다(z = -0.75, -0.25, 0.25, 0.75).
            //  "다 뒤집기"의 병목은 가장 약하게 맞는 동전이라, 그 최솟값을 두 타격 방식으로 비교한다.
            //  접촉점마다 ComputeImpulse를 부르고 결과를 더하는 것이 호출부(메시지 핸들러)가 하는
            //  전부이므로 커널만으로 재현된다.
            float[] coinZ = { -0.75f, -0.25f, 0.25f, 0.75f };
            var tuning = Tuning(0.4f);

            float singlePoint = WeakestCoinImpulse(coinZ, new[] { 0f }, tuning);
            float spreadFour = WeakestCoinImpulse(coinZ, coinZ, tuning);

            //  실측(2026-08-29): 이 곡선에서 비율은 약 9.1배, 옛 곡선(1/(1+4d²))으로 되돌리면
            //  5.85배로 떨어진다. 문턱을 둘 사이(7배)에 두면 되돌렸을 때 반드시 깨진다 — 확인 후
            //  원복함(final-fix-report.md 참고).
            Assert.Greater(spreadFour, singlePoint * 7f,
                "손바닥을 벌렸을 때 가장 약한 동전이 한 점만 쳤을 때보다 확실히 커야 한다");
        }

        //  동전을 샘플 하나로 단순화한다(이 파일의 다른 테스트와 같은 패턴) — 여러 접촉점이 한
        //  동전에 누적되는 방식만 재현하면 되고, 동전 안 샘플 밀도(BuildSamples)는 별도로 검증된다.
        private static float WeakestCoinImpulse(float[] coinZ, float[] strikeZ, PanchigiStrike.StrikeTuning tuning)
        {
            float weakest = float.MaxValue;
            foreach (float cz in coinZ)
            {
                var sample = new[] { new Vector3(0f, 0f, cz) };
                float total = 0f;
                foreach (float sz in strikeZ)
                {
                    var input = new PanchigiStrike.StrikeInput(new Vector3(0f, 0f, sz), Vector3.Zero, 1f);
                    total += PanchigiStrike.ComputeImpulse(input, tuning, sample, 1, 1).Y;
                }
                weakest = Math.Min(weakest, total);
            }
            return weakest;
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
