using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameFramework;
using VContainer;
using LOP.Event.LOPGameEngine.Update;

namespace LOP
{
    public class LOPGameEngine : GameEngineBase
    {
        [Inject]
        private ISessionManager sessionManager;

        [Inject]
        private IActionManager actionManager;

        [Inject]
        private IMovementManager movementManager;

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
                var input = entity.GetComponent<EntityInputComponent>().GetInput(GameEngine.Time.tick);
                if (input == null)
                {
                    continue;
                }

                EntityTransform entityTransform = MapperConfig.mapper.Map<EntityTransform>(input.EntityTransform);

                movementManager.ProcessInput(entity, entityTransform, input.PlayerInput.Horizontal, input.PlayerInput.Vertical, input.PlayerInput.Jump);

                if (string.IsNullOrEmpty(input.PlayerInput.ActionCode) == false)
                {
                    actionManager.TryExecuteAction(entity, input.PlayerInput.ActionCode);
                }
                
                var inputSequnceToC = new InputSequenceToC();
                inputSequnceToC.InputSequence = new InputSequence();
                inputSequnceToC.EntityId = entity.entityId;
                inputSequnceToC.InputSequence.Tick = GameEngine.Time.tick;
                inputSequnceToC.InputSequence.Sequence = input.PlayerInput.SequenceNumber;

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
        }
    }
}
