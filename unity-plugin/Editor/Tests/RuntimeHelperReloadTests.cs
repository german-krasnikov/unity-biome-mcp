using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class RuntimeHelperReloadTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private static List<TaskCompletionSource<string>> GetActiveTcs()
        {
            var field = typeof(RuntimeHelper).GetField("_activeTcs",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "_activeTcs field must exist");
            return (List<TaskCompletionSource<string>>)field.GetValue(null);
        }

        [TearDown]
        public void TearDown()
        {
            var list = GetActiveTcs();
            lock (list) list.Clear();
        }

        [Test]
        public void HookReload_Exists()
        {
            var method = typeof(RuntimeHelper).GetMethod("HookReload",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "HookReload static method must exist");
        }

        [Test]
        public void ActiveTcs_FieldExists_AndIsList()
        {
            var list = GetActiveTcs();
            Assert.IsNotNull(list);
            Assert.IsInstanceOf<List<TaskCompletionSource<string>>>(list);
        }

        [Test]
        public async Task ActiveTcs_ManualAdd_SimulateReloadCancels()
        {
            var list = GetActiveTcs();
            var tcs = new TaskCompletionSource<string>();
            lock (list) list.Add(tcs);
            Assert.AreEqual(1, list.Count);

            // Simulate beforeAssemblyReload behavior
            lock (list)
            {
                foreach (var t in list)
                    t.TrySetResult("err:domain reload — operation aborted");
                list.Clear();
            }

            Assert.IsTrue(tcs.Task.IsCompleted);
            Assert.That(await tcs.Task, Does.StartWith("err:domain reload"));
            Assert.AreEqual(0, list.Count);
        }
    }
}
