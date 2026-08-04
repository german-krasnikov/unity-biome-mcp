using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityMCP.Editor;

namespace UnityMCP.TestProject.SceneObject
{
    [TestFixture]
    public class InspectTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private GameObject _obj1;
        private GameObject _obj2;

        [SetUp]
        public void SetUp()
        {
            _obj1 = TrackOwnedObject(new GameObject("InspA"));
            _obj2 = TrackOwnedObject(new GameObject("InspB"));
        }

        [Test]
        public void Inspect_SinglePath_ReturnsComponents()
        {
            var json = "{\"id\":\"i1\",\"cmd\":\"inspect\",\"args\":{\"paths\":\"/InspA\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("--- /InspA ---", result);
            StringAssert.Contains("Transform", result);
        }

        [Test]
        public void Inspect_MultiplePaths_ReturnsSeparated()
        {
            var json = "{\"id\":\"i2\",\"cmd\":\"inspect\",\"args\":{\"paths\":\"/InspA,/InspB\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("--- /InspA ---", result);
            StringAssert.Contains("--- /InspB ---", result);
        }

        [Test]
        public void Inspect_WithComponentFilter_ReturnsOnlyFiltered()
        {
            _obj1.AddComponent<Rigidbody>();
            var json = "{\"id\":\"i3\",\"cmd\":\"inspect\",\"args\":{\"paths\":\"/InspA\",\"components\":\"Rigidbody\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("[Rigidbody]", result);
            // Should not contain Transform section header (filtered out)
            StringAssert.DoesNotContain("[Transform]", result);
        }

        [Test]
        public void Inspect_MissingObject_ReturnsError()
        {
            var json = "{\"id\":\"i4\",\"cmd\":\"inspect\",\"args\":{\"paths\":\"/NonExistent\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("--- /NonExistent ---", result);
            StringAssert.Contains("not found", result.ToLower());
        }

        [Test]
        public void Inspect_MixedExistingAndMissing()
        {
            var json = "{\"id\":\"i5\",\"cmd\":\"inspect\",\"args\":{\"paths\":\"/InspA,/Ghost\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("--- /InspA ---", result);
            StringAssert.Contains("--- /Ghost ---", result);
            StringAssert.Contains("Transform", result);
        }

        [Test]
        public void Inspect_EmptyPaths_ReturnsError()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("paths or find_type is required"));
            var json = "{\"id\":\"i6\",\"cmd\":\"inspect\",\"args\":{\"paths\":\"\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":false", result);
            StringAssert.Contains("paths or find_type is required", result);
        }

        [Test]
        public void Inspect_MultipleComponentFilters()
        {
            _obj1.AddComponent<Rigidbody>();
            _obj1.AddComponent<BoxCollider>();
            var json = "{\"id\":\"i7\",\"cmd\":\"inspect\",\"args\":{\"paths\":\"/InspA\",\"components\":\"Rigidbody,BoxCollider\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("[Rigidbody]", result);
            StringAssert.Contains("[BoxCollider]", result);
        }

        [Test]
        public void Inspect_FilterNonExistentComponent_NoSection()
        {
            var json = "{\"id\":\"i8\",\"cmd\":\"inspect\",\"args\":{\"paths\":\"/InspA\",\"components\":\"Camera\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("--- /InspA ---", result);
            // No Camera on this object, so no [Camera] section
            StringAssert.DoesNotContain("[Camera]", result);
        }
    }
}
