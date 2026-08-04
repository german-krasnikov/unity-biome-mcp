// TDD: PD-3 optional C# params + PD-4 overload matching in RuntimeHelper.
// Red first — tests fail with current code, green after fixes.
// Run in Unity Test Runner → EditMode.
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class RuntimeHelperParseArgsTests : SceneTestBase
    {
        // ── helpers ──────────────────────────────────────────────────────────

        private class ParseArgsTestBehaviour : MonoBehaviour
        {
            // PD-3: all optional
            public string AllOptionals(string a = "default_a", int b = 42)
                => $"{a},{b}";

            // PD-3: one required, one optional
            public string RequiredAndOptional(string required, string optional = "opt_default")
                => $"{required},{optional}";

            // PD-4: overloads — differ only in param count
            public string Overloaded(string single) => $"single:{single}";
            public string Overloaded(string first, string second) => $"double:{first},{second}";
        }

        private GameObject _go;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("RH_ParseArgs_Test");
            _go.AddComponent<ParseArgsTestBehaviour>();
            _path = ComponentSerializer.GetPath(_go);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        // ── PD-3: optional params ─────────────────────────────────────────────

        [Test]
        public void ParseArgs_AllOptional_EmptyArgs_UsesDefaults()
        {
            // Current code throws: "Expected 2 args …, got 0"
            // After fix: returns defaults → "default_a,42"
            var result = RuntimeHelper.InvokeMethod(
                _path, "ParseArgsTestBehaviour", "AllOptionals", null);

            Assert.AreEqual("default_a,42", result);
        }

        [Test]
        public void ParseArgs_PartialOptional_MissingArgsUseDefaults()
        {
            // Current code throws: "Not enough args for param 1 …"
            // After fix: first arg "supplied", second uses default → "supplied,opt_default"
            var result = RuntimeHelper.InvokeMethod(
                _path, "ParseArgsTestBehaviour", "RequiredAndOptional", "supplied");

            Assert.AreEqual("supplied,opt_default", result);
        }

        // ── PD-4: overload matching ───────────────────────────────────────────

        [Test]
        public void InvokeMethod_Overload_SingleArg_PicksCorrectOverload()
        {
            // Current code always picks the first overload by declaration order.
            // After fix: 1 comma-part → picks Overloaded(string single)
            var result = RuntimeHelper.InvokeMethod(
                _path, "ParseArgsTestBehaviour", "Overloaded", "hello");

            Assert.AreEqual("single:hello", result);
        }

        [Test]
        public void InvokeMethod_Overload_TwoArgs_PicksCorrectOverload()
        {
            // Current code picks first overload → ParseArgs gets 2 parts for 1 param → throws.
            // After fix: 2 comma-parts → picks Overloaded(string, string)
            var result = RuntimeHelper.InvokeMethod(
                _path, "ParseArgsTestBehaviour", "Overloaded", "hello,world");

            Assert.AreEqual("double:hello,world", result);
        }
    }
}
