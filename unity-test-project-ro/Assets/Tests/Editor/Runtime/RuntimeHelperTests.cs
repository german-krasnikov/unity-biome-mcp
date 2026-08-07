using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.TestProject.Runtime
{
    [TestFixture]
    public class RuntimeHelperTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private GameObject _testObj;
        private Rigidbody _rb;

        [SetUp]
        public void SetUp()
        {
            _testObj = TrackOwnedObject(new GameObject("RTTest"));
            _rb = _testObj.AddComponent<Rigidbody>();
        }

        [Test]
        public void InvokeMethod_PublicMethod_ReturnsValue()
        {
            var result = RuntimeHelper.InvokeMethod("RTTest", "Rigidbody", "IsSleeping", "");
            Assert.That(result, Is.EqualTo("False"));
        }

        [Test]
        public void InvokeMethod_MethodWithStringReturn_ReturnsNonNull()
        {
            var result = RuntimeHelper.InvokeMethod("RTTest", "Transform", "ToString", "");
            Assert.That(result, Is.Not.Null);
        }

        [Test]
        public void InvokeMethod_NonExistentMethod_ListsAvailable()
        {
            var ex = Assert.Throws<System.ArgumentException>(
                () => RuntimeHelper.InvokeMethod("RTTest", "Rigidbody", "FakeMethod", ""));
            Assert.That(ex.Message, Does.Contain("not found"));
            Assert.That(ex.Message, Does.Contain("Available:"));
        }

        [Test]
        public void InvokeMethod_WrongArgCount_Error()
        {
            // Translate takes Vector3 (3 floats); pass only 1 to trigger count mismatch
            var ex = Assert.Throws<System.ArgumentException>(
                () => RuntimeHelper.InvokeMethod("RTTest", "Transform", "Translate", "1"));
            Assert.That(ex.Message, Does.Contain("Expected").Or.Contain("Not enough").Or.Contain("Too many"));
        }

        [Test]
        public void InvokeMethod_NonExistentObject_Error()
        {
            Assert.Throws<System.ArgumentException>(
                () => RuntimeHelper.InvokeMethod("RTTestNonExistent999", "Rigidbody", "IsSleeping", ""));
        }

        [Test]
        public void InvokeMethod_NonExistentComponent_Error()
        {
            var ex = Assert.Throws<System.ArgumentException>(
                () => RuntimeHelper.InvokeMethod("RTTest", "FakeComp", "IsSleeping", ""));
            Assert.That(ex.Message, Is.Not.Null);
        }

        [Test]
        public void SetRuntimeProperty_Mass_Changed()
        {
            RuntimeHelper.SetRuntimeProperty("RTTest", "Rigidbody", "mass", "99.5");
            Assert.That(_rb.mass, Is.EqualTo(99.5f).Within(0.001f));
        }

        [Test]
        public void SetRuntimeProperty_UseGravity_Changed()
        {
            RuntimeHelper.SetRuntimeProperty("RTTest", "Rigidbody", "useGravity", "false");
            Assert.That(_rb.useGravity, Is.False);
        }

        [Test]
        public void SetRuntimeProperty_NonExistentField_Error()
        {
            var ex = Assert.Throws<System.ArgumentException>(
                () => RuntimeHelper.SetRuntimeProperty("RTTest", "Rigidbody", "fakeField", "0"));
            Assert.That(ex.Message, Does.Contain("fakeField").And.Contain("not found"));
        }

        [Test]
        public void FindComponentInternal_ByShortName_ReturnsNonNull()
        {
            var comp = RuntimeHelper.FindComponentInternal(_testObj, "Rigidbody");
            Assert.That(comp, Is.Not.Null);
        }

        [Test]
        public void FindComponentInternal_ByFullName_ReturnsNonNull()
        {
            var comp = RuntimeHelper.FindComponentInternal(_testObj, "UnityEngine.Rigidbody");
            Assert.That(comp, Is.Not.Null);
        }

        [Test]
        public void FindComponentInternal_NotFound_ReturnsNull()
        {
            var comp = RuntimeHelper.FindComponentInternal(_testObj, "NonExistent");
            Assert.That(comp, Is.Null);
        }

        [Test]
        public void ReadFieldInternal_Property_ReturnsValue()
        {
            var result = RuntimeHelper.ReadFieldInternal(_rb, "mass");
            Assert.That(result, Is.EqualTo("1"));
        }

        [Test]
        public void ReadFieldInternal_NonExistent_Throws()
        {
            Assert.Throws<System.ArgumentException>(
                () => RuntimeHelper.ReadFieldInternal(_rb, "fakeField"));
        }

        [Test]
        public void ParseArgs_Vector3_SmartGrouping()
        {
            // SetRuntimeProperty sets Vector3 field using ParseFloats internally
            var result = RuntimeHelper.SetRuntimeProperty("RTTest", "Rigidbody", "centerOfMass", "1,2,3");
            Assert.That(result, Does.Contain("centerOfMass"));
        }

        [Test]
        public void ParseArgs_EmptyArgs_NoParamMethod()
        {
            // ResetCenterOfMass() takes no parameters
            var result = RuntimeHelper.InvokeMethod("RTTest", "Rigidbody", "ResetCenterOfMass", "");
            Assert.That(result, Is.EqualTo("void"));
        }

        // ─── GameState / Snapshot tests ───

        [Test]
        public void Snapshot_SingleTriplet_ReturnsFieldValue()
        {
            var snapObj = new GameObject("SnapTest");
            snapObj.AddComponent<Rigidbody>();
            try
            {
                var result = GameStateHelper.Snapshot("SnapTest|Rigidbody|mass");
                Assert.That(result, Does.Contain("Rigidbody.mass=1"));
            }
            finally { Object.DestroyImmediate(snapObj); }
        }

        [Test]
        public void Snapshot_MultipleTriplets_CommaSeparated()
        {
            var snapObj = new GameObject("SnapTest");
            snapObj.AddComponent<Rigidbody>();
            try
            {
                var result = GameStateHelper.Snapshot("SnapTest|Rigidbody|mass,SnapTest|Rigidbody|useGravity");
                Assert.That(result, Does.Contain("Rigidbody.mass="));
                Assert.That(result, Does.Contain("Rigidbody.useGravity="));
            }
            finally { Object.DestroyImmediate(snapObj); }
        }

        [Test]
        public void Snapshot_MalformedTriplet_ReturnsERR()
        {
            var result = GameStateHelper.Snapshot("badformat");
            Assert.That(result, Does.Contain("ERR:"));
        }

        [Test]
        public void Snapshot_NonExistentObject_ReturnsERR()
        {
            var result = GameStateHelper.Snapshot("NoSuchObj|Rigidbody|mass");
            Assert.That(result, Does.Contain("ERR:object not found"));
        }

        [Test]
        public void Snapshot_NonExistentComponent_ReturnsERR()
        {
            var snapObj = new GameObject("SnapTest");
            try
            {
                var result = GameStateHelper.Snapshot("SnapTest|FakeComp|x");
                Assert.That(result, Does.Contain("ERR:component not found"));
            }
            finally { Object.DestroyImmediate(snapObj); }
        }

        [Test]
        public void Snapshot_MethodFallback_InvokesMethod()
        {
            var snapObj = new GameObject("SnapTest");
            snapObj.AddComponent<Rigidbody>();
            try
            {
                // IsSleeping is a method, not a field — ReadFieldInternal throws, falls back to InvokeMethod
                var result = GameStateHelper.Snapshot("SnapTest|Rigidbody|IsSleeping");
                Assert.That(result, Does.Contain("Rigidbody.IsSleeping=False"));
            }
            finally { Object.DestroyImmediate(snapObj); }
        }
    }
}
