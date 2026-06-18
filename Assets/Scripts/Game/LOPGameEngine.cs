using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameFramework;
using VContainer;
using LOP.Event.LOPGameEngine.Update;

namespace LOP
{
    [DIMonoBehaviour]
    public class LOPGameEngine : GameEngineBase
    {
        [Inject]
        private ISessionManager sessionManager;

        [Inject]
        private IActionManager actionManager;

        [Inject]
        private IMovementManager movementManager;

        [Inject] private GameFramework.World.WorldEventBuffer worldEventBuffer;
        [Inject] private GameFramework.World.WorldEventApplicator worldEventApplicator;
        [Inject] private WireBroadcaster wireBroadcaster;
        [Inject] private GameFramework.World.EntityRegistry entityRegistry;

        public new LOPEntityManager entityManager => base.entityManager as LOPEntityManager;

        public override void UpdateEngine()
        {
            BeginUpdate();

            ProcessNetworkMessage();

            ProcessInput();

            UpdateEntity();

            UpdateAI();

            SimulatePhysics();

            ProcessEvent();

            EndUpdate();
        }

        private void BeginUpdate()
        {
            DispatchEvent<Begin>();
        }

        private void ProcessNetworkMessage()
        {
        }

        private void ProcessInput()
        {
            IEnumerable<LOPEntity> LOPEntities = new List<LOPEntity>(entityManager.GetEntities<LOPEntity>());

            foreach (var entity in LOPEntities)
            {
                var input = entity.GetEntityComponent<EntityInputComponent>()?.GetInput(GameEngine.Time.tick);
                if (input == null)
                {
                    continue;
                }

                EntityTransform entityTransform = MapperConfig.mapper.Map<EntityTransform>(input.EntityTransform);

                movementManager.ProcessInput(entity, entityTransform, input.PlayerInput.Horizontal, input.PlayerInput.Vertical, input.PlayerInput.Jump);

                if (string.IsNullOrEmpty(input.PlayerInput.ActionCode) == false)
                {
                    actionManager.TryStartAction(entity, input.PlayerInput.ActionCode);
                }

                InputSequenceToC inputSequnceToC = new InputSequenceToC();
                inputSequnceToC.EntityId = entity.entityId;
                inputSequnceToC.InputSequence = new InputSequence
                {
                    Tick = GameEngine.Time.tick,
                    Sequence = input.PlayerInput.SequenceNumber,
                };

                string userId = entityManager.GetUserIdByEntityId(entity.entityId);
                ISession session = sessionManager.GetSessionByUserId(userId);
                session.Send(inputSequnceToC);
            }
        }

        private void UpdateEntity()
        {
            DispatchEvent<BeforeEntityUpdate>();

            entityManager.UpdateEntities();

            DispatchEvent<AfterEntityUpdate>();
        }

        private void UpdateAI()
        {
        }

        private void SimulatePhysics()
        {
            DispatchEvent<BeforePhysicsSimulation>();

            Physics.Simulate((float)tickUpdater.interval);

            DispatchEvent<AfterPhysicsSimulation>();
        }

        private void ProcessEvent()
        {
            // --- World Core — 슬라이스 3: 이벤트 버퍼 드레인 ---
            var snapshot = worldEventBuffer.Snapshot;
            if (snapshot.Count == 0) return;

            worldEventApplicator.Apply(snapshot);
            wireBroadcaster.Broadcast(snapshot);
            worldEventBuffer.Clear();
            // --- end World Core slice 3 ---
        }

        private void EndUpdate()
        {
            DispatchEvent<End>();

            EntitySnap[] allEntitySnaps = entityManager.GetAllEntitySnaps();

            foreach (var session in sessionManager.GetAllSessions())
            {
                EntitySnapsToC entitySnapsToC = new EntitySnapsToC();
                entitySnapsToC.Tick = tickUpdater.tick;
                entitySnapsToC.EntitySnaps.AddRange(allEntitySnaps);

                session.Send(entitySnapsToC);
            }

            foreach (var session in sessionManager.GetAllSessions())
            {
                LOPEntity entity = entityManager.GetEntityByUserId<LOPEntity>(session.userId);

                // HP/MP는 World.Health/World.Mana(코어)에서 읽는다. Exp/Level/StatPoints는 각자 이행 전까지 legacy 컴포넌트 유지.
                GameFramework.World.Entity worldEntity = entityRegistry.Get(entity.entityId);
                GameFramework.World.Health health = worldEntity?.Get<GameFramework.World.Health>();
                if (health == null)
                {
                    Debug.LogWarning($"[World] UserEntitySnap: Health not found for entity {entity.entityId}");
                }

                UserEntitySnapToC entitySnapsToC = new UserEntitySnapToC();
                entitySnapsToC.CurrentHP = health?.Current ?? 0;
                entitySnapsToC.MaxHP = health?.Max ?? 0;
                GameFramework.World.Mana mana = worldEntity?.Get<GameFramework.World.Mana>();
                if (mana == null)
                {
                    Debug.LogWarning($"[World] UserEntitySnap: Mana not found for entity {entity.entityId}");
                }
                entitySnapsToC.CurrentMP = mana?.Current ?? 0;
                entitySnapsToC.MaxMP = mana?.Max ?? 0;
                entitySnapsToC.CurrentExp = entity.GetEntityComponent<LevelComponent>().currentExp;
                entitySnapsToC.Level = entity.GetEntityComponent<LevelComponent>().level;
                entitySnapsToC.StatPoints = entity.GetEntityComponent<PlayerComponent>().statPoints;

                foreach (var action in entity.GetComponents<Action>())
                {
                    entitySnapsToC.ActionDatas.Add(new ActionData
                    {
                        ActionCode = action.actionCode,
                        IsActive = action.isActive,
                        RemainCooldown = action.remainCooldown,
                        StartTick = action.startTick,
                    });
                }

                session.Send(entitySnapsToC);
            }

            entityManager.DestroyMarkedEntities();
        }
    }
}
