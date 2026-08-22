// TDD — WatchRegistry domain-reload persistence (Subtask 3 of ARCH-domain-reload-state-fixes).
// Mechanism already in place: WatchScheduler [InitializeOnLoad] calls WatchRegistry.Load()
// and re-subscribes EditorApplication.update += Tick. These tests verify the full round-trip.
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class WatchRegistryReloadTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void Setup()
        {
            WatchRegistry.Clear();
            WatchRegistry.Save(); // reset SessionState
        }

        [TearDown]
        public void Teardown()
        {
            WatchRegistry.Clear();
            WatchRegistry.Save();
        }

        [Test]
        public void WatchRegistry_SurvivesDomainReload_WatchRestored()
        {
            var id = WatchRegistry.Add("/Player", "Health", "hp");
            WatchRegistry.Save();
            WatchRegistry.SimulateDomainReloadForTest();

            WatchRegistry.Load();

            Assert.IsTrue(WatchRegistry.All.ContainsKey(id), "Watch must survive reload");
            Assert.AreEqual("/Player", WatchRegistry.All[id].Path);
        }

        [Test]
        public void WatchRegistry_Reload_IdCounterContinues_NoCollision()
        {
            var id1 = WatchRegistry.Add("/A", "C", "f");
            var id2 = WatchRegistry.Add("/B", "C", "f");
            var id3 = WatchRegistry.Add("/C", "C", "f");
            WatchRegistry.Save();
            WatchRegistry.SimulateDomainReloadForTest();
            WatchRegistry.Load();

            var id4 = WatchRegistry.Add("/D", "C", "f");

            Assert.IsFalse(WatchRegistry.All.ContainsKey(id1) && id4 == id1, "New id must not collide");
            Assert.IsFalse(WatchRegistry.All.ContainsKey(id2) && id4 == id2, "New id must not collide");
            Assert.IsFalse(WatchRegistry.All.ContainsKey(id3) && id4 == id3, "New id must not collide");
            Assert.IsTrue(WatchRegistry.All.ContainsKey(id4), "New watch must be added");
        }

        [Test]
        public void WatchScheduler_ReRegister_TickSubscribedAfterLoad()
        {
            WatchRegistry.Add("/Player", "Health", "hp");
            WatchRegistry.Save();
            WatchRegistry.SimulateDomainReloadForTest();

            WatchScheduler.ReRegisterForTest();

            Assert.AreEqual(1, WatchRegistry.All.Count, "Watch must be restored by ReRegisterForTest");

            // Verify Tick is in EditorApplication.update delegate list via reflection
            var field = typeof(EditorApplication).GetField("update",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var del = field?.GetValue(null) as Delegate;
            bool tickSubscribed = false;
            if (del != null)
            {
                foreach (var inv in del.GetInvocationList())
                {
                    if (inv.Method.Name == "Tick" &&
                        inv.Method.DeclaringType == typeof(WatchScheduler))
                    {
                        tickSubscribed = true;
                        break;
                    }
                }
            }
            Assert.IsTrue(tickSubscribed, "WatchScheduler.Tick must be subscribed after ReRegisterForTest");
        }
    }
}
