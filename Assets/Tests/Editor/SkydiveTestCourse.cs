using System.Collections.Generic;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 서버 시스템 시험용 코스 — 옛 더미 맵(SkydiveCourseLayout, 10-07 지움)의 체크포인트 값을 그대로 옮겼다.
    /// 시험이 "지나온 체크포인트로 돌아간다"를 재려면 체크포인트가 있어야 해서, 비면 옛 표를 주던 폴백 대신 명시적으로 채운다.
    /// </summary>
    public static class SkydiveTestCourse
    {
        public const float SpawnY = 3000f;

        public static readonly IReadOnlyList<float> ShelfYs = new[] { 2600f, 2200f, 1800f, 1400f, 1000f, 600f, 200f };

        public static readonly IReadOnlyDictionary<float, Vector3> RespawnPoints = new Dictionary<float, Vector3>
        {
            { 2600f, new Vector3(0f, 2600f, 40f) },
            { 2200f, new Vector3(30f, 2200f, 40f) },
            { 1800f, new Vector3(30f, 1800f, -10f) },
            { 1400f, new Vector3(-25f, 1400f, -10f) },
            { 1000f, new Vector3(-25f, 1000f, 10f) },
            { 600f, new Vector3(30f, 600f, 15f) },
            { 200f, new Vector3(0f, 200f, -15f) },
        };

        /// <summary>맵 표식이 다 들어온 것과 같은 필드(맨 위 = 스폰).</summary>
        public static CheckpointField Checkpoints()
        {
            var field = new CheckpointField();
            field.Add(SpawnY, new Vector3(0f, SpawnY, 0f));
            foreach (var pair in RespawnPoints) { field.Add(pair.Key, pair.Value); }
            return field;
        }
    }
}
