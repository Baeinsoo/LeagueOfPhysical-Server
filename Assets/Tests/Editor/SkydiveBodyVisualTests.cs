using NUnit.Framework;

namespace LOP.Tests
{
    public class SkydiveBodyVisualTests
    {
        [Test]
        public void 스카이다이브는_치비()
        {
            Assert.AreEqual("Assets/Characters/Chibi/Chibi.prefab", SkydiveRuleSystem.BodyVisualId);
        }
    }
}
