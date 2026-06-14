using GameFramework;
using LOP.Event.LOPGameEngine.Update;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class LOPAIController : MonoBehaviour, ICleanup
    {
        [Inject]
        private IGameEngine gameEngine;

        public LOPEntity entity { get; private set; }

        public void SetEntity(LOPEntity entity)
        {
            this.entity = entity;
        }

        public IBrain brain { get; private set; }

        public void SetBrain(IBrain brain)
        {
            this.brain = brain;
        }

        protected virtual void Start()
        {
            gameEngine.AddListener(this);
        }

        public void Cleanup()
        {
            gameEngine.RemoveListener(this);
            entity = null;
        }

        [GameEngineListen(typeof(Begin))]
        private void OnUpdateBegin()
        {
            brain.Think(entity, GameEngine.Time.deltaTime);
        }
    }
}
