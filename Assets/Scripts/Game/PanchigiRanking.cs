using System.Collections.Generic;

namespace LOP
{
    /// <summary>판치기 순위 — 타수가 적은 순. 같으면 공동이고 다음 순위는 그만큼 건너뛴다(1, 1, 3).</summary>
    public static class PanchigiRanking
    {
        public static Dictionary<string, int> Place(IReadOnlyDictionary<string, int> strokes)
        {
            var places = new Dictionary<string, int>();
            foreach (var mine in strokes)
            {
                int better = 0;
                foreach (var other in strokes)
                {
                    if (other.Value < mine.Value) { better++; }
                }
                places[mine.Key] = better + 1;
            }
            return places;
        }
    }
}
