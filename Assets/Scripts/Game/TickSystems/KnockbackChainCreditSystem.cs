namespace LOP
{
    /// <summary>
    /// 연쇄 크레딧(서버 End 페이즈, 맞음 판정 바로 앞). 부딪힘은 world.Tick 안에서 이미 일어났으므로
    /// 이번 틱 탄 맞음이 출처 몸의 크레딧을 덮기 전에 물려받아야 한다.
    /// </summary>
    public sealed class KnockbackChainCreditSystem : GameFramework.Runner.ITickSystem
    {
        private readonly KnockbackWorld world;
        private readonly KnockbackMatchState state;

        public KnockbackChainCreditSystem(KnockbackWorld world, KnockbackMatchState state)
        {
            this.world = world;
            this.state = state;
        }

        public void Tick(long tick, float deltaTime)
        {
            if (world.ChainHitsThisTick.Count == 0)
            {
                return;
            }
            state.Credit.ApplyChains(world.ChainHitsThisTick, tick, state.Team);
        }
    }
}
