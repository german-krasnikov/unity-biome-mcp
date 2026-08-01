// Cycle 3 TDD — SetObjectReference null guard + GetSerializedFieldType regression.
// RED: SetObjectReference_TypeMismatch message must contain "Type mismatch".
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class GetSerializedFieldTypeTests : SceneTestBase
    {
        private GameObject _host;
        private GameObject _someGO;

        [SetUp]
        public void SetUp()
        {
            _host  = new GameObject("GSFT_Host");
            _someGO = new GameObject("SomeGO");   // no Light — used for type-mismatch test
        }

        [TearDown]
        public void TearDown()
        {
            if (_host  != null) Object.DestroyImmediate(_host);
            if (_someGO != null) Object.DestroyImmediate(_someGO);
        }

        // RED: current error is "Component Light not found on /SomeGO" — no "Type mismatch"
        [Test]
        public void SetObjectReference_TypeMismatch_ThrowsArgumentException()
        {
            var comp = _host.AddComponent<ReflectionTestComponent>();
            var so   = new SerializedObject(comp);
            var prop = so.FindProperty("_scalarRef");
            Assert.IsNotNull(prop, "_scalarRef must exist on ReflectionTestComponent");

            var ex = Assert.Throws<System.ArgumentException>(() =>
                ValueParser.SetPropertyValue(prop, "/SomeGO"));
            StringAssert.Contains("Type mismatch", ex.Message);
        }

        // GREEN regression: scalar field returns its declared type
        [Test]
        public void GetSerializedFieldType_Scalar_ReturnsCorrectType()
        {
            var comp = _host.AddComponent<ReflectionTestComponent>();
            var so   = new SerializedObject(comp);
            var prop = so.FindProperty("_scalarRef");
            Assert.IsNotNull(prop);

            var t = ValueParser.GetSerializedFieldType(prop);
            Assert.AreEqual(typeof(Light), t);
        }

        // GREEN regression: Light[] array element returns Light
        [Test]
        public void GetSerializedFieldType_ArrayElement_ReturnsElementType()
        {
            var comp = _host.AddComponent<ReflectionTestComponent>();
            var so   = new SerializedObject(comp);
            var arr  = so.FindProperty("_arrayRef");
            Assert.IsNotNull(arr);
            arr.arraySize = 1;
            so.ApplyModifiedProperties();

            var elem = arr.GetArrayElementAtIndex(0);
            Assert.IsNotNull(elem, "array element property must exist");
            Assert.AreEqual("_arrayRef.Array.data[0]", elem.propertyPath);

            var t = ValueParser.GetSerializedFieldType(elem);
            Assert.AreEqual(typeof(Light), t);
        }

        // GREEN regression: List<Light> element returns Light
        [Test]
        public void GetSerializedFieldType_ListElement_ReturnsElementType()
        {
            var comp = _host.AddComponent<ReflectionTestComponent>();
            var so   = new SerializedObject(comp);
            var lst  = so.FindProperty("_listRef");
            Assert.IsNotNull(lst);
            lst.arraySize = 1;
            so.ApplyModifiedProperties();

            var elem = lst.GetArrayElementAtIndex(0);
            Assert.IsNotNull(elem, "list element property must exist");
            Assert.AreEqual("_listRef.Array.data[0]", elem.propertyPath);

            var t = ValueParser.GetSerializedFieldType(elem);
            Assert.AreEqual(typeof(Light), t);
        }
    }
}
