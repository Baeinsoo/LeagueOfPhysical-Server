namespace LOP
{
    /// <summary>구 LOPRunner.ProcessDeaths 이동. 확정된 이벤트 버퍼에서 사망 cascade(디스폰+경험치)를 resolve 단계(egress 전)에서 처리.</summary>
    public class DeathResolveSystem : GameFramework.ITickSystem
    {
        private readonly GameFramework.World.WorldEventBuffer worldEventBuffer;
        private readonly DeathCascadeSystem deathCascade;

        public DeathResolveSystem(GameFramework.World.WorldEventBuffer worldEventBuffer, DeathCascadeSystem deathCascade)
        {
            this.worldEventBuffer = worldEventBuffer;
            this.deathCascade = deathCascade;
        }

        public void Tick(long tick, float deltaTime)
        {
            var snapshot = worldEventBuffer.Snapshot;
            if (snapshot.Count == 0) return;
            deathCascade.Resolve(snapshot);
        }
    }
}
