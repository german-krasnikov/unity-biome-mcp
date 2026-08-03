// BUG 4 TDD: user bubble must show ALL attached images, not just the first.
// AppendUserBubble currently forwards only FirstImageChipPath → single imagePath.
// Fix: pass IReadOnlyList<string> imagePaths so all images render.
//
// RED phase: AppendUserBubble(string, chips, imagePaths: IReadOnlyList<string>) does not exist yet.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class MultiImageBubbleTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]    public void SetUp()    => ChipKindRegistry.ResetToBuiltIns();
        [TearDown] public void TearDown() { ChipKindRegistry.ResetToBuiltIns(); ChipPillFactory.ColorResolver = null; }

        ChatTranscript Make(out VisualElement c)
        {
            c = new VisualElement();
            return new ChatTranscript(c, ChatBlockRendererFactory.CreateDefault(null, null));
        }

        // BUG 4: Two image paths → bubble must contain two image-related elements.
        // Currently only the first path is passed → only 1 element rendered.
        // This test will FAIL (red) until AppendUserBubble is updated to pass all paths.
        [Test]
        public void AppendUserBubble_TwoImagePaths_BubbleContainsTwoImageElements()
        {
            var t = Make(out var c);
            var imagePaths = new List<string>
            {
                "/nonexistent/shot1.png",
                "/nonexistent/shot2.png",
            };
            // Calls the new overload accepting IReadOnlyList<string> imagePaths.
            // This does NOT exist yet → RED (compile error until overload is added).
            t.AppendUserBubble("look at these", chips: null, imagePaths: imagePaths);

            var bubble = ChatWindowAssertions.GetUserBubble(c, 0);
            // Each missing image renders as Label with class "md-image-alt".
            var altLabels = bubble.Query(className: "md-image-alt").ToList();
            Assert.AreEqual(2, altLabels.Count,
                $"Expected 2 image elements in bubble, got {altLabels.Count}");
        }

        // Regression: single imagePath overload still works unchanged.
        [Test]
        public void AppendUserBubble_SingleImagePath_BubbleContainsOneImageElement()
        {
            var t = Make(out var c);
            t.AppendUserBubble("look", imagePath: "/nonexistent/shot.png");

            var bubble = ChatWindowAssertions.GetUserBubble(c, 0);
            var altLabels = bubble.Query(className: "md-image-alt").ToList();
            Assert.AreEqual(1, altLabels.Count,
                $"Expected 1 image element in bubble, got {altLabels.Count}");
        }

        // Regression: no imagePaths → no image elements.
        [Test]
        public void AppendUserBubble_NoImagePaths_BubbleHasNoImageElements()
        {
            var t = Make(out var c);
            t.AppendUserBubble("just text");

            var bubble = ChatWindowAssertions.GetUserBubble(c, 0);
            var altLabels = bubble.Query(className: "md-image-alt").ToList();
            Assert.AreEqual(0, altLabels.Count,
                $"Expected 0 image elements in bubble, got {altLabels.Count}");
        }
    }
}
