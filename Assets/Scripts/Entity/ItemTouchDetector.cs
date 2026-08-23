using GameFramework;
using LOP.Event.Entity;
using MessagePipe;
using Unity.VisualScripting;
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 아이템에 몸이 닿았을 때 <see cref="ItemTouch"/>를 발행한다. 줍기 판정은 서버만 한다.
    ///
    /// 예전엔 물리 몸을 만드는 코드와 한 클래스(`PhysicsFollower`)에 붙어 있었는데, 둘은 상관없는
    /// 일이다 — 몸 만들기는 클·서 공통(`PhysicsBodyFactory`)이고 접촉 판정은 서버만의 규칙이다.
    /// </summary>
    public class ItemTouchDetector : MonoBehaviour
    {
        [Inject]
        private GameFramework.World.EntityRegistry entityRegistry;

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
                // 바닥 등 엔티티 아닌 콜라이더와의 접촉은 정상(아이템 줍기 대상이 아님) — 조용히 무시.
                return;
            }

            //  상대가 주인 있는 엔티티(=플레이어)일 때만 줍기다. 아이템끼리 닿는 것은 아무 일도 아니다.
            if (entityRegistry.Get(otherEntity.entityId)?.Has<GameFramework.World.Ownership>() == true)
            {
                itemTouchPublisher.Publish(new ItemTouch(worldEntity.Id, otherEntity.entityId));
            }
        }
    }
}
