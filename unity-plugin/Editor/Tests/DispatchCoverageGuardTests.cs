// dsl-bottlenecks-2026-11 S3: per-arm dispatch coverage of a DISPATCH tool (Generated/ToolDispatch.g.cs)
// is otherwise thin -- the unknown-action oracle only proves the default arm. This proves every
// declared ACTION reaches ITS OWN method instead of the unknown-action path: a normal result or
// the method's own argument-validation error both count as "dispatched"; only the literal
// "Unknown action" text (ErrorHelper.InvalidAction) counts as a miss.
//
// Calls with minimal (action-only) args. Every arm of the tools covered here validates its own
// required field(s) before touching any asset (confirmed by reading each arm: AI/testing.md bars a
// guard test from creating/modifying a project asset outside Assets/TestsTemp), so this never
// performs an AssetDatabase write.
using NUnit.Framework;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class DispatchCoverageGuardTests : UnityMcpTestBase
    {
        [SetUp]
        public void SetUp()
        {
            CommandRouter.RegisterAll();
        }

        private static void AssertEveryActionDispatches(string tool)
        {
            foreach (var action in ToolActions.Names(tool))
            {
                var argsJson = "{\"action\":\"" + action + "\"}";
                try
                {
                    CommandRouter.ExecuteCommand(tool, argsJson);
                }
                catch (System.Exception ex)
                {
                    StringAssert.DoesNotStartWith("Unknown action", ex.Message,
                        $"{tool}.{action} hit the unknown-action path instead of its own method");
                }
            }
        }

        [Test]
        public void Prefab_EveryDeclaredAction_DispatchesToItsOwnMethod() =>
            AssertEveryActionDispatches("prefab");
    }
}
