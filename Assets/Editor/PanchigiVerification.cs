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
            Turn(sb);
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

        // ── PanchigiTurn ────────────────────────────────────────────────

        private static void Turn(StringBuilder sb)
        {
            sb.AppendLine("[전이]");
            var players = new[] { "P1", "P2" };

            var t = new PanchigiTurn(players, 60);
            t.OnRested(false);
            sb.AppendLine($"  첫 정지 → {t.Phase} / {t.CurrentEntityId}");
            CheckBool(sb, "    Aiming인가", t.Phase == PanchigiPhase.Aiming, true);
            CheckBool(sb, "    P1 차례인가", t.CurrentEntityId == "P1", true);

            t.OnStruck("P1");
            sb.AppendLine($"  타격 → {t.Phase} / 턴 {t.TurnCount}");
            CheckBool(sb, "    Settling인가", t.Phase == PanchigiPhase.Settling, true);
            CheckBool(sb, "    턴 1인가", t.TurnCount == 1, true);

            t.OnRested(false);
            sb.AppendLine($"  정지 → {t.Phase} / {t.CurrentEntityId}");
            CheckBool(sb, "    Aiming인가", t.Phase == PanchigiPhase.Aiming, true);
            CheckBool(sb, "    P2 차례인가", t.CurrentEntityId == "P2", true);

            t.OnAimTimeout();
            sb.AppendLine($"  패스 → {t.Phase} / {t.CurrentEntityId} / 턴 {t.TurnCount}");
            CheckBool(sb, "    Aiming인가", t.Phase == PanchigiPhase.Aiming, true);
            CheckBool(sb, "    P1 차례인가", t.CurrentEntityId == "P1", true);
            CheckBool(sb, "    턴 2인가", t.TurnCount == 2, true);

            t.OnStruck("P1");
            t.OnRested(true);
            sb.AppendLine($"  다 뒤집힘 → {t.Phase} / 승자 {t.WinnerEntityId}");
            CheckBool(sb, "    Over인가", t.Phase == PanchigiPhase.Over, true);
            CheckBool(sb, "    승자 P1인가", t.WinnerEntityId == "P1", true);

            var limited = new PanchigiTurn(players, 1);
            limited.OnRested(false);
            limited.OnAimTimeout();
            limited.OnAimTimeout();  // 이미 Over라 무시돼야 한다
            sb.AppendLine($"  상한 도달(turnLimit=1, 경로 B=OnAimTimeout) → {limited.Phase} / 승자 {limited.WinnerEntityId ?? "없음"}");
            CheckBool(sb, "    Over인가", limited.Phase == PanchigiPhase.Over, true);
            CheckBool(sb, "    무승부(승자 없음)인가", limited.WinnerEntityId == null, true);

            //  경로 A — "쳐서 동전이 멎었는데 그 시점에 상한 도달"은 OnRested 안의 상한 체크가 처리한다.
            //  위 limited 시나리오는 OnAimTimeout(경로 B)만 지나가므로, OnRested(경로 A)를 따로 지나가 본다.
            var pathA = new PanchigiTurn(players, 1);
            pathA.OnRested(false);
            sb.AppendLine($"  경로A① 첫 정지 → {pathA.Phase} / {pathA.CurrentEntityId} / 턴 {pathA.TurnCount}");
            CheckBool(sb, "    Aiming인가", pathA.Phase == PanchigiPhase.Aiming, true);
            CheckBool(sb, "    P1 차례인가", pathA.CurrentEntityId == "P1", true);
            CheckBool(sb, "    턴 0인가", pathA.TurnCount == 0, true);

            pathA.OnStruck("P1");
            sb.AppendLine($"  경로A② 타격 → {pathA.Phase} / 턴 {pathA.TurnCount}");
            CheckBool(sb, "    Settling인가", pathA.Phase == PanchigiPhase.Settling, true);
            CheckBool(sb, "    턴 1인가", pathA.TurnCount == 1, true);

            pathA.OnRested(false);  // TurnCount(1) >= turnLimit(1) — OnRested 안에서 바로 Over (경로 A)
            sb.AppendLine($"  경로A③ 정지(상한 도달, 경로 A=OnRested) → {pathA.Phase} / 승자 {pathA.WinnerEntityId ?? "없음"}");
            CheckBool(sb, "    Over인가(OnRested 경로)", pathA.Phase == PanchigiPhase.Over, true);
            CheckBool(sb, "    무승부(P1이 쳤지만 안 뒤집혀 승자 아님)인가", pathA.WinnerEntityId == null, true);
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
