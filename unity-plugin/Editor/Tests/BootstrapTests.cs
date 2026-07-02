// TDD: Bootstrap.Init (M7, ROI reliability sprint).
// CommandRegistry no longer eagerly populates itself via a static constructor — Bootstrap.Init
// (an [InitializeOnLoadMethod]) now owns that responsibility. Behavior is covered end-to-end by
// CommandRegistryCompletenessTests (which only passes if something already called InitDefaults()
// by the time tests run — that "something" is Bootstrap.Init firing at domain load). This file
// adds a source-text assertion verifying the wiring itself, since [InitializeOnLoadMethod] fires
// once per domain reload and cannot be re-invoked/observed directly from a test.
using System.IO;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class BootstrapTests
    {
        [Test]
        public void Init_CallsClearThenInitDefaults()
        {
            var src = Path.GetFullPath(
                Path.Combine("Packages", "com.unity-mcp.editor", "Editor", "Bootstrap.cs"));
            if (!File.Exists(src))
            {
                Assert.Ignore($"Bootstrap.cs not found at {src} — skip in CI");
                return;
            }
            var code = File.ReadAllText(src);
            var clearIndex = code.IndexOf("CommandRegistry.Clear()");
            var initIndex = code.IndexOf("CommandRegistry.InitDefaults()");
            Assert.GreaterOrEqual(clearIndex, 0, "Init must call CommandRegistry.Clear()");
            Assert.GreaterOrEqual(initIndex, 0, "Init must call CommandRegistry.InitDefaults()");
            Assert.Less(clearIndex, initIndex, "Clear() must run before InitDefaults() to avoid duplicate-registration warnings");
        }

        [Test]
        public void Init_IsMarkedInitializeOnLoadMethod()
        {
            var src = Path.GetFullPath(
                Path.Combine("Packages", "com.unity-mcp.editor", "Editor", "Bootstrap.cs"));
            if (!File.Exists(src))
            {
                Assert.Ignore($"Bootstrap.cs not found at {src} — skip in CI");
                return;
            }
            var code = File.ReadAllText(src);
            StringAssert.Contains("[InitializeOnLoadMethod]", code,
                "Init must be wired via [InitializeOnLoadMethod] to run once per domain reload");
        }

        [Test]
        public void Init_WrapsBodyInDelayCall()
        {
            var src = Path.GetFullPath(
                Path.Combine("Packages", "com.unity-mcp.editor", "Editor", "Bootstrap.cs"));
            if (!File.Exists(src))
            {
                Assert.Ignore($"Bootstrap.cs not found at {src} — skip in CI");
                return;
            }
            var code = File.ReadAllText(src);
            var delayCallIndex = code.IndexOf("EditorApplication.delayCall");
            var clearIndex = code.IndexOf("CommandRegistry.Clear()");
            Assert.GreaterOrEqual(delayCallIndex, 0,
                "Init must wrap its body in EditorApplication.delayCall so it runs after all " +
                "[InitializeOnLoad] types (incl. plugin assemblies) have registered");
            Assert.Less(delayCallIndex, clearIndex,
                "delayCall wrapping must enclose the Clear()/InitDefaults() calls");
        }

        [Test]
        public void CommandRegistry_HasNoStaticConstructorCall_ToInitDefaults()
        {
            var src = Path.GetFullPath(
                Path.Combine("Packages", "com.unity-mcp.editor", "Editor", "CommandRegistry.cs"));
            if (!File.Exists(src))
            {
                Assert.Ignore($"CommandRegistry.cs not found at {src} — skip in CI");
                return;
            }
            var code = File.ReadAllText(src);
            StringAssert.DoesNotContain("static CommandRegistry()", code,
                "CommandRegistry must not eagerly self-populate via a static ctor (M7) — Bootstrap.Init owns that now");
        }
    }
}
