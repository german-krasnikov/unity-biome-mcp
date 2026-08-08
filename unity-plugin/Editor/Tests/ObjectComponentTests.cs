// TDD — EditMode tests for create_object edge cases.
// Run in Unity Test Runner → EditMode.
using System;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ObjectComponentTests : SceneTestBase
    {

        // ── CreateObject with duplicate name ─────────────────────────────────

        [Test]
        [RequiresReadWrite("creates GameObjects in the scene")]
        public void CreateObjectWithDuplicateName_ReturnsCreatedResponse()
        {
            // Arrange — create first object so a duplicate exists
            var first = new GameObject("DupObj");

            // Act — create second via CommandRouter (exercises ExecCreateObject path)
            var response = CommandRouter.Process(
                "{\"id\":\"t1\",\"cmd\":\"create_object\",\"args\":{\"name\":\"DupObj\"}}");

            // Assert — response must report success, not an ambiguity error
            StringAssert.Contains("Created", response, "Expected 'Created' in response; got: " + response);
            StringAssert.DoesNotContain("Ambiguous", response);
            StringAssert.DoesNotContain("\"ok\":false", response);

            UnityEngine.Object.DestroyImmediate(first);
        }

        // ── Strategy C: ReadOnly verification ────────────────────────────────

        [Test]
        public void CreateObject_WhenReadOnly_IsBlocked()
        {
            var orig = CommandRouter.IsReadOnly;
            CommandRouter.IsReadOnly = () => true;
            try
            {
                var result = CommandRouter.Process(
                    "{\"id\":\"ro-oc1\",\"cmd\":\"create_object\",\"args\":{\"name\":\"ROVerify\"}}");
                StringAssert.Contains("READ_ONLY_BLOCKED", result);
            }
            finally
            {
                CommandRouter.IsReadOnly = orig;
            }
        }
    }
}
