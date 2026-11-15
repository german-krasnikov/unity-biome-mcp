using System;
using NUnit.Framework;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ComponentSerializerResolvePathTests : SceneTestBase
    {
        [Test]
        public void ResolvePath_PathMissing_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                ComponentSerializer.ResolvePath("{\"other\":\"x\"}"));
        }

        [Test]
        public void ResolvePath_PathNotFound_Throws()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                ComponentSerializer.ResolvePath("{\"path\":\"/NoSuchObject_RP_xyz\"}"));
            Assert.AreEqual(ErrorHelper.NotFound("Object", "/NoSuchObject_RP_xyz"), ex.Message);
        }

        [Test]
        public void ResolvePath_PathFound_ReturnsGameObject()
        {
            var go = TrackOwnedObject(new GameObject("ResolvePathProbe"));
            var result = ComponentSerializer.ResolvePath("{\"path\":\"/ResolvePathProbe\"}");
            Assert.AreSame(go, result);
        }

        [Test]
        public void ResolvePath_CustomKey_ReadsGivenKey()
        {
            var go = TrackOwnedObject(new GameObject("ResolvePathCustomKeyProbe"));
            var result = ComponentSerializer.ResolvePath(
                "{\"target\":\"/ResolvePathCustomKeyProbe\"}", "target");
            Assert.AreSame(go, result);
        }
    }
}
