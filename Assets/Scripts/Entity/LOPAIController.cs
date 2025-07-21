using GameFramework;
using LOP.Event.LOPGameEngine.Update;

namespace LOP
{
    public class LOPAIController : MonoEntityController<LOPEntity>
    {
        public IBrain brain { get; private set; }

        public void SetBrain(IBrain brain)
        {
            this.brain = brain;
        }

        protected virtual void Awake()
        {
            SceneLifetimeScope.Resolve<IGameEngine>().AddListener(this);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            SceneLifetimeScope.Resolve<IGameEngine>().RemoveListener(this);
        }

        [GameEngineListen(typeof(Begin))]
        private void OnUpdateBegin()
        {
            brain.Think(entity, GameEngine.Time.deltaTime);
        }
    }
}
