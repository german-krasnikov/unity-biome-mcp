// TDD — GameStateHelper.Snapshot: 2-part shorthand, multi-query, and error branches.
// Tasks 1 & 2: covers activeSelf/activeInHierarchy/tag/layer/name shorthands,
// unknown shorthand, missing path, multi-query, partial failure, null/empty queries.
using System;
using NUnit.Framework;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal sealed class GameStateHelperSnapshotTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private GameObject _go;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _go = TrackOwnedObject(new GameObject("GSH_Test"));
            _path = ComponentSerializer.GetPath(_go);
        }

        // ── 2-part shorthands ──

        [Test]
        public void Snapshot_ActiveSelf_WhenActive_ReturnsTrue()
        {
            _go.SetActive(true);
            var result = GameStateHelper.Snapshot($"{_path}|activeSelf");
            StringAssert.Contains("activeSelf=true", result);
        }

        [Test]
        public void Snapshot_ActiveSelf_WhenInactive_ReturnsFalse()
        {
            _go.SetActive(false);
            var result = GameStateHelper.Snapshot($"{_path}|activeSelf");
            StringAssert.Contains("activeSelf=false", result);
        }

        [Test]
        public void Snapshot_ActiveInHierarchy_WhenParentInactive_ReturnsFalse()
        {
            var parent = TrackOwnedObject(new GameObject("GSH_Parent"));
            var child = TrackOwnedObject(new GameObject("GSH_Child"));
            child.transform.SetParent(parent.transform);
            parent.SetActive(false);
            var childPath = ComponentSerializer.GetPath(child);

            var result = GameStateHelper.Snapshot($"{childPath}|activeInHierarchy");

            StringAssert.Contains("activeInHierarchy=false", result);
        }

        [Test]
        public void Snapshot_Tag_ReturnsTagName()
        {
            // Default tag is "Untagged"
            var result = GameStateHelper.Snapshot($"{_path}|tag");
            StringAssert.Contains("tag=Untagged", result);
        }

        [Test]
        public void Snapshot_Layer_ReturnsLayerIndex()
        {
            _go.layer = 3;
            var result = GameStateHelper.Snapshot($"{_path}|layer");
            StringAssert.Contains("layer=3", result);
        }

        [Test]
        public void Snapshot_Name_ReturnsObjectName()
        {
            var result = GameStateHelper.Snapshot($"{_path}|name");
            StringAssert.Contains("name=GSH_Test", result);
        }

        [Test]
        public void Snapshot_UnknownShorthand_ReturnsUnknownShorthandError()
        {
            var result = GameStateHelper.Snapshot($"{_path}|unknownField");
            StringAssert.Contains("ERR:", result);
            StringAssert.Contains("unknown shorthand", result);
        }

        [Test]
        public void Snapshot_NonExistentPath_TwoPart_ReturnsObjectNotFoundError()
        {
            var result = GameStateHelper.Snapshot("/NonExistentObject_GSH_XYZ|activeSelf");
            StringAssert.Contains("ERR:object not found", result);
        }

        // ── 3-part path: component not found ──

        [Test]
        public void Snapshot_ThreePart_ComponentNotFound_ReturnsError()
        {
            var result = GameStateHelper.Snapshot($"{_path}|NoSuchComp|someField");
            StringAssert.Contains("ERR:component not found", result);
        }

        // ── Error branches ──

        [Test]
        public void Snapshot_EmptyQuery_ReturnsEmptyString()
        {
            var result = GameStateHelper.Snapshot("");
            Assert.AreEqual("", result);
        }

        [Test]
        public void Snapshot_NullQuery_ThrowsNullReferenceException()
        {
            Assert.Throws<NullReferenceException>(() => GameStateHelper.Snapshot(null));
        }

        [Test]
        public void Snapshot_OnePart_NoPipe_ReturnsFormatError()
        {
            // Single item with no '|' has only 1 part → "need path|component|field" error
            var result = GameStateHelper.Snapshot("/SomePath");
            StringAssert.Contains("ERR: need path|component|field", result);
        }

        // ── Multi-query (comma-separated) ──

        [Test]
        public void Snapshot_MultipleItems_ReturnsAllValuesOnSeparateLines()
        {
            var go2 = TrackOwnedObject(new GameObject("GSH_Multi2"));
            var path2 = ComponentSerializer.GetPath(go2);

            var result = GameStateHelper.Snapshot($"{_path}|name,{path2}|name");

            var lines = result.Split('\n');
            Assert.AreEqual(2, lines.Length);
            StringAssert.Contains("GSH_Test", lines[0]);
            StringAssert.Contains("GSH_Multi2", lines[1]);
        }

        [Test]
        public void Snapshot_MultiQuery_PartialFailure_IncludesErrorForBadPath()
        {
            var result = GameStateHelper.Snapshot(
                $"{_path}|name,/NONEXISTENT_GSH_ABC|activeSelf");

            var lines = result.Split('\n');
            Assert.AreEqual(2, lines.Length);
            StringAssert.Contains("name=GSH_Test", lines[0]);
            StringAssert.Contains("ERR:object not found", lines[1]);
        }
    }
}
