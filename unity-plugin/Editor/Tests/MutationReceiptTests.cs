// T15 TDD — MutationReceipt struct, FormatResponseWithReceipt, PendReceipt seam tests.
using NUnit.Framework;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class MutationReceiptTests : SceneTestBase
    {
        // ── MutationReceipt.ToJson ────────────────────────────────────────────

        [Test]
        public void ReceiptToJson_AllFields_ContainsReceiptKey()
        {
            var r = new MutationReceipt
            {
                Path = "/Player", Op = "modify", TargetType = "property",
                Prop = "Health", Before = "100", After = "50", Reversible = true
            };
            var json = r.ToJson();
            StringAssert.StartsWith("\"receipt\":{", json);
            StringAssert.Contains("\"path\":\"/Player\"", json);
            StringAssert.Contains("\"prop\":\"Health\"", json);
            StringAssert.Contains("\"before\":\"100\"", json);
            StringAssert.Contains("\"after\":\"50\"", json);
            StringAssert.Contains("\"rev\":true", json);
        }

        [Test]
        public void ReceiptToJson_NullBefore_OmitsBefore()
        {
            var r = new MutationReceipt
            {
                Path = "/Obj", Op = "create", TargetType = "scene_object",
                Before = null, After = null, Reversible = true
            };
            var json = r.ToJson();
            StringAssert.DoesNotContain("\"before\"", json);
        }

        [Test]
        public void ReceiptToJson_NullAfter_OmitsAfter()
        {
            var r = new MutationReceipt
            {
                Path = "/Obj", Op = "delete", TargetType = "scene_object",
                Before = null, After = null, Reversible = true
            };
            var json = r.ToJson();
            StringAssert.DoesNotContain("\"after\"", json);
        }

        [Test]
        public void ReceiptToJson_CapsLongValues()
        {
            var longValue = new string('x', 600);
            var r = new MutationReceipt
            {
                Path = longValue, Op = "modify", TargetType = "property",
                Reversible = true
            };
            var json = r.ToJson();
            // Path capped to 512 chars + quotes + key + other chars — total under 1200
            Assert.That(json.Length, Is.LessThan(1200));
            // Actual cap: the x-string should appear truncated
            StringAssert.DoesNotContain(new string('x', 513), json);
        }

        [Test]
        public void ReceiptToJson_EscapesQuotesInPath()
        {
            var r = new MutationReceipt
            {
                Path = "say \"hello\"", Op = "modify", TargetType = "property",
                Reversible = true
            };
            var json = r.ToJson();
            StringAssert.Contains("\\\"", json);
        }

        // ── FormatResponseWithReceipt ─────────────────────────────────────────

        [Test]
        public void FormatResponseWithReceipt_NullReceipt_SameAsBase()
        {
            var base_ = JsonHelper.FormatResponse("id-1", true, "data", null);
            var result = JsonHelper.FormatResponseWithReceipt("id-1", true, "data", null, null);
            Assert.That(result, Is.EqualTo(base_));
        }

        [Test]
        public void FormatResponseWithReceipt_EmptyReceipt_SameAsBase()
        {
            var base_ = JsonHelper.FormatResponse("id-1", true, "data", null);
            var result = JsonHelper.FormatResponseWithReceipt("id-1", true, "data", null, "");
            Assert.That(result, Is.EqualTo(base_));
        }

        [Test]
        public void FormatResponseWithReceipt_WithReceipt_InjectsBeforeClosingBrace()
        {
            var receipt = "\"receipt\":{\"path\":\"/P\"}";
            var result = JsonHelper.FormatResponseWithReceipt("id-1", true, "data", null, receipt);
            // Must end with the receipt + closing brace
            StringAssert.EndsWith("," + receipt + "}", result);
            // Must be valid: starts with opening brace
            StringAssert.StartsWith("{", result);
        }

        // ── BuildResponse + PendReceipt ───────────────────────────────────────

        [Test]
        public void BuildResponse_AfterPendReceipt_IncludesReceiptJson()
        {
            CommandRouter.PendReceipt(new MutationReceipt
            {
                Path = "/Player", Op = "modify", TargetType = "property",
                Prop = "Health", Before = "100", After = "50", Reversible = true
            });
            var response = CommandRouter.BuildResponse("id-1", "Health = 50 (was 100)");
            StringAssert.Contains("\"receipt\":", response);
            StringAssert.Contains("\"/Player\"", response);
        }

        [Test]
        public void BuildResponse_ClearsPendingAfterConsume()
        {
            CommandRouter.PendReceipt(new MutationReceipt
            {
                Path = "/P", Op = "modify", TargetType = "property", Reversible = true
            });
            CommandRouter.BuildResponse("id-1", "first");
            // Second call — no PendReceipt between — must NOT include receipt
            var second = CommandRouter.BuildResponse("id-2", "second");
            StringAssert.DoesNotContain("\"receipt\":", second);
        }

        // ── Handler seam tests — receipt appears in Process() response ────────

        [Test]
        public void ExecSetProperty_PendsReceipt_WithPathAndProp()
        {
            CommandRouter.RegisterAll();
            var go = new GameObject("MRT_TestObj");
            TrackOwnedObject(go);
            go.AddComponent<BoxCollider>();
            var response = CommandRouter.Process(
                "{\"id\":\"t1\",\"cmd\":\"set_property\",\"args\":{" +
                "\"path\":\"/MRT_TestObj\",\"component\":\"BoxCollider\",\"prop\":\"enabled\",\"value\":\"false\"}}");
            StringAssert.Contains("\"receipt\":", response);
            StringAssert.Contains("\"path\"", response);
            StringAssert.Contains("\"prop\"", response);
        }

        [Test]
        public void ExecCreateObject_PendsReceipt_OpCreate()
        {
            CommandRouter.RegisterAll();
            var response = CommandRouter.Process(
                "{\"id\":\"t2\",\"cmd\":\"create_object\",\"args\":{\"name\":\"MRT_Created\"}}");
            StringAssert.Contains("\"receipt\":", response);
            StringAssert.Contains("\"op\":\"create\"", response);
        }

        [Test]
        public void ExecDeleteObject_PendsReceipt_OpDelete()
        {
            CommandRouter.RegisterAll();
            var go = new GameObject("MRT_ToDelete");
            TrackOwnedObject(go);
            var response = CommandRouter.Process(
                "{\"id\":\"t3\",\"cmd\":\"delete_object\",\"args\":{\"path\":\"/MRT_ToDelete\"}}");
            StringAssert.Contains("\"receipt\":", response);
            StringAssert.Contains("\"op\":\"delete\"", response);
        }
    }
}
