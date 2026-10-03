using System.IO;

namespace LOP
{
    /// <summary>
    /// 시뮬용 표 읽기 — 에디터에서 마스터데이터 패키지의 .bytes를 직접 읽는다(LoadAsync는 UnityWebRequest라 EditMode에서 못 기다린다).
    /// 변환은 서버 공급자와 같은 함수다.
    /// </summary>
    public static class DodgeSimTables
    {
        private const string Dir = "Packages/com.baegames.lop.masterdata.server/Runtime.Generated/StreamingAssets/MasterData/";

        private static Luban.ByteBuf Read(string file) => new Luban.ByteBuf(File.ReadAllBytes(Path.GetFullPath(Dir + file)));

        public static DodgeConfig Config() =>
            DodgeConfigProvider.From(new LOP.MasterData.TbDodgeConfig(Read("tbdodgeconfig.bytes")).GetOrDefault(1));

        public static DodgeStageTable Stages() =>
            DodgeStageProvider.From(new LOP.MasterData.TbDodgeStage(Read("tbdodgestage.bytes")).DataList);
    }
}
