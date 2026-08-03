using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    [Explicit("Interactive screenshot tests run only in the dedicated GUI lane.")]
    [Category("UnityMCP.InteractiveVisual")]
    public class ChatWindowScreenshotTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private MCPChatWindow _window;

        [SetUp]
        public async Task SetUp()
        {
            if (Application.isBatchMode)
            {
                Assert.Ignore("Screenshot tests require a GUI; skipping in batch mode.");
                return;
            }

            _window = CreateOwnedEditorWindow<MCPChatWindow>();
            _window.titleContent = new GUIContent("MCP Chat Test");
            _window.minSize = new Vector2(400, 600);
            _window.position = new Rect(100, 100, 400, 600);
            _window.Show();
            _window.Focus();
            await WaitForEditorUpdatesAsync(3);
        }

        [Test]
        public async Task Screenshot_EmptyWindow()
        {
            await WaitForEditorUpdatesAsync();
            var path = CaptureWindow(_window, "empty_chat");
            Assert.IsTrue(File.Exists(path), $"Screenshot not saved at {path}");
            Assert.Greater(new FileInfo(path).Length, 1000, "PNG too small");
        }

        [Test]
        public async Task Screenshot_WithChipsAdded()
        {
            _window.InsertInlineChip(null, "/Player", "Player");
            _window.InsertInlineChip(null, "/Enemy",  "Enemy");
            await WaitForEditorUpdatesAsync(2);
            var path = CaptureWindow(_window, "chips_added");
            Assert.IsTrue(File.Exists(path));
        }

        // ── utility ───────────────────────────────────────────────────────────

        private string CaptureWindow(EditorWindow window, string prefix)
        {
            float scale = EditorGUIUtility.pixelsPerPoint;
            var pos = window.position;
            int w = (int)(pos.width  * scale);
            int h = (int)(pos.height * scale);

            var pixels = InternalEditorUtility.ReadScreenPixel(pos.position, w, h);
            var tex    = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            var png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            var dir = Path.Combine(Path.GetTempPath(), "UnityMCP", "Screenshots",
                System.Guid.NewGuid().ToString("N"));
            RegisterCleanup(() =>
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            });
            Directory.CreateDirectory(dir);
            var filename = prefix + ".png";
            var path     = Path.Combine(dir, filename);
            File.WriteAllBytes(path, png);
            TestContext.WriteLine($"Screenshot saved: {path}");
            return path;
        }
    }
}
