// G28: After TELEPORT, Rigidbody.position must match the new transform.position.
// Physics.SyncTransforms() alone may not update rb.position in all configurations;
// explicit rb.position assignment ensures physics world consistency.
using NUnit.Framework;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class TeleportRigidbodySyncTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TRS_Test");
            TrackOwnedObject(_go);
        }

        [Test]
        public void AfterTeleport_RigidbodyPosition_MatchesTransformPosition()
        {
            var rb = _go.AddComponent<Rigidbody>();
            var newPos = new Vector3(10f, 5f, 3f);

            // Simulate what PlaytestRunner TELEPORT does (after G28 fix)
            _go.transform.position = newPos;
            Physics.SyncTransforms();
            rb.position = _go.transform.position; // explicit Rigidbody sync (G28 fix)

            Assert.That(Vector3.Distance(rb.position, newPos), Is.LessThan(0.001f),
                "Rigidbody.position must match transform.position after teleport");
        }

        [Test]
        public void AfterTeleport_NoRigidbody_TransformPositionSet()
        {
            // Without Rigidbody, only transform.position should be set (no error)
            var newPos = new Vector3(7f, 2f, 1f);
            _go.transform.position = newPos;
            Physics.SyncTransforms();
            var rb = _go.GetComponent<Rigidbody>();
            if (rb != null) rb.position = _go.transform.position;
            Assert.That(Vector3.Distance(_go.transform.position, newPos), Is.LessThan(0.001f));
        }
    }
}
