using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using GameFramework;

namespace LOP
{
    public class LOPGameEngine : GameEngineBase
    {
        public static readonly IMessageBrokerExtended updateEvents = new MessageBrokerExtended();

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
            updateEvents.Publish(new Event.LOPGameEngine.Update.Begin());
        }

        private void ProcessNetworkMessage()
        {
        }

        private void ProcessInput()
        {
        }

        private void UpdateEntity()
        {
            updateEvents.Publish(new Event.LOPGameEngine.Update.BeforeEntityUpdate());

            entityManager.UpdateEntities();

            updateEvents.Publish(new Event.LOPGameEngine.Update.AfterEntityUpdate());
        }

        private void UpdateAI()
        {
        }

        private void SimulatePhysics()
        {
            updateEvents.Publish(new Event.LOPGameEngine.Update.BeforePhysicsSimulation());

            Physics.Simulate((float)tickUpdater.interval);

            updateEvents.Publish(new Event.LOPGameEngine.Update.AfterPhysicsSimulation());
        }

        private void ProcessEvent()
        {
        }

        private void EndUpdate()
        {
            updateEvents.Publish(new Event.LOPGameEngine.Update.End());
        }
    }
}
