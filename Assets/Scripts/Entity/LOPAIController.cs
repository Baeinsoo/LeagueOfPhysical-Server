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

        public LOPActor entity { get; private set; }

        public void SetEntity(LOPActor entity)
        {
            this.entity = entity;
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
            entity = null;
        }

        [RunnerListen(typeof(Begin))]
        private void OnUpdateBegin()
        {
            brain.Think(entity, Runner.Time.deltaTime);
        }
    }
}
