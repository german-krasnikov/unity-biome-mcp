using NUnit.Framework;
using UnityEditor;
using UnityMCP.Editor;

namespace UnityMCP.TestProject.Runtime
{
    [TestFixture]
    public class PlaytestLaunchWindowTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUp()
        {
            DeleteEditorPrefString(PlaytestAutoLaunch.PrefKey);
            DeleteEditorPrefFloat(PlaytestAutoLaunch.TimeoutKey);
        }

        [Test]
        public void NoPendingKey_ReturnsFalse()
        {
            var result = PlaytestAutoLaunch.TryGetPendingTest(out _, out _);
            Assert.That(result, Is.False);
        }

        [Test]
        public void PendingKey_ReturnsTrueAndClears()
        {
            SetEditorPrefString(PlaytestAutoLaunch.PrefKey, "/fake/path.playtest");
            SetEditorPrefFloat(PlaytestAutoLaunch.TimeoutKey, 60f);

            var result = PlaytestAutoLaunch.TryGetPendingTest(out var path, out var timeout);

            Assert.That(result, Is.True);
            Assert.That(path, Is.EqualTo("/fake/path.playtest"));
            Assert.That(timeout, Is.EqualTo(60f).Within(0.001f));
            Assert.That(EditorPrefs.HasKey(PlaytestAutoLaunch.PrefKey), Is.False);
            Assert.That(EditorPrefs.HasKey(PlaytestAutoLaunch.TimeoutKey), Is.False);
        }

        [Test]
        public void PendingKey_MissingFile_TryGetDoesNotThrow()
        {
            SetEditorPrefString(PlaytestAutoLaunch.PrefKey, "/nonexistent/path.playtest");
            Assert.DoesNotThrow(() => PlaytestAutoLaunch.TryGetPendingTest(out _, out _));
        }

        [Test]
        public void DefaultTimeout_Is120_WhenKeyAbsent()
        {
            SetEditorPrefString(PlaytestAutoLaunch.PrefKey, "/fake.playtest");
            // TimeoutKey not set — should default to 120f
            PlaytestAutoLaunch.TryGetPendingTest(out _, out var timeout);
            Assert.That(timeout, Is.EqualTo(120f).Within(0.001f));
        }
    }
}
