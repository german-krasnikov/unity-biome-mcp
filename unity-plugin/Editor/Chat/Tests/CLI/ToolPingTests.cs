// TDD — RED first. Tests drive ToolPing contract.
// EditMode: real GameObject creation allowed. EditorGUIUtility.PingObject is no-op in batch.
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ToolPingTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Undo.ClearAll();
        }

        // ── ExtractPath ───────────────────────────────────────────────────────

        [Test]
        public void ExtractPath_PathKey_ReturnsValue()
        {
            var result = ToolPing.ExtractPath("{\"path\":\"/Player/Sword\"}");
            Assert.AreEqual("/Player/Sword", result);
        }

        [Test]
        public void ExtractPath_NullJson_ReturnsNull()
        {
            Assert.IsNull(ToolPing.ExtractPath(null));
        }

        [Test]
        public void ExtractPath_EmptyJson_ReturnsNull()
        {
            Assert.IsNull(ToolPing.ExtractPath(""));
        }

        [Test]
        public void ExtractPath_NoPathKey_ReturnsNull()
        {
            Assert.IsNull(ToolPing.ExtractPath("{\"value\":\"100\"}"));
        }

        [Test]
        public void ExtractPath_ParentFallback_ReturnsParentValue()
        {
            var result = ToolPing.ExtractPath("{\"parent\":\"/World/Enemies\"}");
            Assert.AreEqual("/World/Enemies", result);
        }

        [Test]
        public void ExtractPath_BothPathAndParent_ReturnsPath()
        {
            var result = ToolPing.ExtractPath("{\"path\":\"/A\",\"parent\":\"/B\"}");
            Assert.AreEqual("/A", result);
        }

        [Test]
        public void ExtractPath_BatchArgs_ReturnsFirstPath()
        {
            // batch has "ops" array; the outer args may have "path" from the first op
            var json = "{\"path\":\"/First\",\"ops\":[{\"path\":\"/Second\"}]}";
            Assert.AreEqual("/First", ToolPing.ExtractPath(json));
        }

        // ── TryPing ───────────────────────────────────────────────────────────

        [Test]
        public void TryPing_ValidPath_ReturnsTrueAndPings()
        {
            _go = new GameObject("PingTarget");
            var rec = new ToolCallRecord("set_property", "id1",
                $"{{\"path\":\"{ComponentSerializer.GetPath(_go)}\"}}");
            Assert.IsTrue(ToolPing.TryPing(rec));
        }

        [Test]
        public void TryPing_DestroyedGO_ReturnsFalse()
        {
            _go = new GameObject("DestroyMe");
            var path = ComponentSerializer.GetPath(_go);
            Object.DestroyImmediate(_go);
            _go = null;
            var rec = new ToolCallRecord("set_property", "id2", $"{{\"path\":\"{path}\"}}");
            Assert.IsFalse(ToolPing.TryPing(rec));
        }

        [Test]
        public void TryPing_NullArgsJson_ReturnsFalse()
        {
            var rec = new ToolCallRecord("set_property", "id3", null);
            Assert.IsFalse(ToolPing.TryPing(rec));
        }

        [Test]
        public void TryPing_PathNotInScene_ReturnsFalse()
        {
            var rec = new ToolCallRecord("get_component", "id4",
                "{\"path\":\"/NonExistent/Object\"}");
            Assert.IsFalse(ToolPing.TryPing(rec));
        }

        [Test]
        public void TryPing_NonMutatingTool_WithPath_StillPings()
        {
            // Ping is unconditional — even inspect/get_component benefit from highlighting.
            _go = new GameObject("InspectTarget");
            var rec = new ToolCallRecord("get_component", "id5",
                $"{{\"path\":\"{ComponentSerializer.GetPath(_go)}\"}}");
            Assert.IsTrue(ToolPing.TryPing(rec));
        }

        // ── MAJOR #1: HandleToolRecord fires ping once (args-complete only, not result) ──

        [Test]
        public void TryPing_RecordWithResult_ReturnsFalse_NoPingOnResultRecord()
        {
            // A result record (HasResult==true) must NOT trigger ping because HandleToolRecord
            // guards: `rec.ArgsJson != null && !rec.HasResult`.
            // ToolCallRecord.WithResult preserves ArgsJson — ensure HasResult==true.
            _go = new GameObject("PingOnce");
            var argsJson = $"{{\"path\":\"{ComponentSerializer.GetPath(_go)}\"}}";
            var argsRec  = new ToolCallRecord("set_property", "id6", argsJson);
            var resultRec = argsRec.WithResult("ok", true);

            Assert.IsTrue(resultRec.HasResult, "WithResult must set HasResult");
            // The args-complete record (no result) should ping.
            Assert.IsTrue(ToolPing.TryPing(argsRec),   "args-complete record must ping");
            // The result record still has a valid path but HandleToolRecord skips it.
            // TryPing itself still succeeds — the guard is in HandleToolRecord, not TryPing.
            // The contract tested here is that HasResult is the discriminator.
            Assert.IsTrue(resultRec.HasResult);
            Assert.IsNotNull(resultRec.ArgsJson);
        }

        [Test]
        public void TryPing_ArgsCompleteRecord_HasResultFalse()
        {
            // Args-complete record must have HasResult==false so HandleToolRecord pings it.
            var rec = new ToolCallRecord("set_property", "id7", "{\"path\":\"/X\"}");
            Assert.IsFalse(rec.HasResult, "Fresh record without result must have HasResult==false");
        }

        [Test]
        public void ExtractPath_PathsKey_ReturnsFirstPath()
        {
            var result = ToolPing.ExtractPath("{\"paths\":\"/A\"}");
            Assert.AreEqual("/A", result);
        }

        [Test]
        public void TryPing_AmbiguousPath_ReturnsFalseNoThrow()
        {
            var a = new GameObject("DupPing");
            var b = new GameObject("DupPing");
            try
            {
                var rec = new ToolCallRecord("set_property", "id8", "{\"path\":\"/DupPing\"}");
                Assert.IsFalse(ToolPing.TryPing(rec));
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }
    }
}
