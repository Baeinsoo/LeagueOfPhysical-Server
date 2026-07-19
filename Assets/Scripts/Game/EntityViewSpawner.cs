using GameFramework;
using LOP.Event.Entity;
using MessagePipe;
using System;
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 서버 뷰 스포너 — 엔티티 수명 신호(<see cref="EntityCreated"/>)에 반응해 서버측 Unity 뷰를 부착한다:
    /// 물리 팔로워 + PhysicsBody + 테스트렌더 뷰(+ 비-플레이어에 AIController). Creator는 데이터+앵커만.
    /// (서버는 장식 뷰·보간 없음 — 권위 시뮬.)
    /// </summary>
    public class EntityViewSpawner : IGameMessageHandler
    {
        [Inject] private IObjectResolver objectResolver;
        [Inject] private ISubscriber<EntityCreated> entityCreatedSubscriber;
        [Inject] private GameFramework.World.EntityRegistry entityRegistry;

        private IDisposable subscription;

        public void Initialize()
        {
            subscription = entityCreatedSubscriber.Subscribe(OnEntityCreated);
        }

        public void Dispose()
        {
            subscription?.Dispose();
        }

        private void OnEntityCreated(EntityCreated entityCreated)
        {
            LOPActor actor = entityCreated.actor;
            if (actor == null)
            {
                return;
            }
            GameFramework.World.Entity worldEntity = entityRegistry.Get(actor.entityId);
            if (worldEntity == null)
            {
                return;
            }
            EntityKind kind = worldEntity.Get<EntityKind>();
            if (kind == null)
            {
                return;
            }

            GameObject root = actor.gameObject;
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
    }
}
