// T23: BiomeConversationStore unit tests using static HistoryDir test seam.
using System;
using System.IO;
using NUnit.Framework;
using UnityMCP.Editor.Chat.CLI;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class BiomeConversationStoreTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private string _tempDir;
        private Func<string> _savedHistoryDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "BiomeHistoryTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            _savedHistoryDir = BiomeConversationStore.HistoryDir;
            BiomeConversationStore.HistoryDir = () => _tempDir;
        }

        [TearDown]
        public void TearDown()
        {
            BiomeConversationStore.HistoryDir = _savedHistoryDir;
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }

        private void WriteMeta(string convId, string title = "T", string backend = "Claude",
            string sessionId = "", int turnCount = 1)
        {
            var json = $"{{\"v\":1,\"id\":\"{convId}\",\"title\":\"{title}\"," +
                       $"\"backend\":\"{backend}\",\"session_id\":\"{sessionId}\"," +
                       $"\"turn_count\":{turnCount},\"fingerprint\":\"fp\"}}";
            File.WriteAllText(Path.Combine(_tempDir, $"{convId}.meta.json"), json);
            File.WriteAllText(Path.Combine(_tempDir, $"{convId}.jsonl"), "");
        }

        [Test]
        public void Scan_EmptyWhenDirectoryMissing()
        {
            BiomeConversationStore.HistoryDir = () => Path.Combine(_tempDir, "nonexistent");
            var metas = BiomeConversationStore.Scan();
            Assert.AreEqual(0, metas.Length);
        }

        [Test]
        public void Scan_ParsesMetaFiles()
        {
            WriteMeta("conv1", "How do I fix this?");
            var metas = BiomeConversationStore.Scan();
            Assert.AreEqual(1, metas.Length);
            Assert.AreEqual("conv1",               metas[0].Id);
            Assert.AreEqual("How do I fix this?",  metas[0].Title);
            Assert.AreEqual("Claude",              metas[0].BackendKind);
        }

        [Test]
        public void Scan_CapsAtMaxCount()
        {
            for (int i = 0; i < 35; i++) WriteMeta($"conv_{i:D3}");
            var metas = BiomeConversationStore.Scan(maxCount: 10);
            Assert.LessOrEqual(metas.Length, 10);
        }

        [Test]
        public void Scan_SkipsCorruptMetaFiles()
        {
            File.WriteAllText(Path.Combine(_tempDir, "bad.meta.json"), "{corrupt");
            WriteMeta("good");
            var metas = BiomeConversationStore.Scan();
            Assert.AreEqual(1, metas.Length);
            Assert.AreEqual("good", metas[0].Id);
        }

        [Test]
        public void LoadEventLines_EmptyForMissingConvId()
        {
            var lines = BiomeConversationStore.LoadEventLines("nonexistent");
            Assert.AreEqual(0, lines.Length);
        }

        [Test]
        public void LoadEventLines_ReturnsLinesFromJsonl()
        {
            var line = "{\"kind\":\"turn_started\",\"payload\":{\"text\":\"hello\"}}";
            File.WriteAllText(Path.Combine(_tempDir, "c1.jsonl"), line + "\n");
            var lines = BiomeConversationStore.LoadEventLines("c1");
            Assert.AreEqual(1, lines.Length);
            Assert.AreEqual(line, lines[0]);
        }
    }
}
