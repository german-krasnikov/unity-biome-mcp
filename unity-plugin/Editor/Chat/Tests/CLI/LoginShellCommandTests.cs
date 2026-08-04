using NUnit.Framework;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    [UnityMCP.Editor.Testing.SkipOnWindows("LoginShellCommand assumes /bin/bash — not available on Windows")]
    public class LoginShellCommandTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void ShellQuoteSingle_Plain_WrapsInSingleQuotes()
        {
            Assert.AreEqual("'hello'", LoginShellCommand.ShellQuoteSingle("hello"));
        }

        [Test]
        public void ShellQuoteSingle_ContainsSingleQuote_EscapedCorrectly()
        {
            // "it's" → 'it'\''s'
            Assert.AreEqual("'it'\\''s'", LoginShellCommand.ShellQuoteSingle("it's"));
        }

        [Test]
        public void ShellQuoteSingle_InjectionPayload_EscapedCorrectly()
        {
            // x'; rm -rf ~; ' — each ' becomes '\''
            // Result: 'x'\''; rm -rf ~; '\'''
            const string payload = "x'; rm -rf ~; '";
            var quoted = LoginShellCommand.ShellQuoteSingle(payload);
            Assert.AreEqual("'x'\\''; rm -rf ~; '\\'''", quoted,
                "POSIX single-quote escape: each ' must become '\\''");
        }

        [Test]
        public void BuildArguments_InjectionPath_BinaryFullyQuoted_NoBarRmRf()
        {
            // "x'; rm -rf ~; '" must appear as a single single-quoted token; "rm -rf" must NOT be bare.
            const string evilBinary = "x'; rm -rf ~; '";
            var args = LoginShellCommand.BuildArguments("\"$1\" auth status", evilBinary);
            // The quoted binary token must be present
            var quoted = LoginShellCommand.ShellQuoteSingle(evilBinary);
            Assert.IsTrue(args.Contains(quoted), "binary must appear as a single-quoted token");
            // "rm -rf" must not appear outside quotes (i.e. as a bare shell word)
            // The only occurrence is inside the quoted token — verify that.
            var outerArgs = args.Replace(quoted, "");
            Assert.IsFalse(outerArgs.Contains("rm -rf"), "rm -rf must not be a bare shell token");
        }

        [Test]
        public void BuildArguments_ProducesCorrectShellString()
        {
            // -lic: login (-l) + interactive (-i) + command (-c); interactive needed for PATH resolution on macOS
            var s = LoginShellCommand.BuildArguments("\"$1\" auth status", "/usr/bin/claude");
            var shellName = SystemInfo.operatingSystemFamily == OperatingSystemFamily.Linux
                ? (File.Exists("/bin/bash") ? "bash" : "sh")
                : "zsh";
            Assert.AreEqual($"-lic '\"$1\" auth status' {shellName} '/usr/bin/claude'", s);
        }

        [Test]
        public void Create_SetsCorrectFileName()
        {
            var psi = LoginShellCommand.Create("\"$1\" auth status", "/usr/bin/claude");
            var expected = SystemInfo.operatingSystemFamily == OperatingSystemFamily.Linux
                ? (File.Exists("/bin/bash") ? "/bin/bash" : "/bin/sh")
                : "/bin/zsh";
            Assert.AreEqual(expected, psi.FileName);
        }

        [Test]
        public void Create_SetsUseShellExecuteFalse()
        {
            var psi = LoginShellCommand.Create("\"$1\" auth status", "/usr/bin/claude");
            Assert.IsFalse(psi.UseShellExecute);
        }

        [Test]
        public void Create_SetsRedirectStandardOutputTrue()
        {
            var psi = LoginShellCommand.Create("\"$1\" auth status", "/usr/bin/claude");
            Assert.IsTrue(psi.RedirectStandardOutput);
        }

        [Test]
        public void Create_SetsCreateNoWindowTrue()
        {
            var psi = LoginShellCommand.Create("\"$1\" auth status", "/usr/bin/claude");
            Assert.IsTrue(psi.CreateNoWindow);
        }

        [Test]
        public void Create_ArgumentsString_MatchesBuildArguments()
        {
            var psi = LoginShellCommand.Create("command -v \"$1\"", "claude");
            var expected = LoginShellCommand.BuildArguments("command -v \"$1\"", "claude");
            Assert.AreEqual(expected, psi.Arguments);
        }
    }
}
