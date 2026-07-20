using GameFramework;
using LOP.Event.Entity;
using MessagePipe;
using System;
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 서버 뷰 스포너 — 엔티티 수명 신호(<see cref="EntityCreated"/>/<see cref="EntityDestroyed"/>)에 반응해
    /// actor GameObject + 서버측 Unity 뷰(물리 팔로워 + PhysicsBody + 테스트렌더 뷰 + 비-플레이어 AIController)를
    /// 생성·파괴한다. Creator는 데이터만. (서버는 장식 뷰·보간 없음 — 권위 시뮬.)
    ///
    /// 뷰를 Creator에서 여기로 떼어내도 안전한 이유: <see cref="EntityCreated"/>가 동기 발행이라
    /// 이 핸들러가 CreateEntity 반환 전에 뷰·PhysicsBody를 전부 붙인다 → "뷰/물리 없는 엔티티"가 보이는 틈이 없다.
    /// </summary>
    public class EntityBinder : MessageHandlerBase
    {
        [Inject] private IObjectResolver objectResolver;
        [Inject] private ISubscriber<EntityCreated> entityCreatedSubscriber;
        [Inject] private ISubscriber<EntityDestroyed> entityDestroyedSubscriber;
        [Inject] private GameFramework.World.EntityRegistry entityRegistry;
        [Inject] private ActorRegistry actorRegistry;

        protected override void Subscribe()
        {
            Track(entityCreatedSubscriber.Subscribe(OnEntityCreated));
            Track(entityDestroyedSubscriber.Subscribe(OnEntityDestroyed));
        }

        private void OnEntityCreated(EntityCreated entityCreated)
        {
            GameFramework.World.Entity worldEntity = entityRegistry.Get(entityCreated.entityId);
            if (worldEntity == null)
            {
                return;
            }
            EntityKind kind = worldEntity.Get<EntityKind>();
            if (kind == null)
            {
                return;
            }

            GameObject root = new GameObject($"Actor_{entityCreated.entityId}");
            LOPActor actor = root.AddComponent<LOPActor>();
            objectResolver.Inject(actor);
            actor.SetEntityId(entityCreated.entityId);
            actorRegistry.Add(actor);

            bool isItem = kind.Kind == EntityType.Item;

            // PhysicsBody는 반드시 이 핸들러 안에서 붙인다: 물리 루프가 매 틱 등록된 엔티티를 돌며 몸을 밀기 때문에,
            // 등록만 되고 몸이 아직 없는 순간이 생기면 그 틱 위치가 한 프레임 어긋난다(동기 발행이라 여기선 그 틈이 없다).
            PhysicsFollower physicsFollower = root.AddComponent<PhysicsFollower>();
            objectResolver.Inject(physicsFollower);
            physicsFollower.Initialize(worldEntity, true, isItem);
            // 제네릭을 <PhysicsBody>로 명시해야 한다 — Add<T>는 typeof(T)를 키로 쓰므로,
            // 생략하면 UnityPhysicsBody 키로 저장돼 나중에 Get<PhysicsBody>()가 못 찾는다.
            worldEntity.Add<GameFramework.World.PhysicsBody>(new UnityPhysicsBody(physicsFollower.entityRigidbody, (CapsuleCollider)physicsFollower.entityColliders[0]));

            LOPEntityView view = root.AddComponent<LOPEntityView>();
            objectResolver.Inject(view);
            view.SetEntity(actor);

            if (kind.Kind == EntityType.Character)
            {
                bool isPlayer = worldEntity.Has<GameFramework.World.Ownership>();
                if (isPlayer == false)
                {
                    LOPAIController aiController = root.AddComponent<LOPAIController>();
                    objectResolver.Inject(aiController);
                    aiController.SetEntity(actor);
                    aiController.SetBrain(objectResolver.Resolve<EnemyBrain>());
                }
            }
        }

        private void OnEntityDestroyed(EntityDestroyed entityDestroyed)
        {
            if (actorRegistry.TryGet(entityDestroyed.entityId, out var actor) == false)
            {
                return;
            }

            foreach (var cleanup in actor.transform.GetComponentsInChildren<ICleanup>(true))
            {
                cleanup.Cleanup();
            }

            actorRegistry.Remove(entityDestroyed.entityId);
            UnityEngine.Object.Destroy(actor.gameObject);
        }
    }
}
