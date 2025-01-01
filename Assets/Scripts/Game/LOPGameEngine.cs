using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using GameFramework;

namespace LOP
{
    public class LOPGameEngine : GameEngineBase
    {
        public static readonly IMessageBrokerExtended messageBroker = new MessageBrokerExtended();

        public override void UpdateEngine()
        {
            BeginUpdate();

            ProcessInput();

            UpdateEntity();

            UpdateAI();

            SimulatePhysics();

            ProcessEvent();

            EndUpdate();
        }

        private void BeginUpdate()
        {
            messageBroker.Publish(new Message.LOPGameEngine.Update.Begin());
        }

        private void ProcessInput()
        {
            foreach (var entity in entityManager.GetEntities())
            {

            }
        }

        private void UpdateEntity()
        {
            messageBroker.Publish(new Message.LOPGameEngine.Update.BeforeEntityUpdate());

            entityManager.UpdateEntities();

            messageBroker.Publish(new Message.LOPGameEngine.Update.AfterEntityUpdate());
        }

        private void UpdateAI()
        {

        }

        private void SimulatePhysics()
        {
            messageBroker.Publish(new Message.LOPGameEngine.Update.BeforePhysicsSimulation());

            Physics.Simulate((float)tickUpdater.interval);

            messageBroker.Publish(new Message.LOPGameEngine.Update.AfterPhysicsSimulation());
        }

        private void ProcessEvent()
        {

        }

        private void EndUpdate()
        {
            messageBroker.Publish(new Message.LOPGameEngine.Update.End());
        }
    }
}
