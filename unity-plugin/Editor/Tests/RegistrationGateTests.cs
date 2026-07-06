// TDD: CommandRegistry.Ready gate (registration-race fix).
// Verifies that commands dispatched before RegisterAll() completes receive a 2s retry
// response instead of "Command not registered" errors (the pre-fix race condition).
using System.Linq;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class RegistrationGateTests
    {
        [TearDown]
        public void TearDown()
        {
            CommandRegistry.Clear();
            CommandRegistry.InitDefaults();  // restore full registration + Ready=true
        }

        [Test]
        public void Clear_SetsReady_False()
        {
            CommandRegistry.InitDefaults();     // Ready = true
            CommandRegistry.Clear();            // Ready = false
            Assert.IsFalse(CommandRegistry.Ready);
        }

        [Test]
        public void RegisterAll_SetsReady_True()
        {
            CommandRegistry.Clear();            // Ready = false
            CommandRouter.RegisterAll();        // Ready = true at end
            Assert.IsTrue(CommandRegistry.Ready);
        }

        [Test]
        public void Process_WhenNotReady_ReturnsRetry2000()
        {
            CommandRegistry.Clear();            // Ready = false
            var json = "{\"id\":\"t1\",\"cmd\":\"get_hierarchy\",\"args\":{}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"retry\":2000", result);
            StringAssert.Contains("initializing", result);
        }

        [Test]
        public void Process_WhenReady_DoesNotReturnInitializingError()
        {
            // TearDown + test start: InitDefaults already called; Ready = true
            var json = "{\"id\":\"t1\",\"cmd\":\"get_disabled_tools\",\"args\":{}}";
            var result = CommandRouter.Process(json);
            StringAssert.DoesNotContain("initializing", result);
        }

        [Test]
        public void InitDefaults_AfterClear_SetsReady_True()
        {
            CommandRegistry.Clear();
            Assert.IsFalse(CommandRegistry.Ready, "Clear() must set Ready=false");
            CommandRegistry.InitDefaults();
            Assert.IsTrue(CommandRegistry.Ready, "InitDefaults() must set Ready=true");
        }

        [Test]
        public void InitDefaults_IsIdempotent()
        {
            // Double-call is safe: WatchdogTick calls StartAsync → InitDefaults on restart.
            CommandRegistry.InitDefaults();
            var count1 = CommandRegistry.GetAllCommands().Count();
            Assert.IsTrue(CommandRegistry.Ready);
            CommandRegistry.InitDefaults();
            var count2 = CommandRegistry.GetAllCommands().Count();
            Assert.AreEqual(count1, count2, "Command count must be stable across double InitDefaults");
            Assert.IsTrue(CommandRegistry.Ready);
        }
    }
}
