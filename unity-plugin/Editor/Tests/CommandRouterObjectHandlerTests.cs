// TDD: ExecInspect + ApplyFieldsCompress integration — EditMode.
// Area 1, Task 1 (ExecInspect paths/find_type) + Task 2 (fields/compress routing).
using NUnit.Framework;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class CommandRouterObjectHandlerTests : SceneTestBase
    {
        [SetUp]
        public void SetUp()
        {
            RegisterCleanup(() => CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling);
            RegisterCleanup(() => CommandRouter.IsPlayMode = () => UnityEditor.EditorApplication.isPlaying);
            CommandRouter.IsCompiling = () => false;
            CommandRouter.IsPlayMode = () => false;
        }

        // ── Task 1: ExecInspect ───────────────────────────────────────────────

        [Test]
        public void Inspect_ValidPath_NoComponentFilter_ReturnsTransformData()
        {
            // Arrange
            var go = TrackOwnedObject(new GameObject("InspectValid_T1"));

            // Act
            var result = CommandRouter.Process(
                "{\"id\":\"oh1\",\"cmd\":\"inspect\",\"args\":{\"paths\":\"/InspectValid_T1\"}}");

            // Assert: response ok and data contains Transform info
            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("Transform", result);
            StringAssert.Contains("InspectValid_T1", result);
        }

        [Test]
        public void Inspect_NonExistentPath_ReturnsNotFoundInData()
        {
            // Act: inspect path that does not exist in scene
            var result = CommandRouter.Process(
                "{\"id\":\"oh2\",\"cmd\":\"inspect\",\"args\":{\"paths\":\"/NoSuchObject_XYZ42\"}}");

            // Assert: overall ok:true (handler wrote error line to output, not exception)
            StringAssert.Contains("\"ok\":true", result);
            // Error text about the missing path appears in the data field
            StringAssert.Contains("NoSuchObject_XYZ42", result);
            StringAssert.Contains("not found", result);
        }

        [Test]
        public void Inspect_FindType_BoxCollider_ReturnsObjectsHavingThatComponent()
        {
            // Arrange: object with BoxCollider
            var go = TrackOwnedObject(new GameObject("InspectFindType_T3"));
            go.AddComponent<BoxCollider>();

            // Act: find_type searches by component type, no paths given
            var result = CommandRouter.Process(
                "{\"id\":\"oh3\",\"cmd\":\"inspect\",\"args\":{\"find_type\":\"BoxCollider\"}}");

            // Assert: response references the object we created
            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("InspectFindType_T3", result);
        }

        // ── Task 2: ApplyFieldsCompress ───────────────────────────────────────

        [Test]
        public void Inspect_WithFields_LimitsOutputToRequestedField()
        {
            // Arrange
            var go = TrackOwnedObject(new GameObject("InspectFields_T4"));

            // Act: request only m_LocalPosition field
            var result = CommandRouter.Process(
                "{\"id\":\"oh4\",\"cmd\":\"inspect\"," +
                "\"args\":{\"paths\":\"/InspectFields_T4\",\"fields\":\"m_LocalPosition\"}}");

            // Assert: position data present; rotation data absent
            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("m_LocalPosition", result);
            StringAssert.DoesNotContain("m_LocalRotation", result);
        }

        [Test]
        public void Inspect_WithCompress_StripsDefaultZeroValues()
        {
            // Arrange: fresh GO has position x=0 — DefaultStripper removes lines with value "0"
            var go = TrackOwnedObject(new GameObject("InspectCompress_T5"));

            // Baseline: without compress, the zero position field is present
            var raw = CommandRouter.Process(
                "{\"id\":\"oh5a\",\"cmd\":\"inspect\"," +
                "\"args\":{\"paths\":\"/InspectCompress_T5\"}}");
            StringAssert.Contains("m_LocalPosition: (0, 0, 0)", raw);

            // Act: with compress=true the zero-value line is stripped
            var result = CommandRouter.Process(
                "{\"id\":\"oh5b\",\"cmd\":\"inspect\"," +
                "\"args\":{\"paths\":\"/InspectCompress_T5\",\"compress\":\"true\"}}");

            StringAssert.Contains("\"ok\":true", result);
            StringAssert.DoesNotContain("m_LocalPosition: (0, 0, 0)", result);
        }

        [Test]
        public void Inspect_FieldsAndCompressBoth_FieldsTakesPriority()
        {
            // When both fields and compress are present, FieldProjector runs (not DefaultStripper).
            // Verify by checking that the output contains only the requested field name
            // and not wholesale stripping behavior.
            var go = TrackOwnedObject(new GameObject("InspectBoth_T6"));

            var result = CommandRouter.Process(
                "{\"id\":\"oh6\",\"cmd\":\"inspect\"," +
                "\"args\":{\"paths\":\"/InspectBoth_T6\"," +
                "\"fields\":\"m_LocalPosition\",\"compress\":\"true\"}}");

            StringAssert.Contains("\"ok\":true", result);
            // Fields projection: position key present
            StringAssert.Contains("m_LocalPosition", result);
            // Rotation is neither requested nor should appear (projection, not stripping)
            StringAssert.DoesNotContain("m_LocalRotation", result);
        }

        [Test]
        public void Inspect_NoFieldsNoCompress_ReturnsFullComponentOutput()
        {
            // Without either modifier the full component data is returned
            var go = TrackOwnedObject(new GameObject("InspectFull_T7"));
            go.AddComponent<BoxCollider>();

            var result = CommandRouter.Process(
                "{\"id\":\"oh7\",\"cmd\":\"inspect\"," +
                "\"args\":{\"paths\":\"/InspectFull_T7\"}}");

            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("Transform", result);
            StringAssert.Contains("BoxCollider", result);
        }
    }
}
