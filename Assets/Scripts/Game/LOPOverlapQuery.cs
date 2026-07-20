using System.Collections.Generic;
using System.Linq;
using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>IOverlapQuery 서버 구체 — Physics.OverlapSphere로 범위 검색 후 collider를 LOPActor로 매핑해
    /// entityId를 반환. LOPActor(사이드 타입)를 알아야 해서 각 레포에 존재(의도적 사이드 분기).
    /// 레거시 DamageEffectHandler의 OverlapSphere+매핑 그대로 이식. Plane 등 엔티티 없는 콜라이더는 자연 제외.</summary>
    public sealed class LOPOverlapQuery : GameFramework.Physics.IOverlapQuery
    {
        public string[] OverlapSphere(System.Numerics.Vector3 center, float radius)
        {
            LayerMask layerMask = LayerMask.GetMask("Character");
            Collider[] hits = Physics.OverlapSphere(center.ToUnity(), radius, layerMask);

            var ids = new HashSet<string>();   // 한 엔티티 다중 콜라이더 → 중복 제거(키 기반 RNG라 순서·중복 무관)
            foreach (var hit in hits)
            {
                var actor = hit.GetComponentInParent<LOPActor>();
                if (actor != null)
                {
                    ids.Add(actor.entityId);
                }
            }
            return ids.ToArray();
        }
    }
}
