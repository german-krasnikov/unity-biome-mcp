// SceneGoDragTests — covers ProcessDraggedObject's scene-GO branch (first branch in Chips.cs:L43).
// Gap: zero prior tests exercise 'obj is GameObject go && !AssetDatabase.Contains(go)'.
// No window needed — ProcessDraggedObject is a static method.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityMCP.Editor.Chat;
using Object = UnityEngine.Object;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class SceneGoDragTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private List<(Object obj, string path, string name)> _chips;

        [SetUp]
        public void SetUp()
        {
            ChipKindRegistry.ResetToBuiltIns();
            ChipPillFactory.ColorResolver = null;
            _chips = new List<(Object, string, string)>();
        }

        [TearDown]
        public void TearDown()
        {
            ChipKindRegistry.ResetToBuiltIns();
            ChipPillFactory.ColorResolver = null;
        }

        private void Capture(Object o, string p, string n) => _chips.Add((o, p, n));

        // A1 — root GO produces exactly one chip with the GO as its object
        [Test]
        public void SceneGO_RootObject_InsertsSingleChip()
        {
            var go = new GameObject("TestRoot");
            try
            {
                MCPChatWindow.ProcessDraggedObject(go, null, Capture);
                Assert.AreEqual(1, _chips.Count);
                Assert.AreEqual(go, _chips[0].obj);
            }
            finally { Object.DestroyImmediate(go); }
        }

        // A2 — chip path matches ComponentSerializer.GetPath
        [Test]
        public void SceneGO_ChipPath_MatchesGetPath()
        {
            var go = new GameObject("PathTarget");
            try
            {
                MCPChatWindow.ProcessDraggedObject(go, null, Capture);
                Assert.AreEqual(ComponentSerializer.GetPath(go), _chips[0].path);
            }
            finally { Object.DestroyImmediate(go); }
        }

        // A3 — chip display name matches go.name
        [Test]
        public void SceneGO_ChipName_MatchesGoName()
        {
            var go = new GameObject("DisplayName");
            try
            {
                MCPChatWindow.ProcessDraggedObject(go, null, Capture);
                Assert.AreEqual("DisplayName", _chips[0].name);
            }
            finally { Object.DestroyImmediate(go); }
        }

        // A4 — child GO path contains slash separator and leaf name
        [Test]
        public void SceneGO_ChildObject_PathContainsSlash()
        {
            var parent = new GameObject("Parent");
            var child  = new GameObject("Child");
            child.transform.SetParent(parent.transform);
            try
            {
                MCPChatWindow.ProcessDraggedObject(child, null, Capture);
                StringAssert.Contains("/", _chips[0].path);
                StringAssert.Contains("Child", _chips[0].path);
            }
            finally { Object.DestroyImmediate(parent); }
        }

        // A5 — dragging two GOs produces two chips
        [Test]
        public void SceneGO_TwoObjects_TwoChips()
        {
            var go1 = new GameObject("A");
            var go2 = new GameObject("B");
            try
            {
                MCPChatWindow.ProcessDraggedObject(go1, null, Capture);
                MCPChatWindow.ProcessDraggedObject(go2, null, Capture);
                Assert.AreEqual(2, _chips.Count);
            }
            finally { Object.DestroyImmediate(go1); Object.DestroyImmediate(go2); }
        }

        // A6 — two different GOs produce different paths
        [Test]
        public void SceneGO_TwoObjects_DifferentPaths()
        {
            var go1 = new GameObject("X");
            var go2 = new GameObject("Y");
            try
            {
                MCPChatWindow.ProcessDraggedObject(go1, null, Capture);
                MCPChatWindow.ProcessDraggedObject(go2, null, Capture);
                Assert.AreNotEqual(_chips[0].path, _chips[1].path);
            }
            finally { Object.DestroyImmediate(go1); Object.DestroyImmediate(go2); }
        }

        // A7 — unicode GO name does not throw; chip is produced
        [Test]
        public void SceneGO_UnicodeName_NoException()
        {
            var go = new GameObject("Объект日本語");
            try
            {
                Assert.DoesNotThrow(() => MCPChatWindow.ProcessDraggedObject(go, null, Capture));
                Assert.AreEqual(1, _chips.Count);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
