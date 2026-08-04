using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PrefabInstantiateTests : SceneTestBase
    {
        [Test]
        public void Instantiate_ActionIsRegistered()
        {
            Assert.DoesNotThrow(() =>
                ErrorHelper.InvalidAction("instantiate",
                    new[] { "save", "create_variant", "instantiate", "apply", "revert", "get_overrides", "unpack", "edit" }));
        }

        [Test]
        public void Instantiate_MissingAssetPath_Throws()
        {
            Assert.Throws<System.ArgumentException>(() =>
                PrefabHelper.Execute("instantiate", "{}"));
        }
    }
}
