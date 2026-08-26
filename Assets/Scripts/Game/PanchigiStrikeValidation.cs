using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 타격 메시지가 규칙에 맞나. 접촉점 하나만 어긋나도 <b>치기 전체</b>를 버린다 —
    /// 일부만 버리고 나머지를 적용하면 조작된 값이 조용히 섞여 들어간다.
    /// </summary>
    public static class PanchigiStrikeValidation
    {
        //  클라가 상한에 맞춰 자른 값이라도 성분에서 크기를 다시 재면 미세하게 커질 수 있다
        //  (ClampMagnitude는 성분을 다시 계산한다). 정직한 클라가 경계에서 거절당하지 않게 봐준다.
        public const float BoundEpsilon = 0.001f;

        /// <summary>한 접촉점. 와이어 타입을 이 레이어까지 끌고 오지 않으려고 따로 둔다.</summary>
        public readonly struct Contact
        {
            public readonly Vector3 StrikePoint;
            public readonly Vector3 DragDelta;
            public readonly float HoldTime;

            public Contact(Vector3 strikePoint, Vector3 dragDelta, float holdTime)
            {
                StrikePoint = strikePoint;
                DragDelta = dragDelta;
                HoldTime = holdTime;
            }
        }

        /// <summary>통과하면 true. 막히면 false와 함께 왜 막혔는지를 <paramref name="reason"/>에 담는다.</summary>
        public static bool Validate(IReadOnlyList<Contact> contacts, Bounds boardBounds,
            float holdTimeMax, float strikePowerMax, int contactMax, out string reason)
        {
            if (contacts == null || contacts.Count == 0)
            {
                reason = "접촉점이 없다";
                return false;
            }
            if (contacts.Count > contactMax)
            {
                reason = $"접촉점이 상한을 넘었다 {contacts.Count} > {contactMax}";
                return false;
            }

            for (int i = 0; i < contacts.Count; i++)
            {
                Contact c = contacts[i];
                if (ContainsXZ(boardBounds, c.StrikePoint) == false)
                {
                    reason = $"[{i}] 판 밖 타격점 {c.StrikePoint}";
                    return false;
                }
                if (c.HoldTime < -BoundEpsilon || c.HoldTime > holdTimeMax + BoundEpsilon)
                {
                    reason = $"[{i}] 누른 시간 범위 밖 {c.HoldTime}";
                    return false;
                }
                if (c.DragDelta.magnitude > strikePowerMax + BoundEpsilon)
                {
                    reason = $"[{i}] 세기 범위 밖 {c.DragDelta.magnitude}";
                    return false;
                }
            }

            reason = null;
            return true;
        }

        //  판은 평면이라 높이는 보지 않는다 — 위아래로 얼마나 떨어져 있든 "판 위"다.
        //  가장자리를 정확히 친 값도 반올림으로 밖에 떨어질 수 있어 BoundEpsilon만큼 넉넉히 본다.
        public static bool ContainsXZ(Bounds bounds, Vector3 point)
            => point.x >= bounds.min.x - BoundEpsilon && point.x <= bounds.max.x + BoundEpsilon
            && point.z >= bounds.min.z - BoundEpsilon && point.z <= bounds.max.z + BoundEpsilon;
    }
}
