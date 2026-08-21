// TDD — ST5: HierarchyChipProvider emits &ref (RefManager) instead of $HEX.
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class HierarchyChipProviderTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Create_EmitsAmpRef()
        {
            var go = new GameObject("TestHierarchyChip");
            TrackOwnedObject(go);
            var provider = new HierarchyChipProvider();
            var chip = provider.Create(go, "");
            Assert.IsTrue(chip.ObjectId.StartsWith("&"),
                $"Expected &ref, got: {chip.ObjectId}");
        }

        [Test]
        public void Create_AmpRef_RoundTrip_ResolvesBack()
        {
            var go = new GameObject("RoundTripChip");
            TrackOwnedObject(go);
            var provider = new HierarchyChipProvider();
            var chip = provider.Create(go, "");

            Assert.IsTrue(RefManager.IsRef(chip.ObjectId),
                $"ObjectId '{chip.ObjectId}' must be a valid &ref");
            var resolved = RefManager.Resolve(chip.ObjectId);
            Assert.AreEqual(go, resolved);
        }

        [Test]
        public void Key_Is_hierarchy()
        {
            var provider = new HierarchyChipProvider();
            Assert.AreEqual("hierarchy", provider.Key);
        }
    }
}
