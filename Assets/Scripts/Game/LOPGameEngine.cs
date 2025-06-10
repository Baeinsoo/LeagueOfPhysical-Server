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

                movementManager.ProcessInput(entity, input.horizontal, input.vertical, input.jump);

                if (string.IsNullOrEmpty(input.actionCode) == false)
                {
                    actionManager.TryExecuteAction(entity, input.actionCode);
                }
                
                var inputSequnceToC = new InputSequenceToC();
                inputSequnceToC.InputSequence = new InputSequence();
                inputSequnceToC.EntityId = entity.entityId;
                inputSequnceToC.InputSequence.Tick = GameEngine.Time.tick;
                inputSequnceToC.InputSequence.Sequence = input.sequenceNumber;

                string userId = entityManager.GetUserIdByEntityId(entity.entityId);
                ISession session = sessionManager.GetSessionByUserId(userId);
                session.Send(inputSequnceToC);
            }
        }

        private void SpawnEntity(LOPEntity owner)
        {
            Vector3 offset = new Vector3(Random.Range(1, 5), Random.Range(1, 3), Random.Range(1, 5));
            Vector3 position = owner.position + offset;

            LOPEntityCreationData data = new LOPEntityCreationData
            {
                userId = "",
                entityId = entityManager.GenerateEntityId(),
                visualId = "Cube",
                position = position,
                rotation = Vector3.zero,
                velocity = Vector3.zero,
            };

            LOPEntity entity = entityManager.CreateEntity<LOPEntity, LOPEntityCreationData>(data);

            EntitySpawnToC entitySpawnToC = new EntitySpawnToC
            {
                EntityCreationData = new EntityCreationData
                {
                    LopEntityCreationData = new global::LOPEntityCreationData
                    {
                        BaseEntityCreationData = new BaseEntityCreationData
                        {
                            EntityId = entity.entityId,
                            Position = MapperConfig.mapper.Map<ProtoVector3>(entity.position),
                            Rotation = MapperConfig.mapper.Map<ProtoVector3>(entity.rotation),
                            Velocity = MapperConfig.mapper.Map<ProtoVector3>(entity.velocity),
                        },
                        VisualId = entity.visualId,
                    }
                },
            };

            string userId = entityManager.GetUserIdByEntityId(owner.entityId);
            ISession session = sessionManager.GetSessionByUserId(userId);
            session.Send(entitySpawnToC);
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
