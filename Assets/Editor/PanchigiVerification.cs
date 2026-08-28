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

        //  PanchigiStrike 커널 검증은 Assets/Tests/Editor의 EditMode 테스트로 옮겼다.
        //  여기 한 벌 더 두면 시그니처가 바뀔 때 한쪽만 고쳐져 조용히 어긋난다 - 실제로 그렇게
        //  게임서버 배포가 깨진 적이 있다.
    }
}
