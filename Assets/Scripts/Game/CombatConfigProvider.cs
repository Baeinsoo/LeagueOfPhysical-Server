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
            // 없으면 Luban의 애매한 KeyNotFoundException 대신 원인을 짚어 크게 실패
            var r = md.Tables.TbCombatConfig.GetOrDefault(1);
            if (r == null)
            {
                throw new System.InvalidOperationException(
                    "TbCombatConfig id=1 행을 찾을 수 없음 — MasterData 미로드 또는 CombatConfig 데이터 누락");
            }
            return new CombatConfig(
                r.DodgeChanceMin, r.DodgeChanceMax,
                r.CritChanceMin, r.CritChanceMax,
                r.CritMultMin, r.CritMultMax);
        }
    }
}
