// TDD: SerializedFieldRenameAudit — required-arg + happy-path characterization.
// Added as part of ARG adoption Step 4 Batch 3 (no prior coverage existed).
using System;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class SerializedFieldRenameAuditTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Execute_MissingType_ThrowsRequiredArgError()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                SerializedFieldRenameAudit.Execute("{\"old_field\":\"a\",\"new_field\":\"b\"}"));
            StringAssert.Contains("type", ex.Message);
        }

        [Test]
        public void Execute_MissingOldField_ThrowsRequiredArgError()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                SerializedFieldRenameAudit.Execute("{\"type\":\"UnityEngine.GameObject\",\"new_field\":\"b\"}"));
            StringAssert.Contains("old_field", ex.Message);
        }

        [Test]
        public void Execute_MissingNewField_ThrowsRequiredArgError()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                SerializedFieldRenameAudit.Execute("{\"type\":\"UnityEngine.GameObject\",\"old_field\":\"a\"}"));
            StringAssert.Contains("new_field", ex.Message);
        }

        [Test]
        public void Execute_UnknownType_ThrowsTypeNotFound()
        {
            var json = "{\"type\":\"NoSuchType12345\",\"old_field\":\"a\",\"new_field\":\"b\"}";
            var ex = Assert.Throws<ArgumentException>(() => SerializedFieldRenameAudit.Execute(json));
            StringAssert.Contains("Type not found", ex.Message);
        }

        [Test]
        public void Execute_KnownType_DefaultInclude_ReturnsReport()
        {
            var json = "{\"type\":\"UnityEngine.GameObject\",\"old_field\":\"__no_such_field__\",\"new_field\":\"tag\"}";
            var result = SerializedFieldRenameAudit.Execute(json);
            StringAssert.Contains("has_formerly_serialized_as:", result);
            StringAssert.Contains("stale_assets:", result);
            StringAssert.Contains("safe_to_remove_attribute:", result);
        }

        [Test]
        public void Execute_ExplicitInclude_ScansOnlyRequestedTargets()
        {
            // include=prefabs only -- must not throw even though scenes/scriptable_objects are skipped.
            var json = "{\"type\":\"UnityEngine.GameObject\",\"old_field\":\"__no_such_field__\",\"new_field\":\"tag\",\"include\":\"prefabs\"}";
            string result = null;
            Assert.DoesNotThrow(() => result = SerializedFieldRenameAudit.Execute(json));
            StringAssert.Contains("stale_assets:", result);
        }
    }
}
