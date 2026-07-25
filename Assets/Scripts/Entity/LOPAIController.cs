using GameFramework;
using GameFramework.Runner;
using LOP.Event.LOPRunner.Update;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class LOPAIController : MonoBehaviour, ICleanup, ITickSystem
    {
        [Inject]
        private IRunner runner;

        [Inject]
        private GameFramework.World.EntityRegistry entityRegistry;

        public LOPActor actor { get; private set; }

        public void SetEntity(LOPActor actor)
        {
            this.actor = actor;
        }

        public IBrain brain { get; private set; }

        public void SetBrain(IBrain brain)
        {
            this.brain = brain;
        }

        protected virtual void Start()
        {
            runner.RegisterSystem<Begin>(this);
        }

        public void Cleanup()
        {
            runner.UnregisterSystem(this);
            actor = null;
        }

        public void Tick(long tick, float deltaTime)
        {
            var worldEntity = entityRegistry.Get(actor.entityId);
            if (worldEntity != null)
            {
                brain.Think(worldEntity, deltaTime);
            }
        }
    }
}
