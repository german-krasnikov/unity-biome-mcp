using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal class NlComposerBridgeTests
    {
        [SetUp]
        public void SetUp()
        {
            NlComposerBridge.RunProcessOverride    = null;
            NlComposerBridge.ResolveBinaryOverride = null;
            EditorPrefs.DeleteKey("UnityMCP_Chat_Path_claude");
            EditorPrefs.DeleteKey("UnityMCP_Chat_Path_codex");
        }

        [TearDown]
        public void TearDown()
        {
            NlComposerBridge.RunProcessOverride    = null;
            NlComposerBridge.ResolveBinaryOverride = null;
            EditorPrefs.DeleteKey("UnityMCP_Chat_Path_claude");
            EditorPrefs.DeleteKey("UnityMCP_Chat_Path_codex");
        }

        // ── ParseAsync gating ──────────────────────────────────────────────

        [Test]
        public async Task ParseAsync_ModelEmpty_StillRuns()
        {
            NlComposerBridge.ResolveBinaryOverride = _ => "/usr/bin/claude";
            NlComposerBridge.RunProcessOverride = (_, __, ___) => Task.FromResult("WAIT 2");
            var cfg = new SamplingConfig { Model = "test" };
            var result = await NlComposerBridge.ParseAsync("wait 2", cfg);
            Assert.AreEqual("WAIT 2", result);
        }

        [Test]
        public async Task ParseAsync_BinaryNotFound_ReturnsNull()
        {
            NlComposerBridge.ResolveBinaryOverride = _ => null;
            NlComposerBridge.RunProcessOverride    = (_, __, ___) => throw new System.Exception("must not spawn");
            var cfg = new SamplingConfig { Model = "haiku" };
            var result = await NlComposerBridge.ParseAsync("wait 2", cfg);
            Assert.IsNull(result);
        }

        [Test]
        public async Task ParseAsync_ValidDsl_ReturnsThat()
        {
            NlComposerBridge.ResolveBinaryOverride = _ => "/usr/bin/claude";
            NlComposerBridge.RunProcessOverride    = (_, __, ___) => Task.FromResult("WAIT 2\nASSERT_CONSOLE_CLEAN");
            var cfg = new SamplingConfig { Model = "haiku" };
            var result = await NlComposerBridge.ParseAsync("wait 2", cfg);
            StringAssert.Contains("WAIT 2", result);
        }

        [Test]
        public async Task ParseAsync_RunProcessReturnsNull_ReturnsNull()
        {
            NlComposerBridge.ResolveBinaryOverride = _ => "/usr/bin/claude";
            NlComposerBridge.RunProcessOverride    = (_, __, ___) => Task.FromResult<string>(null);
            var cfg = new SamplingConfig { Model = "haiku" };
            var result = await NlComposerBridge.ParseAsync("wait 2", cfg);
            Assert.IsNull(result);
        }

        [Test]
        public async Task ParseAsync_RunProcessReturnsEmpty_ReturnsNull()
        {
            NlComposerBridge.ResolveBinaryOverride = _ => "/usr/bin/claude";
            NlComposerBridge.RunProcessOverride    = (_, __, ___) => Task.FromResult("");
            var cfg = new SamplingConfig { Model = "haiku" };
            var result = await NlComposerBridge.ParseAsync("wait 2", cfg);
            Assert.IsNull(result);
        }

        [Test]
        public async Task ParseAsync_RunProcessReturnsWhitespace_ReturnsNull()
        {
            NlComposerBridge.ResolveBinaryOverride = _ => "/usr/bin/claude";
            NlComposerBridge.RunProcessOverride    = (_, __, ___) => Task.FromResult("   ");
            var cfg = new SamplingConfig { Model = "haiku" };
            var result = await NlComposerBridge.ParseAsync("wait 2", cfg);
            Assert.IsNull(result);
        }

        // ── BuildPrompt ────────────────────────────────────────────────────

        [Test]
        public void BuildPrompt_ContainsUserText()
        {
            var result = NlComposerBridge.BuildPrompt("wait 5 seconds");
            StringAssert.Contains("wait 5 seconds", result);
        }

        [Test]
        public void BuildPrompt_ContainsWaitCommand() =>
            StringAssert.Contains("WAIT", NlComposerBridge.BuildPrompt("x"));

        [Test]
        public void BuildPrompt_ContainsWaitUntilCommand() =>
            StringAssert.Contains("WAIT_UNTIL", NlComposerBridge.BuildPrompt("x"));

        [Test]
        public void BuildPrompt_ContainsAssertCommand() =>
            StringAssert.Contains("ASSERT", NlComposerBridge.BuildPrompt("x"));

        [Test]
        public void BuildPrompt_ContainsInvokeCommand() =>
            StringAssert.Contains("INVOKE", NlComposerBridge.BuildPrompt("x"));

        [Test]
        public void BuildPrompt_ContainsMoveCommand() =>
            StringAssert.Contains("MOVE", NlComposerBridge.BuildPrompt("x"));

        [Test]
        public void BuildPrompt_ContainsFewShotExamples()
        {
            var result = NlComposerBridge.BuildPrompt("x");
            var matches = System.Text.RegularExpressions.Regex.Matches(result, "^IN:", System.Text.RegularExpressions.RegexOptions.Multiline);
            Assert.GreaterOrEqual(matches.Count, 3);
        }

        [Test]
        public void BuildPrompt_ContainsUnparsedInstruction() =>
            StringAssert.Contains("UNPARSED", NlComposerBridge.BuildPrompt("x"));

        [Test]
        public void BuildPrompt_PathBrackets_Explained() =>
            StringAssert.Contains("[/", NlComposerBridge.BuildPrompt("x"));

        [Test]
        public void BuildPrompt_MultiLanguageInstruction()
        {
            var result = NlComposerBridge.BuildPrompt("x");
            Assert.IsTrue(
                result.IndexOf("Russian", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                result.Contains("Multi-language") || result.Contains("multi-language"));
        }

        [Test]
        public void BuildPrompt_EmptyText_StillBuilds() =>
            Assert.DoesNotThrow(() => NlComposerBridge.BuildPrompt(""));

        // ── ResolveBinary ──────────────────────────────────────────────────

        [Test]
        public void ResolveBinary_Claude_ReadsClaudePrefKey()
        {
            EditorPrefs.SetString("UnityMCP_Chat_Path_claude", "/usr/bin/claude");
            var cfg = new SamplingConfig { Backend = "" };
            Assert.AreEqual("/usr/bin/claude", NlComposerBridge.ResolveBinary(cfg));
        }

        [Test]
        public void ResolveBinary_Codex_ReadsCodexPrefKey()
        {
            EditorPrefs.SetString("UnityMCP_Chat_Path_codex", "/usr/bin/codex");
            var cfg = new SamplingConfig { Backend = "codex" };
            Assert.AreEqual("/usr/bin/codex", NlComposerBridge.ResolveBinary(cfg));
        }

        [Test]
        public void ResolveBinary_NoPref_FallsBackToBackendName()
        {
            var cfg = new SamplingConfig { Backend = "" };
            Assert.AreEqual("claude", NlComposerBridge.ResolveBinary(cfg));
        }

        [Test]
        public void ResolveBinary_UnknownBackend_FallsBackToName()
        {
            var cfg = new SamplingConfig { Backend = "mybackend" };
            Assert.AreEqual("mybackend", NlComposerBridge.ResolveBinary(cfg));
        }

        // ── BuildArgs ─────────────────────────────────────────────────────

        [Test]
        public void BuildArgs_Claude_UsesPrintFlag()
        {
            var cfg = new SamplingConfig { Model = "haiku", MaxTokens = 512 };
            var args = NlComposerBridge.BuildArgs("claude", "prompt", cfg);
            CollectionAssert.Contains(args, "--print");
        }

        [Test]
        public void BuildArgs_Claude_UsesModelFlag()
        {
            var cfg = new SamplingConfig { Model = "haiku", MaxTokens = 512 };
            var args = NlComposerBridge.BuildArgs("claude", "prompt", cfg);
            CollectionAssert.Contains(args, "--model");
            var idx = System.Array.IndexOf(args, "--model");
            Assert.AreEqual("haiku", args[idx + 1]);
        }

        [Test]
        public void BuildArgs_Claude_ModelEmpty_DefaultsToHaiku()
        {
            var cfg = new SamplingConfig { Model = "", MaxTokens = 512 };
            var args = NlComposerBridge.BuildArgs("claude", "prompt", cfg);
            var idx = System.Array.IndexOf(args, "--model");
            Assert.AreEqual("haiku", args[idx + 1]);
        }

        [Test]
        public void BuildArgs_Claude_NoMaxTokensFlag()
        {
            var cfg = new SamplingConfig { Model = "haiku", MaxTokens = 512 };
            var args = NlComposerBridge.BuildArgs("claude", "prompt", cfg);
            CollectionAssert.DoesNotContain(args, "--max-tokens");
        }

        [Test]
        public void BuildArgs_Codex_UsesPromptFlag()
        {
            var cfg = new SamplingConfig { Model = "gpt-4o" };
            var args = NlComposerBridge.BuildArgs("codex", "prompt", cfg);
            CollectionAssert.Contains(args, "--prompt");
        }

        [Test]
        public void BuildArgs_Codex_NoPrintFlag()
        {
            var cfg = new SamplingConfig { Model = "gpt-4o" };
            var args = NlComposerBridge.BuildArgs("codex", "prompt", cfg);
            CollectionAssert.DoesNotContain(args, "--print");
        }

        [Test]
        public async Task BuildArgs_Claude_BinaryPrependedInShellCmd()
        {
            string capturedBin  = null;
            string[] capturedArgs = null;
            NlComposerBridge.ResolveBinaryOverride = _ => "/usr/bin/claude";
            NlComposerBridge.RunProcessOverride = (bin, args, _) =>
            {
                capturedBin   = bin;
                capturedArgs  = args;
                return Task.FromResult("WAIT 2");
            };
            var cfg = new SamplingConfig { Model = "haiku" };
            await NlComposerBridge.ParseAsync("wait 2", cfg);
            Assert.AreEqual("/usr/bin/claude", capturedBin);
            Assert.IsNotNull(capturedArgs);
            StringAssert.Contains("--print", string.Join(" ", capturedArgs));
        }

        [Test]
        public void BuildArgs_ShellQuote_SingleQuotesPrompt()
        {
            var cfg  = new SamplingConfig { Model = "haiku" };
            var args = NlComposerBridge.BuildArgs("claude", "it's a test", cfg);
            // args[1] is the shell-quoted prompt
            StringAssert.StartsWith("'", args[1]);
            StringAssert.Contains("'\\''", args[1]);
        }

        [Test]
        public void BuildArgs_Codex_NoModelFlag()
        {
            var cfg  = new SamplingConfig { Model = "gpt-4o" };
            var args = NlComposerBridge.BuildArgs("codex", "wait 2", cfg);
            CollectionAssert.DoesNotContain(args, "--model");
            CollectionAssert.DoesNotContain(args, "--print");
        }
    }

    [TestFixture]
    [Category("LiveCLI")]
    internal class NlComposerBridgeLiveTests
    {
        static string _binary;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            NlComposerBridge.ResolveBinaryOverride = null;
            NlComposerBridge.RunProcessOverride = null;
            _binary = NlComposerBridge.ResolveBinary(new SamplingConfig());
            if (string.IsNullOrEmpty(_binary) || _binary == "claude")
            {
                // Check if claude is actually in PATH
                try
                {
                    var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("which", "claude")
                    { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true });
                    var path = p?.StandardOutput.ReadToEnd()?.Trim();
                    p?.WaitForExit(3000);
                    if (string.IsNullOrEmpty(path)) Assert.Ignore("claude CLI not found in PATH");
                    _binary = path;
                }
                catch { Assert.Ignore("claude CLI not found"); }
            }
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            NlComposerBridge.ResolveBinaryOverride = null;
            NlComposerBridge.RunProcessOverride = null;
        }

        static SamplingConfig Cfg() => new SamplingConfig { Model = "haiku", Timeout = 15f, MaxTokens = 512 };

        [Test]
        public async Task Live_EnglishMove_ProducesValidDsl()
        {
            NlComposerBridge.ResolveBinaryOverride = _ => _binary;
            var result = await NlComposerBridge.ParseAsync("move /Player to 0,0,0 then wait 2 seconds", Cfg());
            Assert.IsNotNull(result, "LLM returned null");
            StringAssert.Contains("MOVE", result);
            StringAssert.Contains("WAIT", result);
        }

        [Test]
        public async Task Live_RussianWait_ProducesDsl()
        {
            NlComposerBridge.ResolveBinaryOverride = _ => _binary;
            var result = await NlComposerBridge.ParseAsync("подожди 3 секунды", Cfg());
            Assert.IsNotNull(result, "LLM returned null");
            StringAssert.Contains("WAIT", result);
            StringAssert.Contains("3", result);
        }

        [Test]
        public async Task Live_RussianAssert_ProducesDsl()
        {
            NlComposerBridge.ResolveBinaryOverride = _ => _binary;
            var result = await NlComposerBridge.ParseAsync("проверь что /Enemy Health isDead == true", Cfg());
            Assert.IsNotNull(result, "LLM returned null");
            StringAssert.Contains("ASSERT", result);
        }

        [Test]
        public async Task Live_MultiStep_ProducesMultipleLines()
        {
            NlComposerBridge.ResolveBinaryOverride = _ => _binary;
            var result = await NlComposerBridge.ParseAsync(
                "телепортируй /Player в 5,0,3 потом подожди 1 секунду потом сделай скриншот", Cfg());
            Assert.IsNotNull(result, "LLM returned null");
            var lines = result.Split('\n');
            Assert.GreaterOrEqual(lines.Length, 2, $"Expected multi-line DSL, got: {result}");
        }

        [Test]
        public async Task Live_ConsoleClean_ProducesDsl()
        {
            NlComposerBridge.ResolveBinaryOverride = _ => _binary;
            var result = await NlComposerBridge.ParseAsync("assert console clean", Cfg());
            Assert.IsNotNull(result, "LLM returned null");
            StringAssert.Contains("ASSERT_CONSOLE_CLEAN", result);
        }
    }
}
