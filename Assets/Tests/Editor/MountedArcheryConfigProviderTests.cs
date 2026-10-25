using System.IO;
using System.Linq;
using Luban;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>제공자가 원 단계 표를 1단계부터 순서대로, 칸을 제자리에 옮겨 주는지 본다.</summary>
    public class MountedArcheryConfigProviderTests
    {
        private const string Folder = "Packages/com.baegames.lop.masterdata.server/Runtime.Generated/StreamingAssets/MasterData/";

        //  LoadAsync는 UnityWebRequest라 EditMode에서 못 기다린다 — 패키지의 .bytes를 직접 읽어 같은 표를 만든다.
        private static LOP.MasterData.LOPMasterData RealMasterData()
        {
            var tables = new LOP.MasterData.Tables(file =>
                new ByteBuf(File.ReadAllBytes(Path.GetFullPath(Folder + file + ".bytes"))));
            var md = new LOP.MasterData.LOPMasterData();
            typeof(LOP.MasterData.LOPMasterData).GetProperty(nameof(LOP.MasterData.LOPMasterData.Tables))
                .SetValue(md, tables);
            return md;
        }

        [Test]
        public void 단계_표는_순서대로_읽힌다()
        {
            var md = RealMasterData();

            //  표에 적힌 순서가 뒤집혀도 id 순으로 나가는지 보려고 목록을 일부러 거꾸로 만든다.
            var list = (System.Collections.Generic.List<LOP.MasterData.MountedArcheryZone>)
                typeof(LOP.MasterData.TbMountedArcheryZone)
                    .GetField("_dataList", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .GetValue(md.Tables.TbMountedArcheryZone);
            list.Reverse();
            Assert.AreEqual(4, list[0].Id, "목록을 뒤집지 못했다 — 이 시험이 정렬을 못 지킨다");

            var cfg = new MountedArcheryConfigProvider(md).Get();

            Assert.AreEqual(4, cfg.ZoneStages.Count);
            var expected = new[]
            {
                (10f, 23f, 60f, 4f), (10f, 23f, 30f, 7f), (10f, 23f, 12f, 10f), (0f, 21f, 0f, 15f),
            };
            for (int i = 0; i < expected.Length; i++)
            {
                var s = cfg.ZoneStages[i];
                Assert.AreEqual(expected[i], (s.restSeconds, s.shrinkSeconds, s.endRadius, s.damagePerSecond), $"단계 {i + 1}");
            }
            Assert.AreEqual(100f, cfg.ZoneInitialRadius);
            Assert.AreEqual(30f, cfg.ZoneFirstRevealSeconds);
            Assert.AreEqual(4f, cfg.AimAssistConeDeg);
            Assert.AreEqual(90f, cfg.AimAssistRange);
            Assert.AreEqual(0.6f, cfg.AimAssistFriction, 1e-5f);
            Assert.AreEqual(0.5f, cfg.AimAssistTrack, 1e-5f);
        }

        [Test]
        public void 봇_수치는_표에서_읽힌다()
        {
            var cfg = new MountedArcheryConfigProvider(RealMasterData()).Get();

            Assert.AreEqual(2.5f, cfg.BotAimErrorDeg, 1e-5f);
            Assert.AreEqual(0.2f, cfg.BotReactionMinSeconds, 1e-5f);
            Assert.AreEqual(0.4f, cfg.BotReactionMaxSeconds, 1e-5f);
            Assert.AreEqual(60f, cfg.BotEngageRange, 1e-5f);
        }

        [Test]
        public void 반응_지연_최소가_최대보다_크면_맞바꿔_만든다()
        {
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("맞바꿨다"));
            var c = new MountedArcheryConfig(
                150f, 10f, 30f, 2.5f, default, 100, 34, 0.5f, 100f, 30f, null, 4f, 90f, 0.6f, 0.5f, 15f,
                botAimErrorDeg: 2.5f, botReactionMinSeconds: 0.5f, botReactionMaxSeconds: 0.2f, botEngageRange: 60f);
            Assert.AreEqual(0.2f, c.BotReactionMinSeconds);
            Assert.AreEqual(0.5f, c.BotReactionMaxSeconds);
        }
    }
}
