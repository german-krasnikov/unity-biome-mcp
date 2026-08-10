// TDD — RED first. Tests for MentionConfig + MentionSortOrder.
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class MentionConfigTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void MentionConfig_DefaultMaxPopupRows_Is8()
        {
            var cfg = new MentionConfig();
            Assert.AreEqual(8, cfg.MaxPopupRows);
        }

        [Test]
        public void MentionConfig_DefaultSortOrder_IsByRelevance()
        {
            var cfg = new MentionConfig();
            Assert.AreEqual(MentionSortOrder.ByRelevance, cfg.SortOrder);
        }

        [Test]
        public void MentionSortOrder_ByRelevance_IsZero()
        {
            Assert.AreEqual(0, (int)MentionSortOrder.ByRelevance);
        }

        [Test]
        public void MentionConfig_MaxPopupRows_CanBeSetTo20()
        {
            var cfg = new MentionConfig { MaxPopupRows = 20 };
            Assert.AreEqual(20, cfg.MaxPopupRows);
        }

        [Test]
        public void MentionConfig_MaxPopupRows_CanBeSetTo3()
        {
            var cfg = new MentionConfig { MaxPopupRows = 3 };
            Assert.AreEqual(3, cfg.MaxPopupRows);
        }

        [Test]
        public void BackendConfigStore_HasMentionField_WithDefaults()
        {
            var store = new BackendConfigStore();
            Assert.IsNotNull(store.Mention);
            Assert.AreEqual(8, store.Mention.MaxPopupRows);
        }

        [Test]
        public void BackendConfigStore_Load_NullGuardsMentionField()
        {
            // JSON without Mention field → null after JsonUtility → null-guard applies
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"MentionConfigTest_{System.Guid.NewGuid():N}.json");
            try
            {
                System.IO.File.WriteAllText(path, "{\"Claude\":{},\"Codex\":{}}");
                var store = BackendConfigStore.Load(path);
                Assert.IsNotNull(store.Mention);
            }
            finally
            {
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            }
        }

        [Test]
        public void BackendConfigStore_WithModel_PreservesMentionConfig()
        {
            var store = new BackendConfigStore
            {
                Mention = new MentionConfig { MaxPopupRows = 15, SortOrder = MentionSortOrder.ByName },
                Claude  = new ClaudeBackendConfig { Model = "a" },
            };
            var result = store.WithModel(BackendKind.Claude, "opus");
            Assert.IsNotNull(result.Mention);
            Assert.AreEqual(15, result.Mention.MaxPopupRows);
            Assert.AreEqual(MentionSortOrder.ByName, result.Mention.SortOrder);
        }
    }
}
