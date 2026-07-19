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
    /// </summary>
    public class EntityBinder : IGameMessageHandler
    {
        [Inject] private IObjectResolver objectResolver;
        [Inject] private ISubscriber<EntityCreated> entityCreatedSubscriber;
        [Inject] private ISubscriber<EntityDestroyed> entityDestroyedSubscriber;
        [Inject] private GameFramework.World.EntityRegistry entityRegistry;
        [Inject] private ActorRegistry actorRegistry;

        private IDisposable subscriptions;

        public void Initialize()
        {
            var bag = DisposableBag.CreateBuilder();
            entityCreatedSubscriber.Subscribe(OnEntityCreated).AddTo(bag);
            entityDestroyedSubscriber.Subscribe(OnEntityDestroyed).AddTo(bag);
            subscriptions = bag.Build();
        }

        public void Dispose()
        {
            subscriptions?.Dispose();
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

            PhysicsFollower physicsFollower = root.AddComponent<PhysicsFollower>();
            objectResolver.Inject(physicsFollower);
            physicsFollower.Initialize(worldEntity, true, isItem);
            worldEntity.Add(new PhysicsBody(physicsFollower.entityRigidbody, (CapsuleCollider)physicsFollower.entityColliders[0]));

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
