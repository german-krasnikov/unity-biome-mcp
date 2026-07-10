// TDD — RED first. EditMode tests proving RuntimeHelper.InvokeMethod can reach
// private instance methods and public static methods after BindingFlags expansion.
// Run in Unity Test Runner → EditMode.
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class RuntimeHelperInvokeMethodTests : SceneTestBase
    {
        // Test component with private + static methods
        private class InvokeTestBehaviour : MonoBehaviour
        {
            private string PrivateMethod() => "private_ok";
            public static string StaticMethod() => "static_ok";
        }

        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("RHInvoke_Test");
            _go.AddComponent<InvokeTestBehaviour>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void InvokeMethod_PrivateMethod_ReturnsResult()
        {
            var path = ComponentSerializer.GetPath(_go);
            var result = RuntimeHelper.InvokeMethod(path, "InvokeTestBehaviour", "PrivateMethod", null);
            Assert.AreEqual("private_ok", result);
        }

        [Test]
        public void InvokeMethod_StaticMethod_ReturnsResult()
        {
            var path = ComponentSerializer.GetPath(_go);
            var result = RuntimeHelper.InvokeMethod(path, "InvokeTestBehaviour", "StaticMethod", null);
            Assert.AreEqual("static_ok", result);
        }
    }
}
