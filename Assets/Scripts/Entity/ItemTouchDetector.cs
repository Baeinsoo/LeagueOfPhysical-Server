using GameFramework;
using LOP.Event.Entity;
using MessagePipe;
using Unity.VisualScripting;
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 아이템에 뭔가 닿았다는 **엔진 사실**을 도메인 이벤트(<see cref="ItemTouch"/>)로 옮긴다.
    ///
    /// 딱 그것만 한다 — *그게 줍기인가*(닿은 게 플레이어인가)는 규칙(`FlapWangRuleSystem`)이 정한다.
    /// 그래서 이 클래스는 World Core를 모른다. 트리거를 받으려면 MonoBehaviour일 수밖에 없으므로
    /// 여기 있는 것이고, 도메인 지식을 들이면 그때부터 게임 규칙이 Unity 레이어로 새기 시작한다.
    /// </summary>
    public class ItemTouchDetector : MonoBehaviour
    {
        [Inject]
        private IPublisher<ItemTouch> itemTouchPublisher;

        private GameFramework.World.Entity worldEntity;

        public void Initialize(GameFramework.World.Entity worldEntity)
        {
            this.worldEntity = worldEntity;

            TriggerDetector triggerDetector = gameObject.GetOrAddComponent<TriggerDetector>();
            triggerDetector.onTriggerEnter += OnTriggerEnter;
        }

        private void OnTriggerEnter(Collider other)
        {
            LOPActor otherEntity = other.GetComponentInParent<LOPActor>();
            if (otherEntity == null)
            {
                // 바닥 등 엔티티가 아닌 콜라이더는 옮길 도메인 사실이 없다 — 조용히 무시.
                return;
            }

            itemTouchPublisher.Publish(new ItemTouch(worldEntity.Id, otherEntity.entityId));
        }
    }
}
