// TDD — Phase 1.2a: NavTarget value struct tests.
using NUnit.Framework;
using UnityMCP.Editor.Chat;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class NavTargetTests : UnityMcpTestBase
    {
        [Test]
        public void DefaultConstructor_IsEmpty()
            => Assert.IsTrue(default(NavTarget).IsEmpty);

        [Test]
        public void NullRef_IsEmpty()
            => Assert.IsTrue(new NavTarget("script", null).IsEmpty);

        [Test]
        public void EmptyKindKey_IsEmpty()
            => Assert.IsTrue(new NavTarget("", "Assets/Foo.cs").IsEmpty);

        [Test]
        public void ValidArgs_FieldsSet()
        {
            var t = new NavTarget("script", "Assets/Foo.cs");
            Assert.AreEqual("script", t.KindKey);
            Assert.AreEqual("Assets/Foo.cs", t.Reference);
            Assert.AreEqual(0, t.Line);
        }

        [Test]
        public void WithLine_LineSet()
            => Assert.AreEqual(42, new NavTarget("script", "Assets/Foo.cs", 42).Line);
    }
}
