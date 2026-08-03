// Tests for instance-based AssetPreviewService.
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class AssetPreviewServiceTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        AssetPreviewService _service;
        int _assetLoadCalls;
        int _previewCalls;

        [SetUp]
        public void SetUp()
        {
            AssetPreviewService.AutoHookEditorUpdate = false;
            _service = new AssetPreviewService();
            _assetLoadCalls = 0;
            _previewCalls = 0;
            _service.AssetLoader = path => { _assetLoadCalls++; return Texture2D.whiteTexture; };
            _service.PreviewExtractor = asset => { _previewCalls++; return null; };
        }

        [TearDown]
        public void TearDown()
        {
            _service?.Dispose();
            AssetPreviewService.AutoHookEditorUpdate = true;
        }

        [Test]
        public void RequestPreview_CacheHit_ReturnsImmediately()
        {
            bool firstCalled = false;
            _service.PreviewExtractor = asset => Texture2D.whiteTexture;
            _service.RequestPreview("Assets/a.png", _ => firstCalled = true);
            _service.ProcessForTests();
            Assert.IsTrue(firstCalled, "first request must callback");

            bool secondCalled = false;
            _assetLoadCalls = 0;
            _service.RequestPreview("Assets/a.png", _ => secondCalled = true);
            Assert.IsTrue(secondCalled, "cache hit must callback immediately");
            Assert.AreEqual(0, _assetLoadCalls, "cache hit must not reload asset");
        }

        [Test]
        public void RequestPreview_CacheMiss_EnqueuesRequest()
        {
            bool called = false;
            _service.RequestPreview("Assets/missing.png", _ => called = true);
            Assert.IsFalse(called, "callback must not fire synchronously on cache miss");
            Assert.AreEqual(1, _service.QueueCountForTests);
        }

        [Test]
        public void RequestPreview_ExceedsMaxConcurrent_Queues()
        {
            for (int i = 0; i < 6; i++)
                _service.RequestPreview($"Assets/{i}.png", _ => { });

            _service.ProcessForTests();

            Assert.AreEqual(5, _service.ActiveCountForTests, "max 5 concurrent active requests");
            Assert.AreEqual(1, _service.QueueCountForTests, "6th request must stay queued");
        }

        [Test]
        public void RequestPreview_DuplicateInFlight_Deduped()
        {
            bool cb1 = false, cb2 = false;
            _service.RequestPreview("Assets/shared.png", _ => cb1 = true);
            _service.RequestPreview("Assets/shared.png", _ => cb2 = true);
            _service.ProcessForTests();

            Assert.AreEqual(1, _assetLoadCalls, "duplicate path must load asset only once");
            Assert.AreEqual(1, _service.ActiveCountForTests, "duplicate must share one active slot");

            _service.PreviewExtractor = _ => Texture2D.whiteTexture;
            _service.ProcessForTests();
            Assert.IsTrue(cb1 && cb2, "both callbacks must fire when preview resolves");
        }

        [Test]
        public void RequestPreview_Cancelled_CallbackNotInvoked()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            bool called = false;
            _service.RequestPreview("Assets/cancelled.png", _ => called = true, cts.Token);
            _service.ProcessForTests();
            Assert.IsFalse(called, "cancelled request must not invoke callback");
        }

        [Test]
        public void Invalidate_RemovesPathFromCache()
        {
            _service.PreviewExtractor = _ => Texture2D.whiteTexture;
            bool called = false;
            _service.RequestPreview("Assets/b.png", _ => called = true);
            _service.ProcessForTests();
            Assert.IsTrue(called);
            Assert.AreEqual(1, _service.CacheCountForTests);

            _service.Invalidate("Assets/b.png");
            Assert.AreEqual(0, _service.CacheCountForTests);

            _assetLoadCalls = 0;
            _service.RequestPreview("Assets/b.png", _ => { });
            _service.ProcessForTests();
            Assert.AreEqual(1, _assetLoadCalls, "invalidated path must reload");
        }

        [Test]
        public void Clear_EmptiesCacheAndQueue()
        {
            _service.PreviewExtractor = _ => Texture2D.whiteTexture;
            _service.RequestPreview("Assets/c.png", _ => { });
            _service.ProcessForTests();
            Assert.AreEqual(1, _service.CacheCountForTests);

            _service.RequestPreview("Assets/d.png", _ => { });
            _service.Clear();
            Assert.AreEqual(0, _service.CacheCountForTests);
            Assert.AreEqual(0, _service.QueueCountForTests);
            Assert.AreEqual(0, _service.ActiveCountForTests);
        }
    }
}
