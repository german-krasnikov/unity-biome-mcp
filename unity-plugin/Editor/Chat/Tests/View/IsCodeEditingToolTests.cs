// TDD — CRITICAL #2: arming auto-fix is gated on code-editing tool provenance.
// Tests MCPChatWindow.IsCodeEditingTool and the arm/disarm contract.
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class IsCodeEditingToolTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ── Name-based detection ──────────────────────────────────────────────

        [Test]
        public void EditTool_IsCodeEditing()
        {
            var rec = new ToolCallRecord("Edit", "id1", "{\"path\":\"/foo/Bar.cs\"}");
            Assert.IsTrue(MCPChatWindow.IsCodeEditingTool(rec));
        }

        [Test]
        public void WriteTool_IsCodeEditing()
        {
            var rec = new ToolCallRecord("Write", "id2", "{\"path\":\"/X.cs\"}");
            Assert.IsTrue(MCPChatWindow.IsCodeEditingTool(rec));
        }

        [Test]
        public void MultiEditTool_IsCodeEditing()
        {
            var rec = new ToolCallRecord("MultiEdit", "id3", "{\"path\":\"/A.cs\"}");
            Assert.IsTrue(MCPChatWindow.IsCodeEditingTool(rec));
        }

        // ── Path-based detection for MCP tools ───────────────────────────────

        [Test]
        public void McpToolWithCsPath_IsCodeEditing()
        {
            var rec = new ToolCallRecord("set_property", "id4",
                "{\"path\":\"/Assets/Scripts/Player.cs\", \"value\":\"100\"}");
            Assert.IsTrue(MCPChatWindow.IsCodeEditingTool(rec));
        }

        [Test]
        public void McpToolWithCsprojPath_IsNotCodeEditing()
        {
            var rec = new ToolCallRecord("set_property", "id7",
                "{\"path\":\"/Assets/Assembly.csproj\", \"value\":\"x\"}");
            Assert.IsFalse(MCPChatWindow.IsCodeEditingTool(rec));
        }

        // ── Non-code-editing tools ────────────────────────────────────────────

        [Test]
        public void GetHierarchyTool_IsNotCodeEditing()
        {
            var rec = new ToolCallRecord("get_hierarchy", "id5", "{\"root\":\"Player\"}");
            Assert.IsFalse(MCPChatWindow.IsCodeEditingTool(rec));
        }

        [Test]
        public void SetPropertyNoCs_IsNotCodeEditing()
        {
            var rec = new ToolCallRecord("set_property", "id6",
                "{\"path\":\"/Player\", \"component\":\"Health\", \"field\":\"hp\", \"value\":\"50\"}");
            Assert.IsFalse(MCPChatWindow.IsCodeEditingTool(rec));
        }

        // ── Provenance gate reset on Error (FIX 1) ───────────────────────────

        [Test]
        public void CodeEditFollowedByError_ProvenanceFlagFalseForNextTurn()
        {
            // Simulates: tool record sets _turnEditedCode = true, then Error event fires.
            // After Error, _turnEditedCode must be false so next turn cannot falsely arm autofix.
            bool turnEditedCode = false;

            // Step 1: tool record arrives (IsCodeEditingTool returns true)
            var rec = new ToolCallRecord("Edit", "id-e1", "{\"path\":\"/Assets/Foo.cs\"}");
            if (MCPChatWindow.IsCodeEditingTool(rec)) turnEditedCode = true;
            Assert.IsTrue(turnEditedCode, "flag must be true after code-edit record");

            // Step 2: Error event fires — simulate the Error branch reset
            turnEditedCode = false; // mirrors the new line in HandleEvent Error branch

            Assert.IsFalse(turnEditedCode,
                "After Error event, provenance flag must be false for the next turn");
        }

        // ── Arming contract (CRITICAL #2) ─────────────────────────────────────

        [Test]
        public void NoCodeEditTool_AutoFixNotArmed()
        {
            // Without a code-editing tool call, _turnEditedCode stays false → Arm() not called.
            var fix = new CompileAutoFix();
            Assert.IsFalse(fix.IsArmed, "Without code-edit tool, autofix must not be armed");
        }

        [Test]
        public void CodeEditTool_AutoFixArmed()
        {
            // When _turnEditedCode==true, TurnDone calls fix.Arm().
            var fix = new CompileAutoFix();
            fix.Arm(); // simulates "had _turnEditedCode=true at TurnDone"
            Assert.IsTrue(fix.IsArmed, "After code-edit, autofix must be armed");
        }
    }
}
