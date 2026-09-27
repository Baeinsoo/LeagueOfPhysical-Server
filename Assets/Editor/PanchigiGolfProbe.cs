using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GameFramework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LOP.EditorTools
{
    /// <summary>
    /// 골프식 판치기(같은 배치에서 적은 타수로 전부 뒤집기)가 게임이 되는지 잰다.
    /// 판 상태를 이어 가며 다 뒤집힐 때까지 친다. 한 번 치면 <b>이미 뒤집은 동전도 도로 엎어질 수 있어</b>
    /// 아무렇게나 치면 반반 근처를 맴돌 수 있다 — 그래서 치는 방식별 타수 차이가 곧 실력 폭이다.
    ///
    /// - 아무 데나: 판 위 무작위 자리·세기
    /// - 요령: 안 뒤집힌 동전 하나를 골라 그 위를 친다(손가락 오차 포함) — 사람이 할 법한 것
    /// - 최선: 매 타 후보를 전부 굴려 보고 가장 좋은 곳에 손가락 오차를 넣어 친다 — 실력의 상한
    ///
    /// 낙은 게임 설계대로 OB — 1벌타 + 치기 직전 상태로 되돌린다.
    /// 타격 계산은 <see cref="PanchigiTeamSelectivityProbe"/>와 같다(게임과 같은 커널, 친 자리에 임펄스).
    /// </summary>
    public static class PanchigiGolfProbe
    {
        private const float ForceMultiplier = 10f;
        private const float HorizontalForceMultiplier = 2.5f;
        private const float InfluenceRadius = 0.4f;
        private const int CoverageSamples = 13;
        private const float RestSpeed = 0.05f;
        private const float RestAngular = 0.1f;
        private const int RestTicks = 10;

        private const float CoinRadius = 0.15f;
        private const float CoinThickness = 0.04f;
        private const float Dt = 0.02f;
        private const int MaxSteps = 600;

        private static readonly Vector3 BoardSize = new(2.8f, 0.25f, 4f);
        private const float BoardTopY = 0.01f;

        public enum Policy { Random, Heuristic, Best }

        /// <summary>
        /// 낙 처리. 거짓 = 치기 직전 상태로 되돌린다. 참 = 떨어진 동전만 시작 자리(앞면)로 되돌리고
        /// 나머지는 친 결과를 그대로 둔다(골프의 드롭). 직전 복원은 가장자리에 밀린 동전을 다시 가장자리에
        /// 두므로 같은 낙이 끝없이 되풀이된다(2026-09-27 실측).
        /// </summary>
        public static bool DropToSlot;

        /// <summary>참이면 원래 판치기 룰대로 — 낙이 나면 판 전체를 처음 배치로(1벌타). DropToSlot보다 우선한다.</summary>
        public static bool ResetAllOnDrop;

        private struct Pose
        {
            public Vector3 Position;
            public Quaternion Rotation;
        }

        private class Table
        {
            public PhysicsScene Physics;
            public Collider Board;
            public List<Rigidbody> Coins = new();
            public List<GameObject> Roots = new();
        }

        /// <summary>
        /// SixInLine(2·3인 배치) — 판 가운데 세로 일렬, 0.5m 간격. 맵 씬 PanchigiMap의 자리와 같다.
        /// </summary>
        private static Vector3[] SixInLine()
        {
            var slots = new Vector3[6];
            for (int i = 0; i < 6; i++)
            {
                slots[i] = new Vector3(0f, BoardTopY + CoinThickness * 0.5f + 0.002f, -1.25f + 0.5f * i);
            }
            return slots;
        }

        public static string Run(string outPath, Policy policy, int games, int strokeCap, int seed,
            float jitterRadius = 0.05f, float bestGridStep = 0.3f)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"== 골프식 SixInLine — 낙 처리 {(ResetAllOnDrop ? "전부 리셋" : DropToSlot ? "떨어진 것만 시작 자리로" : "직전 상태로")}, 방식 {policy}, {games}판, 상한 {strokeCap}타, 손가락 오차 {jitterRadius * 100f:F0}cm");

            SimulationMode previousMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            Scene scene = EditorSceneManager.NewPreviewScene();
            var rng = new System.Random(seed);
            var strokes = new List<int>();
            int finished = 0, totalDrops = 0, totalUnflips = 0;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                Vector3[] slots = SixInLine();
                for (int g = 0; g < games; g++)
                {
                    Table table = Build(scene, slots);
                    try
                    {
                        int count = 0, drops = 0, unflips = 0;
                        var trace = new StringBuilder();
                        while (count < strokeCap && FlippedCount(table) < slots.Length)
                        {
                            Pose[] before = Capture(table);
                            int flippedBefore = FlippedCount(table);
                            (Vector3 point, float hold) = Choose(policy, table, rng, jitterRadius, bestGridStep);
                            bool dropped = StrikeAndSettle(table, point, hold);
                            count++;
                            if (dropped)
                            {
                                if (ResetAllOnDrop) { ResetAll(table, slots); } else if (DropToSlot) { ReturnDropped(table, slots); } else { Restore(table, before); }
                                count++;   // 1벌타
                                drops++;
                                trace.Append('X');
                                continue;
                            }
                            int now = FlippedCount(table);
                            if (now < flippedBefore) { unflips++; }
                            trace.Append(now);
                        }
                        bool done = FlippedCount(table) == slots.Length;
                        if (done) { finished++; }
                        strokes.Add(done ? count : strokeCap + 1);
                        totalDrops += drops;
                        totalUnflips += unflips;
                        sb.AppendLine($"  판 {g}: {(done ? $"{count}타" : "상한")} 낙 {drops} 되엎힘 {unflips}  [{trace}]");
                    }
                    finally
                    {
                        foreach (GameObject go in table.Roots) { Object.DestroyImmediate(go); }
                    }
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                Physics.simulationMode = previousMode;
            }

            strokes.Sort();
            float mean = (float)strokes.Average();
            int median = strokes[strokes.Count / 2];
            int p90 = strokes[Mathf.Min(strokes.Count - 1, (int)(strokes.Count * 0.9f))];
            sb.AppendLine($"-- {policy}: 완주 {finished}/{games} / 평균 {mean:F1}타(미완주=상한+1) / 중앙 {median} / 90% {p90} / 최소 {strokes[0]} / 판당 낙 {totalDrops / (float)games:F1} / 판당 되엎힘 타격 {totalUnflips / (float)games:F1} / {sw.Elapsed.TotalSeconds:F0}s");

            string text = sb.ToString();
            File.AppendAllText(outPath, text + "\n");
            return text;
        }

        private static (Vector3 point, float hold) Choose(Policy policy, Table table, System.Random rng,
            float jitterRadius, float bestGridStep)
        {
            switch (policy)
            {
                case Policy.Random:
                    return (new Vector3((float)(rng.NextDouble() - 0.5) * BoardSize.x, BoardTopY,
                                        (float)(rng.NextDouble() - 0.5) * BoardSize.z),
                            0.3f + (float)rng.NextDouble() * 0.7f);

                case Policy.Heuristic:
                {
                    var targets = table.Coins.Where(c => IsFlipped(c) == false && OnBoard(c, table)).ToList();
                    Rigidbody target = targets.Count > 0 ? targets[rng.Next(targets.Count)] : table.Coins[0];
                    return Jitter(new Vector3(target.position.x, BoardTopY, target.position.z), 0.7f, rng, jitterRadius);
                }

                default:
                {
                    Pose[] before = Capture(table);
                    float bestScore = float.MinValue;
                    Vector3 bestPoint = Vector3.zero;
                    float bestHold = 0.7f;
                    float[] holds = { 0.4f, 0.7f, 1.0f };
                    for (float x = -BoardSize.x * 0.5f + bestGridStep * 0.5f; x < BoardSize.x * 0.5f; x += bestGridStep)
                    {
                        for (float z = -BoardSize.z * 0.5f + bestGridStep * 0.5f; z < BoardSize.z * 0.5f; z += bestGridStep)
                        {
                            foreach (float hold in holds)
                            {
                                var p = new Vector3(x, BoardTopY, z);
                                bool dropped = StrikeAndSettle(table, p, hold);
                                float score = dropped ? -10f : FlippedCount(table);
                                Restore(table, before);
                                if (score > bestScore)
                                {
                                    bestScore = score;
                                    bestPoint = p;
                                    bestHold = hold;
                                }
                            }
                        }
                    }
                    return Jitter(bestPoint, bestHold, rng, jitterRadius);
                }
            }
        }

        private static (Vector3, float) Jitter(Vector3 point, float hold, System.Random rng, float radius)
        {
            double a = rng.NextDouble() * Mathf.PI * 2.0, r = radius * System.Math.Sqrt(rng.NextDouble());
            var p = point + new Vector3((float)(System.Math.Cos(a) * r), 0f, (float)(System.Math.Sin(a) * r));
            float h = Mathf.Clamp(hold + (float)(rng.NextDouble() - 0.5) * 0.1f, 0.05f, 1f);
            return (p, h);
        }

        private static Table Build(Scene scene, Vector3[] slots)
        {
            var table = new Table { Physics = scene.GetPhysicsScene() };

            GameObject board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "golf-board";
            board.transform.localScale = BoardSize;
            board.transform.position = new Vector3(0f, BoardTopY - BoardSize.y * 0.5f, 0f);
            SceneManager.MoveGameObjectToScene(board, scene);
            table.Roots.Add(board);
            table.Board = board.GetComponent<Collider>();

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.localScale = new Vector3(40f, 1f, 40f);
            floor.transform.position = new Vector3(0f, -3f, 0f);
            SceneManager.MoveGameObjectToScene(floor, scene);
            table.Roots.Add(floor);

            for (int i = 0; i < slots.Length; i++)
            {
                var go = new GameObject($"golf-coin-{i}");
                var entity = new GameFramework.World.Entity($"coin{i}");
                entity.Add(new GameFramework.World.Transform
                {
                    Position = slots[i].ToNumerics(),
                    Rotation = System.Numerics.Quaternion.Identity,
                });
                entity.Add(new GameFramework.World.Velocity());
                entity.Add(new GameFramework.World.DiscShape(CoinRadius, CoinThickness));
                entity.Add(new GameFramework.World.PhysicsConfig(
                    GameFramework.World.BodyKind.Dynamic, freezeRotation: false, isTrigger: false));
                PhysicsBodyFactory.Create(go, entity);
                SceneManager.MoveGameObjectToScene(go, scene);
                table.Roots.Add(go);
                table.Coins.Add(go.GetComponent<Rigidbody>());
            }

            for (int i = 0; i < 20; i++) { table.Physics.Simulate(Dt); }
            return table;
        }

        private static Pose[] Capture(Table table)
        {
            return table.Coins.Select(c => new Pose { Position = c.position, Rotation = c.rotation }).ToArray();
        }

        /// <summary>게임의 낙 복원과 같다 — 자세를 옮기고 속도를 0으로.</summary>
        private static void Restore(Table table, Pose[] poses)
        {
            for (int i = 0; i < table.Coins.Count; i++)
            {
                Rigidbody rb = table.Coins[i];
                rb.transform.SetPositionAndRotation(poses[i].Position, poses[i].Rotation);
                rb.position = poses[i].Position;
                rb.rotation = poses[i].Rotation;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            Physics.SyncTransforms();
        }

        private static void ResetAll(Table table, Vector3[] slots)
        {
            Restore(table, slots.Select(p => new Pose { Position = p, Rotation = Quaternion.identity }).ToArray());
            for (int step = 0; step < 20; step++) { table.Physics.Simulate(Dt); }
        }

        private static void ReturnDropped(Table table, Vector3[] slots)
        {
            Bounds bounds = table.Board.bounds;
            for (int i = 0; i < table.Coins.Count; i++)
            {
                Rigidbody rb = table.Coins[i];
                if (OutOfBoard(rb.position, bounds) == false) { continue; }
                //  시작 자리에 다른 동전이 와 있을 수 있다 — 조금 위에서 떨궈 얹히게 한다.
                Vector3 p = slots[i] + Vector3.up * 0.1f;
                rb.transform.SetPositionAndRotation(p, Quaternion.identity);
                rb.position = p;
                rb.rotation = Quaternion.identity;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            Physics.SyncTransforms();
            for (int step = 0; step < 50; step++) { table.Physics.Simulate(Dt); }
        }

        /// <returns>낙이 났으면 true.</returns>
        private static bool StrikeAndSettle(Table table, Vector3 strikePoint, float hold)
        {
            var tuning = new PanchigiStrike.StrikeTuning(ForceMultiplier, HorizontalForceMultiplier, InfluenceRadius);
            var samples = new System.Numerics.Vector3[CoverageSamples];
            var live = new System.Numerics.Vector3[CoverageSamples];
            Bounds boardBounds = table.Board.bounds;

            var input = new PanchigiStrike.StrikeInput(strikePoint.ToNumerics(), System.Numerics.Vector3.Zero, hold);
            foreach (Rigidbody rb in table.Coins)
            {
                PanchigiStrike.BuildSamples(rb.position.ToNumerics(), CoinRadius, samples);
                float reach = new Vector3(CoinRadius, CoinThickness * 0.5f, CoinRadius).magnitude + 0.01f;
                int liveCount = 0;
                for (int i = 0; i < CoverageSamples; i++)
                {
                    Vector3 s = samples[i].ToUnity();
                    if (s.x < boardBounds.min.x || s.x > boardBounds.max.x || s.z < boardBounds.min.z || s.z > boardBounds.max.z)
                    {
                        continue;
                    }
                    if (table.Physics.Raycast(s, Vector3.down, out RaycastHit hit, reach) == false || hit.collider != table.Board)
                    {
                        continue;
                    }
                    live[liveCount++] = samples[i];
                }

                System.Numerics.Vector3 impulse = PanchigiStrike.ComputeImpulse(input, tuning, live, liveCount, CoverageSamples);
                if (impulse != System.Numerics.Vector3.Zero)
                {
                    rb.AddForceAtPosition(impulse.ToUnity(), strikePoint, ForceMode.Impulse);
                }
            }

            int rest = 0;
            for (int step = 0; step < MaxSteps && rest < RestTicks; step++)
            {
                table.Physics.Simulate(Dt);
                bool allRest = true;
                foreach (Rigidbody rb in table.Coins)
                {
                    if (OutOfBoard(rb.position, boardBounds)) { continue; }
                    if (rb.linearVelocity.magnitude > RestSpeed || rb.angularVelocity.magnitude > RestAngular)
                    {
                        allRest = false;
                        break;
                    }
                }
                rest = allRest ? rest + 1 : 0;
            }

            return table.Coins.Any(c => OutOfBoard(c.position, boardBounds));
        }

        private static int FlippedCount(Table table)
        {
            return table.Coins.Count(c => OnBoard(c, table) && IsFlipped(c));
        }

        private static bool IsFlipped(Rigidbody c) => Vector3.Dot(c.rotation * Vector3.up, Vector3.up) < 0f;

        private static bool OnBoard(Rigidbody c, Table table) => OutOfBoard(c.position, table.Board.bounds) == false;

        private static bool OutOfBoard(Vector3 p, Bounds board)
        {
            return p.x < board.min.x || p.x > board.max.x || p.z < board.min.z || p.z > board.max.z || p.y < board.min.y;
        }
    }
}
