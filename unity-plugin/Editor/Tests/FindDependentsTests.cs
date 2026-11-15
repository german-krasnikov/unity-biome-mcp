// TDD: asset find_dependents — valid action, error paths.
// EditMode only, no TCP required. Tests private statics via reflection.
using System;
using System.Reflection;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class FindDependentsTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        static object InvokePrivate(Type type, string method, params object[] args)
        {
            var mi = type.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(mi, $"Method {type.Name}.{method} not found");
            return mi.Invoke(null, args);
        }

        static string ExecAssetAction(string action, string argsJson) =>
            (string)InvokePrivate(typeof(AssetDatabaseHelper), "Execute", action, argsJson);

        [Test]
        public void FindDependents_IsValidAction()
        {
            // AssetDatabaseHelper's hand ValidActions field was replaced by the catalog-generated
            // ToolActions.Names("asset") in dsl-bottlenecks-2026-11 Part A -- that is now the one
            // source of truth for the tool's valid action vocabulary.
            CollectionAssert.Contains(ToolActions.Names("asset"), "find_dependents");
        }

        [Test]
        public void FindDependents_EmptyPath_Throws()
        {
            var ex = Assert.Throws<TargetInvocationException>(() => ExecAssetAction("find_dependents", "{}"));
            Assert.IsInstanceOf<ArgumentException>(ex.InnerException);
            StringAssert.Contains("path", ex.InnerException.Message);
        }

        [Test]
        public void FindDependents_NonexistentAsset_Throws()
        {
            var ex = Assert.Throws<TargetInvocationException>(() =>
                ExecAssetAction("find_dependents", "{\"path\":\"Assets/DoesNotExist/Fake.mat\"}"));
            Assert.IsInstanceOf<ArgumentException>(ex.InnerException);
            StringAssert.Contains("not found", ex.InnerException.Message);
        }
    }
}
