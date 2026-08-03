using System;
using System.IO;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class SessionScannerTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private string _tempDir;
        private Func<string> _origHome;
        private Func<string> _origProject;

        [SetUp]
        public void SetUp()
        {
            _origHome    = SessionScanner.HomeDir;
            _origProject = SessionScanner.ProjectDir;
            _tempDir = Path.Combine(Path.GetTempPath(), "SessionScannerTests_" + Guid.NewGuid().ToString("N"));
            RegisterCleanup(() =>
            {
                if (Directory.Exists(_tempDir))
                    Directory.Delete(_tempDir, true);
            });
            RegisterCleanup(() =>
            {
                SessionScanner.HomeDir = _origHome;
                SessionScanner.ProjectDir = _origProject;
            });
            Directory.CreateDirectory(_tempDir);
        }

        // ── EncodeCwd ───────────────────────────────────────────────────────────

        [Test]
        public void EncodeCwd_UnixPath_ReplacesSlashesWithDashes()
        {
            var result = SessionScanner.EncodeCwd("/Users/german/Work/project");
            Assert.AreEqual("-Users-german-Work-project", result);
        }

        [Test]
        public void EncodeCwd_WindowsPath_NormalizesBackslashes()
        {
            var result = SessionScanner.EncodeCwd(@"C:\Users\german\Work");
            Assert.AreEqual("C:-Users-german-Work", result);
        }

        [Test]
        public void EncodeCwd_Empty_ReturnsDash()
        {
            Assert.AreEqual("-", SessionScanner.EncodeCwd(""));
        }

        // ── GetSessionDir ───────────────────────────────────────────────────────

        [Test]
        public void GetSessionDir_Claude_ContainsDotClaude()
        {
            SessionScanner.HomeDir    = () => "/home/user";
            SessionScanner.ProjectDir = () => "/home/user/myproject";
            var dir = SessionScanner.GetSessionDir(BackendKind.Claude);
            StringAssert.Contains(".claude/projects", dir.Replace('\\', '/'));
            StringAssert.Contains("-home-user-myproject", dir);
        }

        [Test]
        public void GetSessionDir_Codex_PointsToDotCodex()
        {
            SessionScanner.HomeDir = () => "/home/user";
            var dir = SessionScanner.GetSessionDir(BackendKind.Codex);
            Assert.AreEqual("/home/user/.codex/sessions", dir.Replace('\\', '/'));
        }

        [Test]
        public void GetSessionDir_Kimi_PointsToDotKimiCode()
        {
            SessionScanner.HomeDir = () => "/home/user";
            var dir = SessionScanner.GetSessionDir(BackendKind.Kimi);
            Assert.AreEqual("/home/user/.kimi-code/sessions", dir.Replace('\\', '/'));
        }

        [Test]
        public void GetSessionDir_OpenCode_ReturnsNull()
        {
            Assert.IsNull(SessionScanner.GetSessionDir(BackendKind.OpenCode));
        }

        [Test]
        public void GetSessionDir_Antigravity_ContainsDotGeminiAntigravity()
        {
            SessionScanner.HomeDir = () => "/home/user";
            var dir = SessionScanner.GetSessionDir(BackendKind.Antigravity);
            StringAssert.Contains(".gemini/antigravity-cli/conversations", dir.Replace('\\', '/'));
        }

        // ── Scan ────────────────────────────────────────────────────────────────

        [Test]
        public void Scan_EmptyDir_ReturnsEmptyArray()
        {
            var sessionDir = Path.Combine(_tempDir, "sessions");
            Directory.CreateDirectory(sessionDir);
            SessionScanner.HomeDir    = () => _tempDir;
            SessionScanner.ProjectDir = () => "proj";

            // Redirect Claude to our empty dir by making the encoded path match
            var dir = Path.Combine(_tempDir, ".kimi-code", "sessions");
            Directory.CreateDirectory(dir);
            SessionScanner.HomeDir = () => _tempDir;

            var result = SessionScanner.Scan(BackendKind.Kimi);
            Assert.AreEqual(0, result.Length);
        }

        [Test]
        public void Scan_DirectoryMissing_ReturnsEmptyArray()
        {
            SessionScanner.HomeDir    = () => Path.Combine(_tempDir, "nonexistent");
            SessionScanner.ProjectDir = () => "proj";
            var result = SessionScanner.Scan(BackendKind.Kimi);
            Assert.AreEqual(0, result.Length);
        }

        [Test]
        public void Scan_Kimi_ReturnsJsonlFiles_NewestFirst()
        {
            var dir = Path.Combine(_tempDir, ".kimi-code", "sessions");
            Directory.CreateDirectory(dir);

            var oldFile = Path.Combine(dir, "session-old.jsonl");
            var newFile = Path.Combine(dir, "session-new.jsonl");
            File.WriteAllText(oldFile, "{}");
            File.WriteAllText(newFile, "{}");
            // Force older mtime on old file
            File.SetLastWriteTime(oldFile, DateTime.Now.AddHours(-2));
            File.SetLastWriteTime(newFile, DateTime.Now);

            SessionScanner.HomeDir = () => _tempDir;

            var result = SessionScanner.Scan(BackendKind.Kimi);
            Assert.AreEqual(2, result.Length);
            Assert.AreEqual("session-new", result[0].Id);
            Assert.AreEqual("session-old", result[1].Id);
        }

        [Test]
        public void Scan_MaxCount_Truncates()
        {
            var dir = Path.Combine(_tempDir, ".kimi-code", "sessions");
            Directory.CreateDirectory(dir);
            for (int i = 0; i < 5; i++)
                File.WriteAllText(Path.Combine(dir, $"s{i}.jsonl"), "{}");

            SessionScanner.HomeDir = () => _tempDir;
            var result = SessionScanner.Scan(BackendKind.Kimi, maxCount: 3);
            Assert.AreEqual(3, result.Length);
        }

        [Test]
        public void Scan_Codex_ReadsSubdirs()
        {
            var dir = Path.Combine(_tempDir, ".codex", "sessions");
            Directory.CreateDirectory(Path.Combine(dir, "abc-123"));
            Directory.CreateDirectory(Path.Combine(dir, "xyz-456"));

            SessionScanner.HomeDir = () => _tempDir;
            var result = SessionScanner.Scan(BackendKind.Codex);
            Assert.AreEqual(2, result.Length);
            Assert.AreEqual(BackendKind.Codex, result[0].Kind);
        }

        [Test]
        public void Scan_Antigravity_ReadsDbFiles()
        {
            var dir = Path.Combine(_tempDir, ".gemini", "antigravity-cli", "conversations");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "abc123.db"), "");

            SessionScanner.HomeDir = () => _tempDir;

            var result = SessionScanner.Scan(BackendKind.Antigravity);
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual("abc123", result[0].Id);
            Assert.AreEqual("Untitled", result[0].Title);
        }

        // ── ExtractAiTitle ──────────────────────────────────────────────────────

        [Test]
        public void ExtractAiTitle_SubtypeAiTitle_ReturnsValue()
        {
            var line = "{\"type\":\"summary\",\"subtype\":\"ai-title\",\"value\":\"My Session\"}";
            Assert.AreEqual("My Session", SessionScanner.ExtractAiTitle(line));
        }

        [Test]
        public void ExtractAiTitle_TypeAiTitle_ReturnsValue()
        {
            var line = "{\"type\":\"ai-title\",\"value\":\"Another Title\"}";
            Assert.AreEqual("Another Title", SessionScanner.ExtractAiTitle(line));
        }

        [Test]
        public void ExtractAiTitle_NoMatch_ReturnsNull()
        {
            var line = "{\"type\":\"user\",\"message\":\"hello\"}";
            Assert.IsNull(SessionScanner.ExtractAiTitle(line));
        }

        [Test]
        public void ExtractAiTitle_EmptyLine_ReturnsNull()
        {
            Assert.IsNull(SessionScanner.ExtractAiTitle(""));
        }

        // ── ReadJsonlTitle ──────────────────────────────────────────────────────

        [Test]
        public void ReadJsonlTitle_FileWithAiTitle_ExtractsIt()
        {
            var path = Path.Combine(_tempDir, "test.jsonl");
            File.WriteAllLines(path, new[]
            {
                "{\"type\":\"user\",\"message\":\"hello\"}",
                "{\"type\":\"summary\",\"subtype\":\"ai-title\",\"value\":\"Fix the bug\"}",
                "{\"type\":\"assistant\",\"message\":\"sure\"}",
            });
            Assert.AreEqual("Fix the bug", SessionScanner.ReadJsonlTitle(path));
        }

        [Test]
        public void ReadJsonlTitle_FileWithoutAiTitle_ReturnsUntitled()
        {
            var path = Path.Combine(_tempDir, "empty.jsonl");
            File.WriteAllText(path, "{\"type\":\"user\",\"message\":\"hello\"}\n");
            Assert.AreEqual("Untitled", SessionScanner.ReadJsonlTitle(path));
        }

        [Test]
        public void ReadJsonlTitle_NonexistentFile_ReturnsUntitled()
        {
            Assert.AreEqual("Untitled", SessionScanner.ReadJsonlTitle("/nonexistent/path.jsonl"));
        }

        [Test]
        public void Scan_Kimi_IncludesTitleFromJsonl()
        {
            var dir = Path.Combine(_tempDir, ".kimi-code", "sessions");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, "my-session.jsonl");
            File.WriteAllLines(file, new[]
            {
                "{\"type\":\"user\",\"message\":\"hello\"}",
                "{\"type\":\"summary\",\"subtype\":\"ai-title\",\"value\":\"Hello World\"}",
            });

            SessionScanner.HomeDir = () => _tempDir;
            var result = SessionScanner.Scan(BackendKind.Kimi);
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual("Hello World", result[0].Title);
            Assert.AreEqual("my-session", result[0].Id);
        }
    }
}
