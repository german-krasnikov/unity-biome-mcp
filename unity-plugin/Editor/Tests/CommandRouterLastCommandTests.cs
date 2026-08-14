// TDD: CommandRouter.LastCommandName is set by Process() and ProcessAsync().
using System.Threading.Tasks;
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class CommandRouterLastCommandTests : SceneTestBase
    {
        [SetUp]
        public void ResetLastCommand()
        {
            CommandRouter.LastCommandName = "";
            RegisterCleanup(() => CommandRouter.LastCommandName = "");
        }

        [Test]
        public void LastCommandName_DefaultsToEmpty()
        {
            // Reset already done in SetUp; verify initial/reset state
            Assert.AreEqual("", CommandRouter.LastCommandName);
        }

        [Test]
        public void Process_SetsLastCommandName()
        {
            CommandRouter.IsCompiling = () => false;
            CommandRouter.IsPlayMode  = () => false;
            try
            {
                CommandRouter.Process("{\"id\":\"1\",\"cmd\":\"ping\",\"args\":{}}");
                Assert.AreEqual("ping", CommandRouter.LastCommandName);
            }
            finally
            {
                CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling;
                CommandRouter.IsPlayMode  = () => UnityEditor.EditorApplication.isPlaying;
            }
        }

        [Test]
        public void ProcessAsync_SetsLastCommandName()
        {
            CommandRouter.IsCompiling = () => false;
            CommandRouter.IsPlayMode  = () => false;
            try
            {
                var tcs = new TaskCompletionSource<string>();
                CommandRouter.ProcessAsync("{\"id\":\"2\",\"cmd\":\"ping\",\"args\":{}}", tcs);
                Assert.AreEqual("ping", CommandRouter.LastCommandName);
            }
            finally
            {
                CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling;
                CommandRouter.IsPlayMode  = () => UnityEditor.EditorApplication.isPlaying;
            }
        }
    }
}
