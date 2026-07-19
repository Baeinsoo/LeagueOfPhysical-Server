using GameFramework;
using LOP.Event.LOPRunner.Update;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class LOPAIController : MonoBehaviour, ICleanup
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
            runner.AddListener(this);
        }

        public void Cleanup()
        {
            runner.RemoveListener(this);
            actor = null;
        }

        [RunnerListen(typeof(Begin))]
        private void OnUpdateBegin()
        {
            var worldEntity = entityRegistry.Get(actor.entityId);
            if (worldEntity != null)
            {
                brain.Think(worldEntity, Runner.Time.deltaTime);
            }
        }
    }
}
