using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal class ShellHelperTests
    {
        [TearDown]
        public void TearDown() => ShellHelper.ResetForTests();

        // ── ShellQuoteSingle ────────────────────────────────────────────────

        [Test]
        public void ShellQuoteSingle_Plain_WrapsInSingleQuotes() =>
            Assert.AreEqual("'hello'", ShellHelper.ShellQuoteSingle("hello"));

        [Test]
        public void ShellQuoteSingle_ContainsSingleQuote_EscapedAsBackslashSequence() =>
            Assert.AreEqual("'it'\\''s'", ShellHelper.ShellQuoteSingle("it's"));

        [Test]
        public void ShellQuoteSingle_InjectionPayload_NoShellMetaCharsEscape()
        {
            const string payload = "x'; rm -rf ~; '";
            var quoted = ShellHelper.ShellQuoteSingle(payload);
            Assert.AreEqual("'x'\\''; rm -rf ~; '\\'''", quoted);
        }

        [Test]
        public void ShellQuoteSingle_Empty_ReturnsTwoSingleQuotes() =>
            Assert.AreEqual("''", ShellHelper.ShellQuoteSingle(""));

        // ── BuildLoginShellArgs ─────────────────────────────────────────────

        [Test]
        public void BuildLoginShellArgs_MacOs_ContainsLicFlag()
        {
            var args = ShellHelper.BuildLoginShellArgs("echo hi", "arg1");
            StringAssert.Contains("-lic", args);
        }

        [Test]
        public void BuildLoginShellArgs_ScriptAndArg_BothSingleQuoted()
        {
            var args = ShellHelper.BuildLoginShellArgs("echo hi", "myarg");
            StringAssert.Contains("'echo hi'", args);
            StringAssert.Contains("'myarg'", args);
        }

        [Test]
        public void BuildLoginShellArgs_InjectionArg_CannotBreakOutOfQuotes()
        {
            const string evil = "x'; rm -rf ~; '";
            var args   = ShellHelper.BuildLoginShellArgs("echo", evil);
            var quoted = ShellHelper.ShellQuoteSingle(evil);
            StringAssert.Contains(quoted, args);
        }

        // ── CreateLoginShellPsi ─────────────────────────────────────────────

        [Test]
        public void CreateLoginShellPsi_MacOs_ReturnsZshPsi()
        {
            if (SystemInfo.operatingSystemFamily != OperatingSystemFamily.MacOSX)
                Assert.Ignore("macOS only");
            var psi = ShellHelper.CreateLoginShellPsi("echo hi", "arg");
            Assert.IsNotNull(psi);
            Assert.AreEqual("/bin/zsh", psi.FileName);
        }

        [Test]
        public void CreateLoginShellPsi_Linux_ReturnsBashOrShPsi()
        {
            if (SystemInfo.operatingSystemFamily != OperatingSystemFamily.Linux)
                Assert.Ignore("Linux only");
            var psi = ShellHelper.CreateLoginShellPsi("echo hi", "arg");
            Assert.IsNotNull(psi);
            StringAssert.StartsWith("/bin/", psi.FileName);
        }

        [Test]
        public void CreateLoginShellPsi_HasRedirectStdoutTrue()
        {
            if (SystemInfo.operatingSystemFamily == OperatingSystemFamily.Windows)
                Assert.Ignore("Windows returns null PSI");
            var psi = ShellHelper.CreateLoginShellPsi("echo hi", "arg");
            Assert.IsNotNull(psi);
            Assert.IsTrue(psi.RedirectStandardOutput);
        }

        [Test]
        public void CreateLoginShellPsi_HasShellExecuteFalse()
        {
            if (SystemInfo.operatingSystemFamily == OperatingSystemFamily.Windows)
                Assert.Ignore("Windows returns null PSI");
            var psi = ShellHelper.CreateLoginShellPsi("echo hi", "arg");
            Assert.IsNotNull(psi);
            Assert.IsFalse(psi.UseShellExecute);
        }

        // ── RunViaLoginShellAsync ────────────────────────────────────────────

        [Test]
        public async Task RunViaLoginShellAsync_UsesRunOverrideWhenSet()
        {
            ShellHelper.RunOverride = (_, __) => Task.FromResult("override-result");
            var result = await ShellHelper.RunViaLoginShellAsync("echo hi", 5000);
            Assert.AreEqual("override-result", result);
        }

        [Test]
        public async Task RunViaLoginShellAsync_ReturnsNullWhenOverrideReturnsNull()
        {
            ShellHelper.RunOverride = (_, __) => Task.FromResult<string>(null);
            var result = await ShellHelper.RunViaLoginShellAsync("echo hi", 5000);
            Assert.IsNull(result);
        }

        [Test]
        public async Task RunViaLoginShellAsync_ReturnsNullWhenOverrideReturnsWhitespace()
        {
            ShellHelper.RunOverride = (_, __) => Task.FromResult("   ");
            var result = await ShellHelper.RunViaLoginShellAsync("echo hi", 5000);
            Assert.IsNull(result);
        }

        [Test]
        public async Task RunViaLoginShellAsync_TrimsStdout()
        {
            ShellHelper.RunOverride = (_, __) => Task.FromResult("  result  ");
            var result = await ShellHelper.RunViaLoginShellAsync("echo hi", 5000);
            Assert.AreEqual("result", result);
        }
    }
}
