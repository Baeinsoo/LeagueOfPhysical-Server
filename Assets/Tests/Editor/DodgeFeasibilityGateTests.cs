using System.Text;
using NUnit.Framework;

namespace LOP.Tests
{
    // 표를 고칠 때마다 "완벽하게 움직여도 못 피하는 판"이 생기지 않았는지 막는다(슬라이스 4a 스펙 §4).
    // 서든데스는 끝없이 어려워지는 구간이라 뺀다. 실패하면 시드·틱·스테이지·그 틱의 패턴을 찍는다(재현용).
    public class DodgeFeasibilityGateTests
    {
        public const int Seeds = 200;

        [Test]
        public void 스테이지_1에서_5까지는_완벽하면_다_피할_수_있다()
        {
            var c = DodgeSimTables.Config();
            var s = DodgeSimTables.Stages();
            long end = DodgeFeasibility.StagesEndTick(s, c);
            var fails = new StringBuilder();
            for (ulong seed = 1; seed <= Seeds; seed++)
            {
                var r = DodgeFeasibility.Run(seed, c, s, end);
                if (r.DeadTick >= 0)
                {
                    var at = s.At(r.DeadTick, 0, c);
                    fails.AppendLine($"seed={seed} tick={r.DeadTick} stage={at.Index + 1}({s[at.Index].Name})");
                }
            }
            Assert.IsEmpty(fails.ToString(), "못 피하는 판:\n" + fails);
        }
    }
}
