namespace LOP
{
    /// <summary>
    /// 별만 결승인 맵에서 별을 놓치고 구름 아래로 빠지면 출구 아래에서 다시 떨어뜨린다(사용자 10-07, 안 A).
    /// <b>서버에서만 돈다</b> — 레이저 부활과 같은 길(클라는 순간이동을 예측하지 않고 스냅샷으로 받는다).
    /// </summary>
    public class SkydiveRetrySystem : GameFramework.Runner.ITickSystem
    {
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly RetryField field;
        private readonly SkydiveConfig config;
        private int spreadOrder;

        public SkydiveRetrySystem(GameFramework.World.EntityRegistry entityRegistry, RetryField field, SkydiveConfig config)
        {
            this.entityRegistry = entityRegistry;
            this.field = field;
            this.config = config;
        }

        public void Tick(long tick, float deltaTime)
        {
            //  맵이 이 시스템보다 늦게 뜬다 — 지금 읽는다. 별 없는 맵은 표식도 없어 아무 일도 안 한다.
            if (field.All.Count == 0)
            {
                return;
            }
            foreach (GameFramework.World.Entity entity in entityRegistry.All)
            {
                //  SkydiveWorld.CollectDivers와 같은 기준.
                if (entity.Get<EntityKind>()?.Kind != EntityType.Character || entity.Has<GameFramework.World.Simulated>() == false)
                {
                    continue;
                }
                if (SkydiveRetry.ShouldRetry(entity, field, out UnityEngine.Vector3 point))
                {
                    SkydiveRespawn.ApplyAt(entity, point, config.StaminaMax, ref spreadOrder);
                }
            }
        }
    }
}
