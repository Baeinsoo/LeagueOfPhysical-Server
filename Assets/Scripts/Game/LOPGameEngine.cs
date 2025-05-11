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
            foreach (var entity in entityManager.GetEntities<LOPEntity>())
            {
                var input = entity.GetComponent<EntityInputComponent>().GetNextInput(GameEngine.Time.tick);
                if (input == null)
                {
                    continue;
                }

                Vector3 direction = new Vector3(input.horizontal, 0, input.vertical).normalized;

                //  Move & Rotate
                if (direction.normalized.sqrMagnitude > 0)
                {
                    var velocity = direction.normalized * 5;

                    entity.velocity = new Vector3(velocity.x, entity.velocity.y, velocity.z);

                    float myFloat = 0;
                    var angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                    var smooth = Mathf.SmoothDampAngle(entity.rotation.y, angle, ref myFloat, 0.01f);

                    entity.rotation = new Vector3(0, smooth, 0);
                }

                //  Jump
                if (input.jump)
                {
                    var normalizedPower = 1;
                    var dir = Vector3.up;
                    var JumpPowerFactor = 10;

                    entity.visualRigidbody.AddForce(normalizedPower * dir.normalized * JumpPowerFactor, ForceMode.Impulse);
                }

                var inputSequnceToC = new InputSequnceToC();
                inputSequnceToC.InputSequnce = new InputSequnce();
                inputSequnceToC.EntityId = entity.entityId;
                inputSequnceToC.InputSequnce.Tick = GameEngine.Time.tick;
                inputSequnceToC.InputSequnce.Sequence = input.sequenceNumber;

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
