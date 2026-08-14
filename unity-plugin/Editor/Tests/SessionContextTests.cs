// TDD: RED — tests for SessionContext.cs (session token gen + Library persistence).
// SessionDirOverride seam redirects all file I/O to a temp directory.
using System;
using System.IO;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class SessionContextTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(
                Path.GetTempPath(),
                "SessionContextTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            SessionContext.SessionDirOverride = _tempDir;
            RegisterCleanup(() =>
            {
                SessionContext.SessionDirOverride = null;
                if (Directory.Exists(_tempDir))
                    Directory.Delete(_tempDir, recursive: true);
            });
        }

        [Test]
        public void GenerateToken_Returns64HexChars()
        {
            var token = SessionContext.GenerateToken();
            Assert.That(token.Length, Is.EqualTo(64));
        }

        [Test]
        public void GenerateToken_IsHexString()
        {
            var token = SessionContext.GenerateToken();
            Assert.That(token, Does.Match("^[0-9a-f]{64}$"));
        }

        [Test]
        public void GenerateToken_IsDifferentEachTime()
        {
            var t1 = SessionContext.GenerateToken();
            var t2 = SessionContext.GenerateToken();
            Assert.That(t1, Is.Not.EqualTo(t2));
        }

        [Test]
        public void SaveAndLoadSession_RoundTrip_ReturnsToken()
        {
            var token = SessionContext.GenerateToken();
            SessionContext.SaveSession(token);
            bool ok = SessionContext.TryLoadSession(out var loaded);
            Assert.That(ok, Is.True);
            Assert.That(loaded, Is.EqualTo(token));
        }

        [Test]
        public void TryLoadSession_MissingFile_ReturnsFalse()
        {
            bool ok = SessionContext.TryLoadSession(out var token);
            Assert.That(ok, Is.False);
            Assert.That(token, Is.Null);
        }

        [Test]
        public void TryLoadSession_CorruptJson_ReturnsFalse()
        {
            var path = Path.Combine(_tempDir, "chat_session.json");
            File.WriteAllText(path, "!!!");
            bool ok = SessionContext.TryLoadSession(out _);
            Assert.That(ok, Is.False);
        }
    }
}
