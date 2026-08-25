using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 동전 하나의 상태를 보고 참/거짓을 내는 판정들. 계산이 아니라 판단이라 값만 받는다.
    /// 판치기는 클라 예측이 없어 이 판정을 서버만 한다.
    /// </summary>
    public static class PanchigiCoin
    {
        /// <summary>
        /// 뒤집혔나. 동전은 전부 같은 면(+up)으로 놓이므로 윗면이 아래를 보면 뒤집힌 것이다.
        /// 모로 선 동전(내적 ≈ 0)은 뒤집힌 것으로 치지 않는다 — 실제로 나오는 자세라 미리 정해 둔다.
        /// </summary>
        public static bool IsFlipped(System.Numerics.Quaternion rotation)
        {
            Quaternion q = new Quaternion(rotation.X, rotation.Y, rotation.Z, rotation.W);
            return Vector3.Dot(q * Vector3.up, Vector3.up) < 0f;
        }

        /// <summary>판을 벗어났나. 판 위에서 x·z가 벗어났거나 판 아래로 떨어진 경우.</summary>
        public static bool IsOutOfBoard(System.Numerics.Vector3 position, Bounds board)
        {
            if (position.X < board.min.x || position.X > board.max.x) { return true; }
            if (position.Z < board.min.z || position.Z > board.max.z) { return true; }
            return position.Y < board.min.y;
        }

        /// <summary>
        /// 이 한 틱만 놓고 볼 때 멎어 있나. 튀어 오른 동전은 정점에서 속도가 순간 0을 지나므로
        /// 이것만으로 "멎었다"고 하면 안 된다 — 연속 몇 틱인지는 부르는 쪽이 센다.
        /// 제자리에서 도는 동전은 선속도가 0이라 각속도도 같이 본다.
        /// </summary>
        public static bool IsAtRest(System.Numerics.Vector3 linear, System.Numerics.Vector3 angular,
            float speedEpsilon, float angularEpsilon)
        {
            return linear.Length() <= speedEpsilon && angular.Length() <= angularEpsilon;
        }
    }
}
