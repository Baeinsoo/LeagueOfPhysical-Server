namespace LOP
{
    public interface IBrain
    {
        void Think(GameFramework.World.Entity worldEntity, double deltaTime);
    }
}
