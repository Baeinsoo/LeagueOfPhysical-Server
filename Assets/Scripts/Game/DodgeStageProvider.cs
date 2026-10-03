using System.Collections.Generic;

namespace LOP
{
    /// <summary>TbDodgeStage → 공용 DodgeStageTable(id 순). 클·서가 같은 표를 읽어야 HUD와 진행기가 같은 스테이지를 가리킨다.</summary>
    public class DodgeStageProvider
    {
        private readonly LOP.MasterData.LOPMasterData md;

        public DodgeStageProvider(LOP.MasterData.LOPMasterData md)
        {
            this.md = md;
        }

        public DodgeStageTable Get() => From(md.Tables.TbDodgeStage.DataList);

        /// <summary>표 행들 → 시간표(id 순). 시뮬도 같은 변환을 쓴다.</summary>
        public static DodgeStageTable From(IEnumerable<LOP.MasterData.DodgeStage> data)
        {
            var rows = new List<LOP.MasterData.DodgeStage>(data);
            rows.Sort((a, b) => a.Id.CompareTo(b.Id));
            var stages = new List<DodgeStage>(rows.Count);
            foreach (var r in rows)
            {
                var kinds = new DodgePatternKind[r.Kinds.Count];
                for (int i = 0; i < kinds.Length; i++) kinds[i] = (DodgePatternKind)r.Kinds[i];
                stages.Add(new DodgeStage(r.Name, r.DurationSeconds, kinds, r.BaseIntensity, r.Tighten, r.IntervalSeconds));
            }
            return new DodgeStageTable(stages);
        }
    }
}
