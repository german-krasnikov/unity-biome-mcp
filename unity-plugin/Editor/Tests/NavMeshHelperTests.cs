// TDD — RED first. EditMode NUnit tests for NavMeshHelper bake/status/clear actions.
// Run in Unity Test Runner → EditMode.
#if UNITY_MODULE_AI || UNITY_AI_NAVIGATION
using System;
using NUnit.Framework;
using UnityEngine.AI;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal class NavMeshHelperTests
    {
        [TearDown]
        public void TearDown()
        {
            // Clear any NavMesh data baked during tests
            NavMesh.RemoveAllNavMeshData();
        }

        [Test]
        public void Bake_IsValidAction()
        {
            var result = NavMeshHelper.Execute("{\"action\":\"bake\"}");
            Assert.That(result, Does.Not.StartWith("ERR unknown navmesh action"));
            Assert.That(result, Does.StartWith("baked"));
        }

        [Test]
        public void Status_ReturnsTriangulationData()
        {
            var result = NavMeshHelper.Execute("{\"action\":\"status\"}");
            Assert.That(result, Does.Contain("triangles:"));
            Assert.That(result, Does.Contain("vertices:"));
            Assert.That(result, Does.Contain("areas:"));
        }

        [Test]
        public void Clear_IsValidAction()
        {
            var result = NavMeshHelper.Execute("{\"action\":\"clear\"}");
            Assert.That(result, Does.Not.StartWith("ERR unknown navmesh action"));
            Assert.AreEqual("cleared", result);
        }

        [Test]
        public void Execute_UnknownAction_Throws()
        {
            Assert.Throws<ArgumentException>(() => NavMeshHelper.Execute("{\"action\":\"nonsense\"}"));
        }

        [Test]
        public void ExistingActions_StillWork()
        {
            // Verify existing actions are not broken by the new additions.
            var sample = NavMeshHelper.Execute("{\"action\":\"sample\",\"center\":\"0,0,0\"}");
            Assert.That(sample, Does.Not.StartWith("ERR"));

            var raycast = NavMeshHelper.Execute("{\"action\":\"raycast\",\"from\":\"0,0,0\",\"to\":\"1,0,1\"}");
            Assert.That(raycast, Does.Not.StartWith("ERR"));
        }
    }
}
#endif
