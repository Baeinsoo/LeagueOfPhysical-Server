using System.Collections.Generic;
using System.IO;
using System.Text;
using GameFramework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LOP.EditorTools
{
    /// <summary>
    /// 판치기 타격이 동전을 어떻게 뒤집는지 잰다. 처음 목적은 편 나누기(8개 = 4 대 4)가 게임이 되는지 —
    /// <b>내 동전만 골라 뒤집을 수 있나</b> — 였다(2026-09-26: 지금 방식으로는 안 된다. 어디를 쳐도 반반).
    ///
    /// 너무 잘 골라지면 상대와 상관없는 각자 경주가 되고, 전혀 안 골라지면 운 게임이 된다.
    /// 그래서 "겨냥해서 친 최선"과 "아무 데나 친 평균"을 나란히 본다. 둘의 차이가 실력이 들어갈 자리다.
    ///
    /// 게임 씬과 섞이지 않게 미리보기 씬(자기 물리 공간)에서 굴린다. 타격 계산은 서버 핸들러
    /// (<c>PanchigiStrikeMessageHandler.ApplyStrike</c>)와 같은 커널·같은 샘플 걸러내기를 쓴다.
    /// 오래 걸리므로 결과는 파일로 남긴다(CLI 응답은 30초에 끊긴다).
    /// </summary>
    public static class PanchigiTeamSelectivityProbe
    {
        //  TbPanchigiConfig(1) 현재 값.
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

        /// <summary>
        /// 거짓이면 게임과 같다 — 임펄스를 <b>친 자리</b>에 건다(먼 동전일수록 지렛대가 길어 1.5m 밖도 뒤집힌다).
        /// 참이면 비교안 — 판에 닿은 샘플을 눌린 세기로 가중평균한 <b>동전 밑 한 점</b>에 건다(지렛대 ≤ 반지름).
        /// 비교안은 한 번 게임에 넣어 봤다가 되돌렸다 — 골라 치기는 쉬워졌지만 현실 근거가 약했다.
        /// </summary>
        public static bool FootprintTorque;

        /// <summary>손가락 수(1~3)와 손가락 사이 거리(판 위 미터). 친 자리를 중심으로 가로줄·삼각형으로 놓는다.</summary>
        public static int Contacts = 1;
        public static float ContactSpacing = 0.25f;

        private static Vector3[] ContactPoints(Vector3 center)
        {
            float h = ContactSpacing;
            return Contacts switch
            {
                2 => new[] { center + new Vector3(-h * 0.5f, 0f, 0f), center + new Vector3(h * 0.5f, 0f, 0f) },
                3 => new[] { center + new Vector3(-h * 0.5f, 0f, -h * 0.29f), center + new Vector3(h * 0.5f, 0f, -h * 0.29f), center + new Vector3(0f, 0f, h * 0.58f) },
                _ => new[] { center },
            };
        }

        //  판(책 표지) — 씬의 Board와 같은 치수. 윗면 y = 0.01.
        private static readonly Vector3 BoardSize = new(2.8f, 0.25f, 4f);
        private const float BoardTopY = 0.01f;

        private struct Outcome
        {
            public int Own;
            public int Enemy;
            public bool Dropped;
        }

        /// <summary>
        /// ① 판 전체를 격자로 훑어 "정확히 쳤을 때" 가장 좋은 자리·세기를 찾고
        /// ② 그 상위 자리를 손가락 오차만큼 흔들어 다시 쳐 "사람이 쳤을 때"를 잰다
        /// ③ 아무 데나 친 평균과 비교한다.
        /// </summary>
        public static string Run(string outPath, float spacing, bool checker, int randomTrials,
            float gridStep, float jitterRadius, int jitterCount)
        {
            var sb = new StringBuilder();
            string layoutName = checker ? "체크무늬" : "반반(열로 나눔)";
            sb.AppendLine($"== 배치 2열x4행 {layoutName}, 간격 {spacing}m, 손가락 오차 {jitterRadius * 100f:F0}cm — 힘 거는 곳: {(FootprintTorque ? "동전 밑(비교안)" : "친 자리(게임)")}, 손가락 {Contacts}개(간격 {ContactSpacing}m)");

            Vector3[] slots = Slots(spacing);
            int[] team = new int[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                int col = i % 2, row = i / 2;
                team[i] = checker ? (col + row) % 2 : col;
            }

            SimulationMode previousMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            Scene scene = EditorSceneManager.NewPreviewScene();
            try
            {
                PhysicsScene physics = scene.GetPhysicsScene();
                float[] holds = { 0.3f, 0.5f, 0.7f, 1.0f };
                var grid = new List<(Vector3 point, float hold, Outcome o)>();

                for (float x = -BoardSize.x * 0.5f + gridStep * 0.5f; x < BoardSize.x * 0.5f; x += gridStep)
                {
                    for (float z = -BoardSize.z * 0.5f + gridStep * 0.5f; z < BoardSize.z * 0.5f; z += gridStep)
                    {
                        foreach (float hold in holds)
                        {
                            var point = new Vector3(x, BoardTopY, z);
                            grid.Add((point, hold, Strike(scene, physics, slots, team, point, hold)));
                        }
                    }
                }

                static float Score(Outcome o) => o.Own - o.Enemy - (o.Dropped ? 3f : 0f);
                grid.Sort((a, b) => Score(b.o).CompareTo(Score(a.o)));

                int cleanExact = 0, dropExact = 0;
                float ownExact = 0, enemyExact = 0;
                foreach (var g in grid)
                {
                    ownExact += g.o.Own; enemyExact += g.o.Enemy;
                    if (g.o.Own > 0 && g.o.Enemy == 0 && g.o.Dropped == false) { cleanExact++; }
                    if (g.o.Dropped) { dropExact++; }
                }
                sb.AppendLine($"-- 격자 {grid.Count}곳(정확히 침) 평균: 내 {ownExact / grid.Count:F2} / 상대 {enemyExact / grid.Count:F2} / 깨끗한 자리 {cleanExact / (float)grid.Count:P0} / 낙 {dropExact / (float)grid.Count:P0}");

                var rng = new System.Random(777);
                sb.AppendLine($"-- 정확히 쳤을 때 상위 10곳 → 같은 곳을 오차 {jitterRadius * 100f:F0}cm·세기 ±0.05로 {jitterCount}번 쳤을 때");
                for (int i = 0; i < Mathf.Min(10, grid.Count); i++)
                {
                    var g = grid[i];
                    float own = 0, enemy = 0, clean = 0, drop = 0;
                    for (int j = 0; j < jitterCount; j++)
                    {
                        double a = rng.NextDouble() * Mathf.PI * 2.0, r = jitterRadius * System.Math.Sqrt(rng.NextDouble());
                        var p = g.point + new Vector3((float)(System.Math.Cos(a) * r), 0f, (float)(System.Math.Sin(a) * r));
                        float h = Mathf.Clamp(g.hold + (float)(rng.NextDouble() - 0.5) * 0.1f, 0.05f, 1f);
                        Outcome o = Strike(scene, physics, slots, team, p, h);
                        own += o.Own; enemy += o.Enemy;
                        if (o.Own > 0 && o.Enemy == 0 && o.Dropped == false) { clean++; }
                        if (o.Dropped) { drop++; }
                    }
                    sb.AppendLine($"  ({g.point.x:F1},{g.point.z:F1}) 세기 {g.hold:F1}: 정확히 내{g.o.Own}/상대{g.o.Enemy}{(g.o.Dropped ? "/낙" : "")}"
                        + $" → 사람 내 {own / jitterCount:F2} / 상대 {enemy / jitterCount:F2} / 깨끗 {clean / jitterCount:P0} / 낙 {drop / jitterCount:P0}");
                }

                var rr = new System.Random(12345);
                float rOwn = 0, rEnemy = 0, rClean = 0, rDrop = 0, rAny = 0;
                for (int t = 0; t < randomTrials; t++)
                {
                    Vector3 point = new(
                        (float)(rr.NextDouble() - 0.5) * BoardSize.x,
                        BoardTopY,
                        (float)(rr.NextDouble() - 0.5) * BoardSize.z);
                    float hold = 0.3f + (float)rr.NextDouble() * 0.7f;
                    Outcome o = Strike(scene, physics, slots, team, point, hold);
                    rOwn += o.Own; rEnemy += o.Enemy;
                    if (o.Own > 0 && o.Enemy == 0 && o.Dropped == false) { rClean++; }
                    if (o.Own + o.Enemy > 0) { rAny++; }
                    if (o.Dropped) { rDrop++; }
                }
                sb.AppendLine($"-- 아무 데나 {randomTrials}회: 내 {rOwn / randomTrials:F2} / 상대 {rEnemy / randomTrials:F2} / 깨끗 {rClean / randomTrials:P0} / 뭐라도 뒤집힘 {rAny / randomTrials:P0} / 낙 {rDrop / randomTrials:P0}");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                Physics.simulationMode = previousMode;
            }

            string text = sb.ToString();
            File.AppendAllText(outPath, text + "\n");
            return text;
        }

        /// <summary>
        /// 측정 도구부터 믿을 수 있나. ① 세기 0이면 아무것도 안 뒤집혀야 한다 ② 같은 타격은 같은 결과
        /// ③ 한 번의 타격에서 동전마다 무슨 일이 났는지(거리·뒤집힘·들린 높이).
        /// </summary>
        public static string Sanity(string outPath, float spacing, float dx, float dz, float hold)
        {
            var sb = new StringBuilder();
            Vector3[] slots = Slots(spacing);
            int[] team = new int[slots.Length];
            for (int i = 0; i < slots.Length; i++) { team[i] = (i % 2 + i / 2) % 2; }

            SimulationMode previousMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            Scene scene = EditorSceneManager.NewPreviewScene();
            try
            {
                PhysicsScene physics = scene.GetPhysicsScene();
                Vector3 point = slots[3] + new Vector3(dx, 0f, dz);
                point.y = BoardTopY;

                Outcome zero = Strike(scene, physics, slots, team, point, 0f);
                sb.AppendLine($"[세기 0] 내 {zero.Own} 상대 {zero.Enemy} 낙 {zero.Dropped}");

                for (int r = 0; r < 3; r++)
                {
                    Outcome o = Strike(scene, physics, slots, team, point, hold, sb);
                    sb.AppendLine($"[반복 {r}] 내 {o.Own} 상대 {o.Enemy} 낙 {o.Dropped}");
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                Physics.simulationMode = previousMode;
            }

            string text = sb.ToString();
            File.AppendAllText(outPath, text + "\n");
            return text;
        }

        /// <summary>동전 하나만 놓고, 친 거리·세기별로 뒤집히는 비율.</summary>
        public static string FlipByDistance(string outPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"== 동전 하나 — 친 거리별 뒤집힘 비율 (방향 8개 평균) — 힘 거는 곳: {(FootprintTorque ? "동전 밑(비교안)" : "친 자리(게임)")}");
            var slots = new[] { new Vector3(0f, BoardTopY + CoinThickness * 0.5f + 0.002f, 0f) };
            var team = new[] { 0 };
            float[] holds = { 0.3f, 0.5f, 0.7f, 1.0f };
            sb.AppendLine("  거리  | " + string.Join(" | ", System.Array.ConvertAll(holds, h => $"세기 {h:F1}")));

            SimulationMode previousMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            Scene scene = EditorSceneManager.NewPreviewScene();
            try
            {
                PhysicsScene physics = scene.GetPhysicsScene();
                for (float d = 0f; d <= 1.61f; d += 0.1f)
                {
                    var line = new StringBuilder($"  {d:F1}m |");
                    foreach (float hold in holds)
                    {
                        int flips = 0;
                        for (int k = 0; k < 8; k++)
                        {
                            float a = k * Mathf.PI / 4f;
                            var p = new Vector3(Mathf.Cos(a) * d, BoardTopY, Mathf.Sin(a) * d);
                            if (Strike(scene, physics, slots, team, p, hold).Own > 0) { flips++; }
                        }
                        line.Append($"   {flips / 8f,5:P0}  |");
                    }
                    sb.AppendLine(line.ToString());
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                Physics.simulationMode = previousMode;
            }

            string text = sb.ToString();
            File.AppendAllText(outPath, text + "\n");
            return text;
        }

        private static Vector3[] Slots(float spacing)
        {
            //  2열(x) x 4행(z). 순서: 행마다 왼쪽, 오른쪽.
            var slots = new Vector3[8];
            for (int row = 0; row < 4; row++)
            {
                for (int col = 0; col < 2; col++)
                {
                    slots[row * 2 + col] = new Vector3(
                        (col - 0.5f) * spacing,
                        BoardTopY + CoinThickness * 0.5f + 0.002f,
                        (row - 1.5f) * spacing);
                }
            }
            return slots;
        }

        private static Outcome Strike(Scene scene, PhysicsScene physics, Vector3[] slots, int[] team,
            Vector3 strikePoint, float hold, StringBuilder detail = null)
        {
            var roots = new List<GameObject>();
            var coins = new List<Rigidbody>();

            GameObject board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "probe-board";
            board.transform.localScale = BoardSize;
            board.transform.position = new Vector3(0f, BoardTopY - BoardSize.y * 0.5f, 0f);
            SceneManager.MoveGameObjectToScene(board, scene);
            roots.Add(board);
            Collider boardCollider = board.GetComponent<Collider>();

            //  바닥 — 판 밖으로 떨어진 동전이 끝없이 떨어지지 않게.
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.localScale = new Vector3(40f, 1f, 40f);
            floor.transform.position = new Vector3(0f, -3f, 0f);
            SceneManager.MoveGameObjectToScene(floor, scene);
            roots.Add(floor);

            for (int i = 0; i < slots.Length; i++)
            {
                var go = new GameObject($"probe-coin-{i}");
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
                roots.Add(go);
                coins.Add(go.GetComponent<Rigidbody>());
            }

            //  먼저 가라앉힌다 — 스폰 직후 접촉이 자리잡기 전에 치면 판에 안 닿은 것으로 걸러진다.
            for (int i = 0; i < 20; i++) { physics.Simulate(Dt); }

            var tuning = new PanchigiStrike.StrikeTuning(ForceMultiplier, HorizontalForceMultiplier, InfluenceRadius);
            var samples = new System.Numerics.Vector3[CoverageSamples];
            var live = new System.Numerics.Vector3[CoverageSamples];
            Bounds boardBounds = boardCollider.bounds;

            //  서버와 같다 — 손가락마다 타격을 따로 한 번씩 더한다.
            foreach (Vector3 contact in ContactPoints(strikePoint))
            {
                var input = new PanchigiStrike.StrikeInput(contact.ToNumerics(), System.Numerics.Vector3.Zero, hold);
                foreach (Rigidbody rb in coins)
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
                        if (physics.Raycast(s, Vector3.down, out RaycastHit hit, reach) == false || hit.collider != boardCollider)
                        {
                            continue;
                        }
                        live[liveCount++] = samples[i];
                    }

                    System.Numerics.Vector3 impulse = PanchigiStrike.ComputeImpulse(input, tuning, live, liveCount, CoverageSamples);
                    if (impulse != System.Numerics.Vector3.Zero)
                    {
                        //  비교용: 옛 방식(친 자리에 건다) / 게임이 쓰는 방식(동전 밑).
                        Vector3 at = FootprintTorque ? FootprintPoint(contact, live, liveCount) : contact;
                        rb.AddForceAtPosition(impulse.ToUnity(), at, ForceMode.Impulse);
                    }
                }
            }

            var maxLift = new float[coins.Count];
            int rest = 0;
            for (int step = 0; step < MaxSteps && rest < RestTicks; step++)
            {
                physics.Simulate(Dt);
                for (int i = 0; i < coins.Count; i++)
                {
                    maxLift[i] = Mathf.Max(maxLift[i], coins[i].position.y - slots[i].y);
                }
                bool allRest = true;
                foreach (Rigidbody rb in coins)
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

            var outcome = new Outcome();
            for (int i = 0; i < coins.Count; i++)
            {
                if (OutOfBoard(coins[i].position, boardBounds)) { outcome.Dropped = true; continue; }
                bool flipped = Vector3.Dot(coins[i].rotation * Vector3.up, Vector3.up) < 0f;
                if (detail != null)
                {
                    float dist = Vector2.Distance(new Vector2(slots[i].x, slots[i].z), new Vector2(strikePoint.x, strikePoint.z));
                    detail.AppendLine($"    #{i} 편{team[i]} 거리 {dist:F2} 최고 {maxLift[i]:F2}m 뒤집힘 {flipped}");
                }
                if (flipped == false) { continue; }
                if (team[i] == 0) { outcome.Own++; } else { outcome.Enemy++; }
            }

            foreach (GameObject go in roots) { Object.DestroyImmediate(go); }
            return outcome;
        }

        private static Vector3 FootprintPoint(Vector3 contact, System.Numerics.Vector3[] live, int liveCount)
        {
            Vector3 sum = Vector3.zero;
            float wsum = 0f;
            for (int i = 0; i < liveCount; i++)
            {
                Vector3 s = live[i].ToUnity();
                float w = Mathf.Exp(-Vector2.Distance(new Vector2(s.x, s.z), new Vector2(contact.x, contact.z)) / InfluenceRadius);
                sum += s * w;
                wsum += w;
            }
            return sum / wsum;
        }

        private static bool OutOfBoard(Vector3 p, Bounds board)
        {
            return p.x < board.min.x || p.x > board.max.x || p.z < board.min.z || p.z > board.max.z || p.y < board.min.y;
        }
    }
}
