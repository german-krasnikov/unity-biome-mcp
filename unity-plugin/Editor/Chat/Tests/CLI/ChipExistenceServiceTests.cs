// TDD tests for ChipExistenceService — instance-based existence checker.
using System;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChipExistenceServiceTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        ChipExistenceService _service;

        [SetUp]
        public void SetUp() => _service = new ChipExistenceService();

        [TearDown]
        public void TearDown() => _service.Clear();

        // ── Exists ────────────────────────────────────────────────────────────

        [Test]
        public void Exists_Unknown_ReturnsNull()
        {
            var result = _service.Exists(ChipKindKeys.Asset, "Assets/unknown.mat");
            Assert.IsNull(result, "First Exists on uncached entry must return null");
        }

        [Test]
        public void Exists_Cached_ReturnsValue()
        {
            _service.Exists(ChipKindKeys.Asset, "Assets/x.mat"); // queue
            _service.ForceProcessForTests(); // resolve pending
            var result = _service.Exists(ChipKindKeys.Asset, "Assets/x.mat");
            Assert.IsNotNull(result);
        }

        [Test]
        public void Exists_NullPath_ReturnsFalse()
        {
            var result = _service.Exists(ChipKindKeys.Asset, null);
            Assert.AreEqual(false, result);
        }

        [Test]
        public void Exists_Catch_ReturnsFalseNotTrue()
        {
            // Invalid path characters should be caught and resolve to false, not true.
            _service.Exists(ChipKindKeys.Hierarchy, "\0invalid");
            _service.ForceProcessForTests();
            var result = _service.Exists(ChipKindKeys.Hierarchy, "\0invalid");
            Assert.AreEqual(false, result);
        }

        // ── Observe ───────────────────────────────────────────────────────────

        [Test]
        public void Observe_Resolved_CallbackFires()
        {
            bool? received = null;
            using (_service.Observe(ChipKindKeys.Image, "Assets/img.png", exists => received = exists))
            {
                _service.ForceProcessForTests();
                Assert.IsTrue(received.HasValue, "Observe callback must fire after resolution");
            }
        }

        [Test]
        public void Observe_Disposed_CallbackDoesNotFire()
        {
            bool? received = null;
            var token = _service.Observe(ChipKindKeys.Image, "Assets/img2.png", exists => received = exists);
            token.Dispose();
            _service.ForceProcessForTests();
            Assert.IsFalse(received.HasValue, "Disposed subscription must not receive callbacks");
        }

        [Test]
        public void Observe_AlreadyCached_CallbackFiresSynchronously()
        {
            _service.Exists(ChipKindKeys.Image, "Assets/img3.png");
            _service.ForceProcessForTests();

            bool? received = null;
            using (_service.Observe(ChipKindKeys.Image, "Assets/img3.png", exists => received = exists))
            {
                Assert.IsTrue(received.HasValue, "Observe must invoke callback synchronously for cached values");
            }
        }

        // ── Invalidate / Clear ────────────────────────────────────────────────

        [Test]
        public void Invalidate_RemovesEntry()
        {
            _service.Exists(ChipKindKeys.Image, "Assets/a.png");
            _service.ForceProcessForTests();
            Assert.IsTrue(_service.Exists(ChipKindKeys.Image, "Assets/a.png").HasValue);

            _service.Invalidate(ChipKindKeys.Image, "Assets/a.png");
            Assert.IsNull(_service.Exists(ChipKindKeys.Image, "Assets/a.png"));
        }

        [Test]
        public void Clear_EmptiesCacheAndPending()
        {
            _service.Exists(ChipKindKeys.Asset, "Assets/b.mat");
            _service.Exists(ChipKindKeys.Hierarchy, "/Root");
            _service.Clear();
            Assert.IsNull(_service.Exists(ChipKindKeys.Asset, "Assets/b.mat"));
            Assert.IsNull(_service.Exists(ChipKindKeys.Hierarchy, "/Root"));
        }
    }
}
