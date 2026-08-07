using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityMCP.Editor;

namespace UnityMCP.TestProject.Scene
{
    [TestFixture]
    public class SpatialTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private GameObject _player;
        private GameObject _enemy;
        private GameObject _item;
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _player = TrackOwnedObject(new GameObject("SpatialPlayer"));
            _player.transform.position = Vector3.zero;

            _enemy = TrackOwnedObject(new GameObject("SpatialEnemy"));
            _enemy.transform.position = new Vector3(3, 0, 0);
            _enemy.AddComponent<Rigidbody>();

            _item = TrackOwnedObject(new GameObject("SpatialItem"));
            _item.transform.position = new Vector3(10, 0, 0);

            _root = TrackOwnedObject(new GameObject("LayoutTestRoot"));
            _root.transform.position = new Vector3(100, 0, 0);

            var child1 = new GameObject("Trigger1");
            child1.transform.parent = _root.transform;
            child1.transform.position = new Vector3(50, 0, 0); // far from SpatialPlayer (0,0,0)
            var col1 = child1.AddComponent<BoxCollider>();
            col1.isTrigger = true;

            var child2 = new GameObject("Trigger2");
            child2.transform.parent = _root.transform;
            child2.transform.position = new Vector3(51, 0, 0); // far, dist=1 from Trigger1 for LayoutValidator
            var col2 = child2.AddComponent<BoxCollider>();
            col2.isTrigger = true;

            var child3 = new GameObject("Solid1");
            child3.transform.parent = _root.transform;
            child3.transform.position = new Vector3(52, 0, 0); // far from SpatialPlayer
            child3.AddComponent<BoxCollider>(); // isTrigger = false by default
        }

        // --- SpatialHelper ---

        [Test]
        public void Nearest_FindsClosest()
        {
            // Player at (0,0,0); Enemy at (3,0,0); Item at (10,0,0)
            // Nearest to player (no filter) should be Enemy
            var result = SpatialHelper.Nearest("/SpatialPlayer", "");
            StringAssert.Contains("dist=", result);
            // Closest non-self object should have dist < 10
            Assert.That(result, Does.Not.Contain("dist=10"));
            Assert.That(result, Does.Contain("3.00").Or.Contain("dist=3"));
        }

        [Test]
        public void Nearest_WithComponentFilter_FindsMatchingObject()
        {
            // Enemy has Rigidbody; Item has none
            var result = SpatialHelper.Nearest("/SpatialPlayer", "Rigidbody");
            StringAssert.Contains("SpatialEnemy", result);
            StringAssert.Contains("dist=", result);
        }

        [Test]
        public void Nearest_NoMatchingComponent_ReturnsNotFound()
        {
            var result = SpatialHelper.Nearest("/SpatialPlayer", "SomeNonExistentComponent999");
            Assert.AreEqual("No matching object found", result);
        }

        [Test]
        public void Nearest_ObjectNotFound_Throws()
        {
            Assert.Throws<System.ArgumentException>(() =>
                SpatialHelper.Nearest("/NoSuchObject999", ""));
        }

        [Test]
        public void InFrontOf_ReturnsCorrectPosition()
        {
            // Player at (0,0,0) facing forward (0,0,1) with distance=5 → (0,0,5)
            var result = SpatialHelper.InFrontOf("/SpatialPlayer", 5f);
            // Result is "(x,y,z)" format
            StringAssert.StartsWith("(", result);
            StringAssert.EndsWith(")", result);
        }

        [Test]
        public void InFrontOf_ZeroDistance_ReturnsObjectPosition()
        {
            var result = SpatialHelper.InFrontOf("/SpatialPlayer", 0f);
            // At distance 0, pos = object pos = (0,0,0)
            Assert.AreEqual("(0.00,0.00,0.00)", result);
        }

        [Test]
        public void InFrontOf_ObjectNotFound_Throws()
        {
            Assert.Throws<System.ArgumentException>(() =>
                SpatialHelper.InFrontOf("/NoSuchObject999", 5f));
        }

        [Test]
        public void ObjectsInRadius_FindsNearby()
        {
            // Enemy is at dist=3 from Player, Item at dist=10
            var result = SpatialHelper.ObjectsInRadius("/SpatialPlayer", 5f);
            StringAssert.Contains("SpatialEnemy", result);
            Assert.That(result, Does.Not.Contain("SpatialItem"));
        }

        [Test]
        public void ObjectsInRadius_NoObjects_ReturnsNotFound()
        {
            // Radius 0 → nothing
            var result = SpatialHelper.ObjectsInRadius("/SpatialPlayer", 0.001f);
            Assert.AreEqual("No objects within radius", result);
        }

        [Test]
        public void ObjectsInRadius_ObjectNotFound_Throws()
        {
            Assert.Throws<System.ArgumentException>(() =>
                SpatialHelper.ObjectsInRadius("/NoSuchObject999", 5f));
        }

        [Test]
        public void BoundsInfo_ReturnsDimensions()
        {
            // Add a BoxCollider so bounds are meaningful
            _player.AddComponent<BoxCollider>();
            var result = SpatialHelper.BoundsInfo("/SpatialPlayer");
            StringAssert.Contains("center=", result);
            StringAssert.Contains("size=", result);
            StringAssert.Contains("min=", result);
            StringAssert.Contains("max=", result);
        }

        [Test]
        public void BoundsInfo_ObjectNotFound_Throws()
        {
            Assert.Throws<System.ArgumentException>(() =>
                SpatialHelper.BoundsInfo("/NoSuchObject999"));
        }

        [Test]
        public void Execute_Dispatch_NearestAction()
        {
            // Test Execute() method dispatches correctly
            var argsJson = "{\"action\":\"nearest\",\"path\":\"/SpatialPlayer\",\"component\":\"Rigidbody\"}";
            var result = SpatialHelper.Execute(argsJson);
            StringAssert.Contains("SpatialEnemy", result);
        }

        [Test]
        public void Execute_InvalidAction_Throws()
        {
            var argsJson = "{\"action\":\"unknown\",\"path\":\"/SpatialPlayer\"}";
            Assert.Throws<System.ArgumentException>(() => SpatialHelper.Execute(argsJson));
        }

        // --- LayoutValidator ---

        [Test]
        public void Validate_NoColliders_ReturnsOK()
        {
            var empty = new GameObject("EmptyRoot");
            try
            {
                var result = LayoutValidator.Validate("/EmptyRoot", 3f);
                StringAssert.Contains("0 triggers, 0 solids", result);
                StringAssert.Contains("OK", result);
            }
            finally
            {
                Object.DestroyImmediate(empty);
            }
        }

        [Test]
        public void Validate_TriggersFarApart_ReturnsOK()
        {
            // Move Trigger2 far away
            _root.transform.Find("Trigger2").position = new Vector3(100, 0, 0);

            var result = LayoutValidator.Validate("/LayoutTestRoot", 3f);
            StringAssert.Contains("OK: no trigger overlaps", result);
        }

        [Test]
        public void Validate_TriggersClose_ReturnsWarning()
        {
            // Trigger1 at (0,0,0), Trigger2 at (1,0,0) — dist=1 < minDistance=3
            var result = LayoutValidator.Validate("/LayoutTestRoot", 3f);
            StringAssert.Contains("WARNING", result);
            // Should report the distance
            Assert.That(result, Does.Contain("dist="));
        }

        [Test]
        public void Validate_NonExistentRoot_ReturnsError()
        {
            var result = LayoutValidator.Validate("/NoSuchRoot999", 3f);
            Assert.IsNotNull(result);
            Assert.IsNotEmpty(result);
            // ErrorHelper.ObjectNotFound returns an error string
            Assert.That(result.ToLower(), Does.Contain("not found").Or.Contain("nosuchroot999").IgnoreCase);
        }

        [Test]
        public void Validate_MixedSolidsAndTriggers_CountsCorrectly()
        {
            // Setup has 2 triggers + 1 solid
            var result = LayoutValidator.Validate("/LayoutTestRoot", 3f);
            StringAssert.Contains("2 triggers, 1 solids", result);
        }

        [Test]
        public void GetSpatialContext_ReturnsPositionAndColliders()
        {
            // The root itself has no collider, use a child with one
            var result = LayoutValidator.GetSpatialContext("/LayoutTestRoot/Trigger1", 5f);
            StringAssert.Contains("Position:", result);
        }

        [Test]
        public void GetSpatialContext_NonPlayMode_ApproachNA()
        {
            Assert.IsFalse(EditorApplication.isPlaying, "This test requires EditMode");

            var result = LayoutValidator.GetSpatialContext("/LayoutTestRoot/Trigger1", 5f);
            StringAssert.Contains("Approach vectors:", result);
        }

        [Test]
        public void GetSpatialContext_NonExistentObject_ReturnsError()
        {
            var result = LayoutValidator.GetSpatialContext("/NoSuchObject999", 5f);
            Assert.IsNotNull(result);
            Assert.IsNotEmpty(result);
            Assert.That(result.ToLower(), Does.Contain("not found").Or.Contain("nosuchobject999").IgnoreCase);
        }
    }
}
