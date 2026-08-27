using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LOP.EditorTools
{
    /// <summary>
    /// 판치기 판정·전이·타격 커널을 표로 찍는다. 눈으로 틀린 걸 알기 어려운 값들이라 직접 대조한다.
    /// EditMode 테스트를 안 만들기로 했으므로(asmdef 없이 Assembly-CSharp-Editor에서 바로 부른다) 이 슬라이스의
    /// 유일한 자동 검증이다. unity CLI의 menu 명령으로 헤드리스 재실행이 된다.
    /// </summary>
    public static class PanchigiVerification
    {
        [MenuItem("LOP/판치기 검증")]
        public static void Run()
        {
            var sb = new StringBuilder();
            Flip(sb);
            OutOfBoard(sb);
            Rest(sb);
            StrikeValidation(sb);
            StrikeKernel(sb);
            SampleLayout(sb);
            Debug.Log(sb.ToString());
        }

        // ── 공용 체크 헬퍼 ──────────────────────────────────────────────

        private static void CheckBool(StringBuilder sb, string label, bool actual, bool expected)
        {
            bool ok = actual == expected;
            sb.AppendLine($"  {label}: 실제={actual} 기대={expected} → {(ok ? "OK" : "FAIL")}");
        }

        private static void CheckFloat(StringBuilder sb, string label, float actual, float expected, float eps = 1e-4f)
        {
            bool ok = MathF.Abs(actual - expected) <= eps;
            sb.AppendLine($"  {label}: 실제={actual:F4} 기대={expected:F4} → {(ok ? "OK" : "FAIL")}");
        }

        private static void CheckLess(StringBuilder sb, string label, float smaller, float larger)
        {
            bool ok = smaller < larger;
            sb.AppendLine($"  {label}: {smaller:F4} < {larger:F4} → {(ok ? "OK" : "FAIL")}");
        }

        private static void CheckVector3(StringBuilder sb, string label,
            System.Numerics.Vector3 actual, System.Numerics.Vector3 expected, float eps = 1e-4f)
        {
            bool ok = MathF.Abs(actual.X - expected.X) <= eps
                && MathF.Abs(actual.Y - expected.Y) <= eps
                && MathF.Abs(actual.Z - expected.Z) <= eps;
            sb.AppendLine($"  {label}: 실제=({actual.X:F4},{actual.Y:F4},{actual.Z:F4}) " +
                $"기대=({expected.X:F4},{expected.Y:F4},{expected.Z:F4}) → {(ok ? "OK" : "FAIL")}");
        }

        private static void CheckThrows(StringBuilder sb, string label, Action action)
        {
            bool threw;
            try
            {
                action();
                threw = false;
            }
            catch (ArgumentOutOfRangeException)
            {
                threw = true;
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  {label}: 예상과 다른 예외 {ex.GetType().Name} → FAIL");
                return;
            }
            sb.AppendLine($"  {label}: 예외 발생={threw} 기대=True → {(threw ? "OK" : "FAIL")}");
        }

        // ── PanchigiCoin ────────────────────────────────────────────────

        private static void Flip(StringBuilder sb)
        {
            sb.AppendLine("[면] 기울기 → 뒤집힘");
            var cases = new (float deg, bool expected)[]
            {
                (0f, false), (45f, false), (89f, false), (90f, false),
                (91f, true), (135f, true), (180f, true),
            };
            foreach (var (deg, expected) in cases)
            {
                Quaternion q = Quaternion.Euler(deg, 0f, 0f);
                bool flipped = PanchigiCoin.IsFlipped(
                    new System.Numerics.Quaternion(q.x, q.y, q.z, q.w));
                CheckBool(sb, $"{deg,5:F0}도", flipped, expected);
            }
        }

        private static void OutOfBoard(StringBuilder sb)
        {
            var board = new Bounds(new Vector3(0f, -0.05f, 0f), new Vector3(10f, 0.1f, 10f));
            sb.AppendLine("[장외] 위치 → 판 밖");
            var cases = new (Vector3 p, bool expected)[]
            {
                (new Vector3(0f, 0.02f, 0f), false),
                (new Vector3(4.9f, 0.02f, 0f), false),
                (new Vector3(5.1f, 0.02f, 0f), true),
                (new Vector3(0f, 0.02f, -5.1f), true),
                (new Vector3(0f, -1f, 0f), true),
            };
            foreach (var (p, expected) in cases)
            {
                bool outside = PanchigiCoin.IsOutOfBoard(
                    new System.Numerics.Vector3(p.x, p.y, p.z), board);
                CheckBool(sb, $"{p}", outside, expected);
            }
        }

        private static void Rest(StringBuilder sb)
        {
            sb.AppendLine("[정지] (선속도, 각속도) → 멎음");
            var cases = new (float linear, float angular, bool expected)[]
            {
                (0f, 0f, true), (1f, 0f, false), (0f, 1f, false), (0.01f, 0.01f, true),
            };
            foreach (var (linear, angular, expected) in cases)
            {
                bool rest = PanchigiCoin.IsAtRest(
                    new System.Numerics.Vector3(linear, 0f, 0f),
                    new System.Numerics.Vector3(0f, angular, 0f), 0.05f, 0.1f);
                CheckBool(sb, $"({linear}, {angular})", rest, expected);
            }
        }

        //  PanchigiTurn 전이 검증은 공용 패키지의 EditMode 테스트(PanchigiTurnTests)로 옮겼다.
        //  진행 규칙이 패키지로 가면서 진짜 테스트를 붙일 수 있게 됐다. 여기 한 벌 더 두면
        //  시그니처가 바뀔 때 한쪽만 고쳐져 조용히 어긋난다 - 실제로 그렇게 배포가 깨졌다.

        // ── PanchigiStrikeValidation ────────────────────────────────────

        private static void StrikeValidation(StringBuilder sb)
        {
            sb.AppendLine("[타격 검증] PanchigiStrikeValidation.Validate");

            var board = new Bounds(Vector3.zero, new Vector3(10f, 0.1f, 10f));
            const float HoldMax = 1f;
            const float PowerMax = 3f;
            const int ContactMax = 4;

            var good = new PanchigiStrikeValidation.Contact(new Vector3(1f, 0f, 1f), new Vector3(1f, 0f, 0f), 0.5f);
            string reason;

            CheckBool(sb, "접촉점 1개 정상 → 통과",
                PanchigiStrikeValidation.Validate(new[] { good }, board, HoldMax, PowerMax, ContactMax, out reason), true);

            CheckBool(sb, "상한만큼(4개) → 통과",
                PanchigiStrikeValidation.Validate(new[] { good, good, good, good }, board, HoldMax, PowerMax, ContactMax, out reason), true);

            CheckBool(sb, "빈 배열 → 거절",
                PanchigiStrikeValidation.Validate(new PanchigiStrikeValidation.Contact[0], board, HoldMax, PowerMax, ContactMax, out reason), false);

            CheckBool(sb, "null → 거절",
                PanchigiStrikeValidation.Validate(null, board, HoldMax, PowerMax, ContactMax, out reason), false);

            CheckBool(sb, "상한 초과(5개) → 거절",
                PanchigiStrikeValidation.Validate(new[] { good, good, good, good, good }, board, HoldMax, PowerMax, ContactMax, out reason), false);

            //  하나만 어긋나도 전체가 막힌다 — 이게 "전부 아니면 전무"의 핵심이다
            var farOut = new PanchigiStrikeValidation.Contact(new Vector3(99f, 0f, 0f), Vector3.zero, 0f);
            CheckBool(sb, "3개 정상 + 1개 판 밖 → 전체 거절",
                PanchigiStrikeValidation.Validate(new[] { good, good, good, farOut }, board, HoldMax, PowerMax, ContactMax, out reason), false);

            var tooLong = new PanchigiStrikeValidation.Contact(new Vector3(1f, 0f, 1f), Vector3.zero, HoldMax + 1f);
            CheckBool(sb, "1개만 누른 시간 초과 → 전체 거절",
                PanchigiStrikeValidation.Validate(new[] { good, tooLong }, board, HoldMax, PowerMax, ContactMax, out reason), false);

            var tooStrong = new PanchigiStrikeValidation.Contact(new Vector3(1f, 0f, 1f), new Vector3(PowerMax + 1f, 0f, 0f), 0f);
            CheckBool(sb, "1개만 세기 초과 → 전체 거절",
                PanchigiStrikeValidation.Validate(new[] { good, tooStrong }, board, HoldMax, PowerMax, ContactMax, out reason), false);

            //  경계에서 정직한 클라가 막히지 않아야 한다
            var atEdge = new PanchigiStrikeValidation.Contact(new Vector3(5f, 0f, 5f), new Vector3(PowerMax, 0f, 0f), HoldMax);
            CheckBool(sb, "판 모서리 + 상한 정확히 → 통과",
                PanchigiStrikeValidation.Validate(new[] { atEdge }, board, HoldMax, PowerMax, ContactMax, out reason), true);
        }

        // ── PanchigiStrike — 잃어버린 EditMode 테스트 13개를 대신한다 ────
        //  대응: deleted-strike-tests.cs의 각 [Test]를 아래 라벨에 그대로 옮겼다.

        private static PanchigiStrike.StrikeTuning Tuning(float falloffRate = 1f)
            => new PanchigiStrike.StrikeTuning(forceMultiplier: 10f, horizontalForceMultiplier: 4f, falloffRate: falloffRate);

        private static PanchigiStrike.StrikeInput Strike(System.Numerics.Vector3 point,
            float dragX = 1f, float dragZ = 0f, float hold = 0.5f)
            => new PanchigiStrike.StrikeInput(point, new System.Numerics.Vector3(dragX, 0f, dragZ), hold);

        private static void StrikeKernel(StringBuilder sb)
        {
            var zero = System.Numerics.Vector3.Zero;

            sb.AppendLine("[타격 세기] PanchigiStrike.ComputeImpulse");

            // 1. 살아남은_샘플이_없으면_임펄스는_0
            {
                var impulse = PanchigiStrike.ComputeImpulse(
                    Strike(zero), Tuning(), new System.Numerics.Vector3[4], liveCount: 0, totalSamples: 4);
                CheckVector3(sb, "live=0 → 임펄스 0", impulse, zero);
            }

            // 2. 전부_살아남고_타격점이_샘플과_겹치면_감쇠가_없다 (구체 수치)
            {
                var samples = new[] { zero, zero, zero, zero };
                var impulse = PanchigiStrike.ComputeImpulse(
                    Strike(zero, dragX: 1f, hold: 0.5f), Tuning(), samples, 4, 4);
                //  덮임=1이므로 힘벡터 그대로: (dragX*horizontalMul, hold*forceMul, dragZ*horizontalMul) = (4, 5, 0)
                CheckVector3(sb, "전부 생존+겹침 → 감쇠 없음", impulse, new System.Numerics.Vector3(4f, 5f, 0f));
            }

            // 3. 타격점이_멀수록_약해진다
            {
                var samples = new[] { zero };
                float near = PanchigiStrike.ComputeImpulse(
                    Strike(new System.Numerics.Vector3(0.1f, 0f, 0f)), Tuning(), samples, 1, 1).Length();
                float far = PanchigiStrike.ComputeImpulse(
                    Strike(new System.Numerics.Vector3(3f, 0f, 0f)), Tuning(), samples, 1, 1).Length();
                CheckLess(sb, "먼 타격 < 가까운 타격", far, near);
            }

            // 4. 높이_차이는_감쇠에_영향을_주지_않는다 (평면 거리만 본다)
            {
                var flat = new[] { zero };
                var raised = new[] { new System.Numerics.Vector3(0f, 5f, 0f) };
                float a = PanchigiStrike.ComputeImpulse(Strike(zero), Tuning(), flat, 1, 1).Length();
                float b = PanchigiStrike.ComputeImpulse(Strike(zero), Tuning(), raised, 1, 1).Length();
                CheckFloat(sb, "평지 세기 == 5m 높이 세기", b, a);
            }

            // 5. 샘플_개수를_늘려도_세기가_변하지_않는다 (K 정규화)
            {
                var four = new[] { zero, zero, zero, zero };
                var eight = new System.Numerics.Vector3[8];
                float a = PanchigiStrike.ComputeImpulse(Strike(zero), Tuning(), four, 4, 4).Length();
                float b = PanchigiStrike.ComputeImpulse(Strike(zero), Tuning(), eight, 8, 8).Length();
                CheckFloat(sb, "4샘플 세기 == 8샘플 세기", b, a);
            }

            // 6. 절반만_살아남으면_세기도_절반이다
            {
                var samples = new[] { zero, zero, zero, zero };
                float full = PanchigiStrike.ComputeImpulse(Strike(zero), Tuning(), samples, 4, 4).Length();
                float half = PanchigiStrike.ComputeImpulse(Strike(zero), Tuning(), samples, 2, 4).Length();
                CheckFloat(sb, "half == full*0.5", half, full * 0.5f);
            }

            // 7. 누른_시간이_0이면_수직_성분이_없다
            {
                var samples = new[] { zero };
                var impulse = PanchigiStrike.ComputeImpulse(
                    Strike(zero, dragX: 1f, hold: 0f), Tuning(), samples, 1, 1);
                CheckFloat(sb, "hold=0 → y=0", impulse.Y, 0f);
                CheckBool(sb, "hold=0이어도 x>0", impulse.X > 0f, true);
            }

            // 8. 같은_입력이면_같은_결과다 (결정론)
            {
                var samples = new[] { new System.Numerics.Vector3(0.1f, 0f, 0.2f) };
                var a = PanchigiStrike.ComputeImpulse(Strike(zero), Tuning(), samples, 1, 1);
                var b = PanchigiStrike.ComputeImpulse(Strike(zero), Tuning(), samples, 1, 1);
                CheckVector3(sb, "같은 입력 두 번 → 같은 결과", b, a);
            }

            // 9. 샘플_개수가_0_이하면_예외
            CheckThrows(sb, "totalSamples=0 → ArgumentOutOfRangeException", () =>
                PanchigiStrike.ComputeImpulse(Strike(zero), Tuning(), new System.Numerics.Vector3[1], 1, 0));
            // 10. 손가락_수와_간격이_결과를_바꾼다 (설계 §6)
            //
            //  핵심: 힘 모델이 접촉점마다 다른 게 아니라, **같은 커널이 각자 자리에서 여러 번**
            //  들어가기 때문에 수와 간격이 결과를 바꾼다. 그래서 커널 하나만 두고는 검증할 수
            //  없고, 호출부가 하는 것과 같은 방식(접촉점마다 돌려 누적)으로 재야 한다.
            //
            //  ⚠️ 이 검증을 "f(a)+f(a) == f(a)*2" 같은 항등식으로 쓰면 안 된다 — 커널이 무엇을
            //  계산하든 항상 참이라 아무것도 안 잡힌다(그렇게 썼다가 지운 적이 있다).
            //  **서로 다른 접촉점**을 실제 대형에 넣어 **동전별 분포**를 비교해야 의미가 생긴다.
            ContactSpread(sb);
        }

        //  실제 마스터데이터 값. 여기 숫자가 TbPanchigiConfig와 어긋나면 이 검증의 의미가 흐려지므로
        //  값을 바꿀 때 같이 본다(회귀 방지가 목적이지 값 동기화 장치는 아니다).
        private static LOP.PanchigiStrike.StrikeTuning LiveTuning()
            => new LOP.PanchigiStrike.StrikeTuning(
                forceMultiplier: 8f, horizontalForceMultiplier: 2f, falloffRate: 4f);

        /// <summary>동전마다 "접촉점들이 준 임펄스 크기의 합"을 구한다 — 서버 핸들러가 하는 것과 같은 누적.</summary>
        private static float[] ImpulsePerCoin(System.Numerics.Vector3[] contacts, float[] coinX)
        {
            const int Samples = 13;
            const float Radius = 0.15f;

            var result = new float[coinX.Length];
            var buffer = new System.Numerics.Vector3[Samples];
            var tuning = LiveTuning();

            for (int c = 0; c < coinX.Length; c++)
            {
                var center = new System.Numerics.Vector3(coinX[c], 0.02f, 0f);
                LOP.PanchigiStrike.BuildSamples(center, Radius, buffer);

                var total = System.Numerics.Vector3.Zero;
                foreach (var point in contacts)
                {
                    var input = new LOP.PanchigiStrike.StrikeInput(
                        point, new System.Numerics.Vector3(1f, 0f, 0f), 0.7f);
                    total += LOP.PanchigiStrike.ComputeImpulse(input, tuning, buffer, Samples, Samples);
                }
                result[c] = total.Length();
            }
            return result;
        }

        private static float Sum(float[] a) { float s = 0; foreach (var v in a) s += v; return s; }

        private static string Row(float[] a)
        {
            var b = new StringBuilder();
            for (int i = 0; i < a.Length; i++)
            {
                if (i > 0) { b.Append(" | "); }
                b.Append(a[i].ToString("F3"));
            }
            return b.ToString();
        }

        /// <summary>최대÷합 — 힘이 한 동전에 얼마나 쏠렸나. 낮을수록 고루 퍼진 것.</summary>
        private static float Concentration(float[] a)
        {
            float max = 0, sum = 0;
            foreach (var v in a) { if (v > max) max = v; sum += v; }
            return sum > 0f ? max / sum : 0f;
        }

        private static void ContactSpread(StringBuilder sb)
        {
            sb.AppendLine("[손가락 수·간격] 접촉점마다 커널을 돌려 누적");

            //  FourInLine 대형 그대로 — 씬의 자리와 같은 x 좌표
            float[] coinX = { -1.05f, -0.35f, 0.35f, 1.05f };

            var single = new[] { new System.Numerics.Vector3(0f, 0.02f, 0f) };
            //  세 손가락을 붙여 짚은 손 (간격 2cm)
            var together = new[] {
                new System.Numerics.Vector3(-0.02f, 0.02f, 0f),
                new System.Numerics.Vector3( 0.00f, 0.02f, 0f),
                new System.Numerics.Vector3( 0.02f, 0.02f, 0f) };
            //  같은 세 손가락을 벌려 짚은 손 (간격 70cm — 동전 간격과 같게)
            var spread = new[] {
                new System.Numerics.Vector3(-0.70f, 0.02f, 0f),
                new System.Numerics.Vector3( 0.00f, 0.02f, 0f),
                new System.Numerics.Vector3( 0.70f, 0.02f, 0f) };

            float[] one = ImpulsePerCoin(single, coinX);
            float[] near = ImpulsePerCoin(together, coinX);
            float[] wide = ImpulsePerCoin(spread, coinX);

            sb.AppendLine($"  동전별 임펄스 1개   : {Row(one)}  합={Sum(one):F3}");
            sb.AppendLine($"  동전별 임펄스 모아서: {Row(near)}  합={Sum(near):F3}");
            sb.AppendLine($"  동전별 임펄스 벌려서: {Row(wide)}  합={Sum(wide):F3}");

            //  ① 손가락 수 — 같은 자리를 3번 치면 한 번의 3배가 들어간다(선형 누적)
            CheckFloat(sb, "3개 모아서 합 == 1개 합의 3배", Sum(near), Sum(one) * 3f, eps: 1e-2f);

            //  ② 간격 — 바깥 동전은 벌려 칠 때 더 받고, 가운데 동전은 덜 받는다.
            //     이게 "벌리면 영향 범위가 넓어진다"의 실체다.
            CheckLess(sb, "바깥 동전: 모아서 < 벌려서 (왼쪽)", near[0], wide[0]);
            CheckLess(sb, "바깥 동전: 모아서 < 벌려서 (오른쪽)", near[3], wide[3]);
            CheckLess(sb, "가운데 동전: 벌려서 < 모아서 (왼쪽)", wide[1], near[1]);
            CheckLess(sb, "가운데 동전: 벌려서 < 모아서 (오른쪽)", wide[2], near[2]);

            //  ③ 분포 — 벌려 치면 힘이 한 동전에 덜 쏠린다
            CheckLess(sb, "집중도(최대/합): 벌려서 < 모아서", Concentration(wide), Concentration(near));
        }

        private static void SampleLayout(StringBuilder sb)
        {
            sb.AppendLine("[샘플 배치] PanchigiStrike.BuildSamples");

            // 10. 샘플은_전부_발자국_원_안에_깔린다 (+ 동전과 같은 높이)
            {
                var center = new System.Numerics.Vector3(2f, 0.5f, -3f);
                const float radius = 0.15f;
                var buffer = new System.Numerics.Vector3[13];
                PanchigiStrike.BuildSamples(center, radius, buffer);

                bool allInside = true;
                bool allSameHeight = true;
                foreach (var p in buffer)
                {
                    float dx = p.X - center.X;
                    float dz = p.Z - center.Z;
                    if (MathF.Sqrt(dx * dx + dz * dz) > radius + 1e-4f) { allInside = false; }
                    if (MathF.Abs(p.Y - center.Y) > 1e-4f) { allSameHeight = false; }
                }
                CheckBool(sb, "13개 전부 반지름 안", allInside, true);
                CheckBool(sb, "13개 전부 동전과 같은 높이", allSameHeight, true);
            }

            // 11. 샘플_배치는_결정론적이다
            {
                var a = new System.Numerics.Vector3[13];
                var b = new System.Numerics.Vector3[13];
                PanchigiStrike.BuildSamples(System.Numerics.Vector3.Zero, 0.15f, a);
                PanchigiStrike.BuildSamples(System.Numerics.Vector3.Zero, 0.15f, b);

                bool allEqual = true;
                for (int i = 0; i < a.Length; i++)
                {
                    if (a[i] != b[i]) { allEqual = false; break; }
                }
                CheckBool(sb, "두 번 배치 → 완전히 같음", allEqual, true);
            }

            // 12. 샘플이_하나여도_동작한다
            {
                var buffer = new System.Numerics.Vector3[1];
                PanchigiStrike.BuildSamples(System.Numerics.Vector3.Zero, 0.15f, buffer);
                CheckBool(sb, "샘플 1개 → 반지름 안", buffer[0].Length() <= 0.15f + 1e-4f, true);
            }

            // 13. 샘플_버퍼가_비어_있으면_예외
            CheckThrows(sb, "빈 버퍼 → ArgumentOutOfRangeException", () =>
                PanchigiStrike.BuildSamples(System.Numerics.Vector3.Zero, 0.15f, new System.Numerics.Vector3[0]));
        }
    }
}
