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

        public LOPActor actor { get; private set; }

        public void SetEntity(LOPActor actor)
        {
            this.actor = actor;
        }

        public IBrain<LOPActor> brain { get; private set; }

        public void SetBrain(IBrain<LOPActor> brain)
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
            brain.Think(actor, Runner.Time.deltaTime);
        }
    }
}
