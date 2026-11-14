using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class SettingsPageFactoryTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // Task 3.1 (sub-task 2.3): a true behavioral reproduction of the
        // [InitializeOnLoad] ordering race is not possible from EditMode — every
        // static ctor across every loaded assembly has already run before any
        // NUnit test method executes, so ChatHostBridge.Current is never null
        // "before ChatHostBridgeImpl registers" from inside a test. This is a
        // structural guard instead: the childCount==0 fallback to
        // InvokeConnectionViaReflection in BuildChatPage must stay wired, since
        // InvokeConnectionViaReflection itself is intentionally left untouched.
        [Test]
        public void BuildChatPage_StillHasReflectionFallbackForRegistrationRace()
        {
            var src = ReadRequiredPackageSource(typeof(SettingsPageFactory), "Editor/SettingsPageFactory.cs");
            StringAssert.Contains("ChatSettingsHook.InvokeConnection(scroll);", src);
            StringAssert.Contains("scroll.childCount == 0", src);
            StringAssert.Contains("ChatSettingsHook.InvokeConnectionViaReflection(scroll);", src);
        }
    }
}
