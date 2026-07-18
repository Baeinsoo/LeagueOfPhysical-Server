using GameFramework;
using LOP.Event.Entity;
using MessagePipe;
using Unity.VisualScripting;
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// World.Transform을 따라가는 물리 바디(Rigidbody/캡슐 콜라이더)를 소유하는 프레젠테이션 컴포넌트.
    /// rb 팔로우는 호스트 단일 패스(LOPRunner)가 MotionBridge.PushMotion으로 구동한다.
    /// 서버는 트리거로 아이템 접촉(ItemTouch)을 감지한다.
    /// </summary>
    public class PhysicsFollower : MonoBehaviour
    {
        [Inject]
        private GameFramework.World.EntityRegistry entityRegistry;

        [Inject]
        private IPublisher<ItemTouch> itemTouchPublisher;

        private GameFramework.World.Entity worldEntity;

        public Rigidbody entityRigidbody { get; private set; }
        public Collider[] entityColliders { get; private set; }

        public void Initialize(GameFramework.World.Entity worldEntity, bool isKinematic, bool isTrigger)
        {
            this.worldEntity = worldEntity;
            var worldTransform = worldEntity.Get<GameFramework.World.Transform>();
            var worldVelocity = worldEntity.Get<GameFramework.World.Velocity>();

            gameObject.layer = LayerMask.NameToLayer("Character");

            // 루트(시뮬 바디)를 스폰 위치에 즉시 배치 — kinematic rb.position은 다음 물리 스텝에야 반영돼
            // 루트가 한 틱 원점에 머물다 점프하면 자식 모델이 끌려가 첫 틱 순간이동한다(그 방지, 클라와 동일).
            transform.SetPositionAndRotation(worldTransform.Position.ToUnity(), worldTransform.Rotation.ToUnity());

            entityRigidbody = gameObject.AddComponent<Rigidbody>();
            entityRigidbody.linearDamping = 0f;
            entityRigidbody.angularDamping = 0.05f;
            entityRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            entityRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            entityRigidbody.position = worldTransform.Position.ToUnity();
            entityRigidbody.rotation = worldTransform.Rotation.ToUnity();
            entityRigidbody.linearVelocity = worldVelocity.Linear.ToUnity();
            entityRigidbody.isKinematic = isKinematic;

            CapsuleCollider capsuleCollider = gameObject.AddComponent<CapsuleCollider>();
            capsuleCollider.radius = 0.35f;
            capsuleCollider.height = 1.5f;
            capsuleCollider.center = new Vector3(0, capsuleCollider.height * 0.5f, 0);
            capsuleCollider.isTrigger = isTrigger;
            entityColliders = new Collider[] { capsuleCollider };

            TriggerDetector triggerDetector = gameObject.GetOrAddComponent<TriggerDetector>();
            triggerDetector.onTriggerEnter += OnTriggerEnter;
            triggerDetector.onTriggerStay += OnTriggerStay;
            triggerDetector.onTriggerExit += OnTriggerExit;
        }

        private void OnTriggerEnter(Collider other)
        {
            LOPActor otherEntity = other.GetComponentInParent<LOPActor>();
            if (otherEntity == null)
            {
                Debug.LogWarning($"Trigger detected with non-entity object: {other.name}");
                return;
            }

            if (entityRegistry.Get(otherEntity.entityId)?.Has<GameFramework.World.Ownership>() == true)
            {
                itemTouchPublisher.Publish(new ItemTouch(worldEntity.Id, otherEntity.entityId));
            }
        }

        private void OnTriggerStay(Collider other)
        {
        }

        private void OnTriggerExit(Collider other)
        {
        }
    }
}
