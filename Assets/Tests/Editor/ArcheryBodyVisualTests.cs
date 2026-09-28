using NUnit.Framework;

namespace LOP.Tests
{
    public class ArcheryBodyVisualTests
    {
        [Test]
        public void 한_발_승부는_치비_나머지는_기사()
        {
            Assert.AreEqual("Assets/Characters/Chibi/Chibi.prefab", ArcheryRuleSystem.BodyVisualFor(ArcheryCourseKind.ShootOff));
            Assert.AreEqual("Assets/Art/Characters/Knight/Knight.prefab", ArcheryRuleSystem.BodyVisualFor(ArcheryCourseKind.Range));
        }
    }
}
