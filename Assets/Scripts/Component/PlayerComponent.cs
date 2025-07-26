
namespace LOP
{
    public class PlayerComponent : LOPComponent
    {
        public string userId { get; private set; }

        public void Initialize(string userId)
        {
            this.userId = userId;
        }
    }
}
