namespace LOP
{
    /// <summary>
    /// 삼색 그랩의 몸 — 보통 캐릭터 몸을 만든 뒤 걷는 속도만 이 모드 값(<see cref="LOP.TriGrab.TriGrabConfig.MoveSpeed"/>)으로 바꾼다.
    /// 캐릭터 마스터데이터의 속도는 모든 모드가 같이 쓰므로 여기서만 덮는다. 클라 예측이 같은 값을 써야 보정 떨림이 없다.
    /// </summary>
    public sealed class TriGrabCharacterCreator : ICharacterCreator
    {
        private readonly ICharacterCreator inner;
        private readonly GameFramework.World.EntityRegistry registry;

        public TriGrabCharacterCreator(ICharacterCreator inner, GameFramework.World.EntityRegistry registry)
        {
            this.inner = inner;
            this.registry = registry;
        }

        public void Create(CharacterCreationData data)
        {
            inner.Create(data);
            var stats = registry.Get(data.entityId).Get<GameFramework.World.Stats>();
            stats.BaseStats[(int)GameFramework.World.EntityStatType.MoveSpeed] = LOP.TriGrab.TriGrabConfig.MoveSpeed;
        }
    }
}
