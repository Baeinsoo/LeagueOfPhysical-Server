using GameFramework;
using LOP.Event.LOPGameEngine.Update;
using VContainer;

namespace LOP
{
    public class LOPAIController : MonoEntityController<LOPEntity>
    {
        [Inject]
        private IGameEngine gameEngine;

        public IBrain brain { get; private set; }

        public void SetBrain(IBrain brain)
        {
            this.brain = brain;
        }

        protected virtual void Start()
        {
            gameEngine.AddListener(this);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            gameEngine.RemoveListener(this);
        }

        [GameEngineListen(typeof(Begin))]
        private void OnUpdateBegin()
        {
            brain.Think(entity, GameEngine.Time.deltaTime);
        }
    }
}
