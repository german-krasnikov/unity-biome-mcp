// TDD: ReloadDomainStamp — 3 tests per plan (грань 3: ComputeStamp_ReturnsNonEmpty,
// ComputeStamp_ContainsColon, MainDomainStamp_NotNull)
using NUnit.Framework;

namespace UnityMCP.Reload.Tests
{
    [TestFixture]
    public class ReloadDomainStampTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void ComputeStamp_ReturnsNonEmpty()
        {
            var stamp = ReloadDomainStamp.ComputeStamp();
            Assert.IsFalse(string.IsNullOrEmpty(stamp),
                "ComputeStamp() must return non-empty string when assembly is loaded");
        }

        [Test]
        public void ComputeStamp_ContainsColon()
        {
            var stamp = ReloadDomainStamp.ComputeStamp();
            StringAssert.Contains(":", stamp,
                "Stamp format must be 'mvid:mtime_ticks' — colon separator required");
        }

        [Test]
        public void MainDomainStamp_IsNotNull()
        {
            // SessionState returns "" when key absent — never null
            var stamp = ReloadDomainStamp.MainDomainStamp;
            Assert.IsNotNull(stamp,
                "MainDomainStamp must never return null (SessionState contract)");
        }

        [Test]
        public void MainAsmdefMvid_WhenUnityMCPEditorLoaded_ReturnsMvid()
        {
            var mvid = ReloadDomainStamp.MainAsmdefMvid();
            Assert.IsNotNull(mvid);
            Assert.IsFalse(string.IsNullOrEmpty(mvid));
            StringAssert.AreNotEqualIgnoringCase("absent", mvid,
                "MainAsmdefMvid must return real GUID when UnityMCP.Editor is loaded");
        }
    }
}
