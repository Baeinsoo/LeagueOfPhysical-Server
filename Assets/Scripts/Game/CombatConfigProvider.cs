using VContainer;

namespace LOP
{
    /// <summary>Luban <c>TbCombatConfig</c>(전역 단일 행, id=1)을 LOP-Shared <see cref="CombatConfig"/>로 매핑하는
    /// 서버 side-local 어댑터. (Shared는 MasterData 패키지 비참조 → 여기서 변환. <see cref="AbilityDataProvider"/> 대칭.)</summary>
    public class CombatConfigProvider
    {
        [Inject]
        private LOP.MasterData.LOPMasterData md;

        public CombatConfig Get()
        {
            var r = md.Tables.TbCombatConfig.Get(1);
            return new CombatConfig(
                r.DodgeChanceMin, r.DodgeChanceMax,
                r.CritChanceMin, r.CritChanceMax,
                r.CritMultMin, r.CritMultMax);
        }
    }
}
