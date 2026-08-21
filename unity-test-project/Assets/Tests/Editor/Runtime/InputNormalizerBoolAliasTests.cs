using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.TestProject.Runtime
{
    [TestFixture]
    public class InputNormalizerBoolAliasTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void NormalizeValue_LowerTrue_ReturnsTrue() =>
            Assert.That(InputNormalizer.NormalizeValue("true"), Is.EqualTo("True"));

        [Test]
        public void NormalizeValue_UppercaseTRUE_ReturnsTrue() =>
            Assert.That(InputNormalizer.NormalizeValue("TRUE"), Is.EqualTo("True"));

        [Test]
        public void NormalizeValue_LowerFalse_ReturnsFalse() =>
            Assert.That(InputNormalizer.NormalizeValue("false"), Is.EqualTo("False"));

        [Test]
        public void NormalizeValue_Yes_ReturnsTrue() =>
            Assert.That(InputNormalizer.NormalizeValue("yes"), Is.EqualTo("True"));

        [Test]
        public void NormalizeValue_No_ReturnsFalse() =>
            Assert.That(InputNormalizer.NormalizeValue("no"), Is.EqualTo("False"));

        [Test]
        public void NormalizeValue_On_ReturnsTrue() =>
            Assert.That(InputNormalizer.NormalizeValue("on"), Is.EqualTo("True"));

        [Test]
        public void NormalizeValue_Off_ReturnsFalse() =>
            Assert.That(InputNormalizer.NormalizeValue("off"), Is.EqualTo("False"));
    }
}
