// TDD: StaleCacheException classification and FindObject stale-ref behavior.
// Red→Green: StaleCacheException must be classified as "STALE_CACHE", not "VALIDATION" or "STATE".
using NUnit.Framework;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class StaleCacheErrorTests : SceneTestBase
    {
        [SetUp]
        public void SetUp() => RefManager.Invalidate();

        [TearDown]
        public void TearDown() => RefManager.Invalidate();

        // ── ErrorClassifier ───────────────────────────────────────────────────

        [Test]
        public void StaleCacheException_ClassifiedAsStaleCache()
        {
            var ex = new StaleCacheException("path cache stale");
            Assert.AreEqual("STALE_CACHE", ErrorClassifier.Classify(ex));
        }

        [Test]
        public void StaleCacheException_FormatError_IncludesCode()
        {
            var ex = new StaleCacheException("path cache stale");
            Assert.AreEqual("STALE_CACHE: path cache stale", ErrorClassifier.FormatError(ex));
        }

        [Test]
        public void InvalidOperationException_StillMapsToState()
        {
            // Regression guard: regular InvalidOperationException must NOT become STALE_CACHE.
            var ex = new System.InvalidOperationException("bad state");
            Assert.AreEqual("STATE", ErrorClassifier.Classify(ex));
        }

        // ── ComponentSerializer.FindObject ────────────────────────────────────

        [Test]
        public void FindObject_StaleRef_ThrowsStaleCacheException()
        {
            var go = TrackOwnedObject(new GameObject("StaleCacheTest_StaleRef"));
            var refStr = RefManager.Assign(go);

            // Destroy the GO to make the ref stale.
            Object.DestroyImmediate(go);

            Assert.Throws<StaleCacheException>(() => ComponentSerializer.FindObject(refStr));
        }

        [Test]
        public void FindObject_RegularPath_NotFound_ReturnsNull()
        {
            // Non-ref paths that don't exist should return null (STATE from ExecGetComponent, not STALE_CACHE).
            var result = ComponentSerializer.FindObject("NonExistentObject_XYZ_StaleCacheTest");
            Assert.IsNull(result);
        }
    }
}
