using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    public abstract class ChipTestBase
    {
        [SetUp]
        public virtual void ChipSetUp()
        {
            ChipKindRegistry.ResetForTests();
            ChipPillFactory.ColorResolver = null;
        }

        [TearDown]
        public virtual void ChipTearDown()
        {
            ChipKindRegistry.ResetForTests();
            ChipPillFactory.ColorResolver = null;
        }
    }
}
