// TDD — RED first. P-160: Physics.SyncTransforms must fire before reading bounds.
// With autoSyncTransforms=false, col.bounds stays stale until explicit sync.
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal class SpatialHelperSyncTests : UnityMcpTestBase
    {
        private GameObject _go;
        private bool _prevAutoSync;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("SSH_BoundsSync");
            _go.AddComponent<BoxCollider>();
            TrackOwnedObject(_go);
            _prevAutoSync = Physics.autoSyncTransforms;
            Physics.autoSyncTransforms = false;
        }

        [TearDown]
        public void TearDown()
        {
            Physics.autoSyncTransforms = _prevAutoSync;
        }

        // RED before fix: col.bounds still reports origin because autoSyncTransforms=false
        // and BoundsInfo has no explicit Physics.SyncTransforms() call.
        [Test]
        public void BoundsInfo_AfterTransformMove_ReturnsFreshBounds()
        {
            _go.transform.position = new Vector3(10f, 5f, 3f);

            var result = SpatialHelper.BoundsInfo("/" + _go.name);

            Assert.That(result, Does.Contain("10.00"),
                "Bounds center X must reflect post-move position when autoSyncTransforms=false");
        }

        [Test]
        public void BoundsInfo_TwoConsecutiveCalls_ReturnIdenticalResults()
        {
            _go.transform.position = new Vector3(7f, 2f, 1f);

            var r1 = SpatialHelper.BoundsInfo("/" + _go.name);
            var r2 = SpatialHelper.BoundsInfo("/" + _go.name);

            Assert.AreEqual(r1, r2, "Consecutive BoundsInfo calls must return identical results");
        }

        // GetSpatialContext: pos read at L51 precedes SyncTransforms at L55,
        // so collider bounds section is stale on the first post-mutation call.
        [Test]
        public void GetSpatialContext_AfterTransformMove_ColliderBoundsMatchNewPosition()
        {
            _go.transform.position = new Vector3(8f, 4f, 2f);

            var result = LayoutValidator.GetSpatialContext("/" + _go.name, 5f);

            // Collider center must report new position, not origin
            Assert.That(result, Does.Contain("8.0"),
                "Collider bounds center X must reflect post-move position");
        }
    }
}
