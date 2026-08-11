// Pure NUnit tests for ChatLinkify. No UnityEngine/UnityEditor deps.
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChatLinkifyTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // Resolver that matches any name starting with "GO_"
        private static string ResolveObj(string name) =>
            name.StartsWith("GO_") ? "/" + name : null;

        // Resolver that matches any name ending with ".cs"
        private static string ResolveScript(string name) =>
            name.EndsWith(".cs") ? "Assets/Scripts/" + name : null;

        // Resolver that matches only valid "Assets/..." paths
        private static string ResolveAsset(string name) =>
            name != null && name.StartsWith("Assets/") ? name : null;

        private const string CodeOpen  = "<color=#9aa5ce>";
        private const string CodeClose = "</color>";

        private string Span(string inner) => CodeOpen + inner + CodeClose;

        // ── null / empty ─────────────────────────────────────────────────────

        [Test]
        public void NullInput_ReturnsNull()
        {
            var result = ChatLinkify.Apply(null, ResolveObj, ResolveScript, null);
            Assert.IsNull(result);
        }

        [Test]
        public void EmptyInput_ReturnsEmpty()
        {
            var result = ChatLinkify.Apply("", ResolveObj, ResolveScript, null);
            Assert.AreEqual("", result);
        }

        // ── no code spans ────────────────────────────────────────────────────

        [Test]
        public void NoCodeSpans_Unchanged()
        {
            var input = "Hello <b>world</b> foo";
            Assert.AreEqual(input, ChatLinkify.Apply(input, ResolveObj, ResolveScript, null));
        }

        // ── object resolved ──────────────────────────────────────────────────

        [Test]
        public void CodeSpan_ObjectResolved_WrapsLink()
        {
            var input  = "See " + Span("GO_Player") + " here";
            var result = ChatLinkify.Apply(input, ResolveObj, ResolveScript, null);

            Assert.IsTrue(result.Contains("<link=\"obj:/GO_Player\">"), $"Got: {result}");
            Assert.IsTrue(result.Contains("</link>"),                  $"Got: {result}");
            Assert.IsTrue(result.Contains("<u>"),                      $"Got: {result}");
            Assert.IsTrue(result.Contains(Span("GO_Player")),          $"Got: {result}");
        }

        // ── script resolved ──────────────────────────────────────────────────

        [Test]
        public void CodeSpan_ScriptResolved_WrapsLink()
        {
            var input  = "Edit " + Span("Player.cs") + " now";
            var result = ChatLinkify.Apply(input, ResolveObj, ResolveScript, null);

            Assert.IsTrue(result.Contains("<link=\"script:Assets/Scripts/Player.cs\">"), $"Got: {result}");
            Assert.IsTrue(result.Contains("</link>"), $"Got: {result}");
        }

        // ── unresolved ───────────────────────────────────────────────────────

        [Test]
        public void CodeSpan_Unresolved_Unchanged()
        {
            var input  = "Use " + Span("SomeUnknown") + " here";
            var result = ChatLinkify.Apply(input, ResolveObj, ResolveScript, null);

            Assert.AreEqual(input, result);
        }

        // ── object wins over script ──────────────────────────────────────────

        [Test]
        public void CodeSpan_ObjectPriority_OverScript()
        {
            // A name that matches BOTH resolvers — object wins.
            string BothResolveObj(string n)    => "/" + n;  // always resolves
            string BothResolveScript(string n) => "Assets/" + n; // also resolves

            var input  = Span("Foo");
            var result = ChatLinkify.Apply(input, BothResolveObj, BothResolveScript, null);

            Assert.IsTrue(result.Contains("<link=\"obj:/Foo\">"), $"Got: {result}");
            Assert.IsFalse(result.Contains("script:"),            $"Got: {result}");
        }

        // ── multiple spans ───────────────────────────────────────────────────

        [Test]
        public void MultipleCodeSpans_EachResolvedIndependently()
        {
            var input  = Span("GO_A") + " and " + Span("Unknown") + " and " + Span("B.cs");
            var result = ChatLinkify.Apply(input, ResolveObj, ResolveScript, null);

            Assert.IsTrue(result.Contains("<link=\"obj:/GO_A\">"),              $"Got: {result}");
            Assert.IsTrue(result.Contains(Span("Unknown")),                    $"Got: {result}");
            Assert.IsTrue(result.Contains("<link=\"script:Assets/Scripts/B.cs\">"), $"Got: {result}");
        }

        // ── noparse stripping for lookup ─────────────────────────────────────

        [Test]
        public void CodeSpan_WithNoparse_StripsForLookup()
        {
            // After MarkdownInline.Escape, "GO_<A>" becomes "GO_<noparse><</noparse>A>"
            // Resolver should see "GO_<A>".
            // We craft the already-escaped rich text:
            var escapedInner = "GO_<noparse><</noparse>A>";
            var input        = Span(escapedInner);

            string ObjResolver(string n) => n == "GO_<A>" ? "/GO_<A>" : null;
            var result = ChatLinkify.Apply(input, ObjResolver, _ => null, null);

            Assert.IsTrue(result.Contains("<link=\"obj:/GO_<A>\">"), $"Got: {result}");
        }

        // ── existing link tag not double-wrapped ──────────────────────────────

        [Test]
        public void ExistingLinkTag_NotDoubleWrapped()
        {
            var input  = "<link=\"obj:/Already\">" + Span("AlreadyLinked") + "</link>";
            var result = ChatLinkify.Apply(input, _ => "/AlreadyLinked", _ => null, null);

            // Should NOT gain another <link> around the already-linked span
            var linkCount = System.Text.RegularExpressions.Regex.Matches(result, "<link=").Count;
            Assert.AreEqual(1, linkCount, $"Got: {result}");
        }

        // ── StripNoparse helper ───────────────────────────────────────────────

        [Test]
        public void StripNoparse_Basic()
        {
            var input    = "foo<noparse><</noparse>bar";
            var expected = "foo<bar";
            Assert.AreEqual(expected, ChatLinkify.StripNoparse(input));
        }

        [Test]
        public void StripNoparse_Empty()
        {
            Assert.AreEqual("", ChatLinkify.StripNoparse(""));
        }

        [Test]
        public void StripNoparse_NoNoparse_Unchanged()
        {
            Assert.AreEqual("hello", ChatLinkify.StripNoparse("hello"));
        }

        // ── asset resolver ───────────────────────────────────────────────────

        [Test]
        public void CodeSpan_AssetPathResolved_WrapsAssetLink()
        {
            var assetPath = "Assets/Materials/Sky.mat";
            var input     = "Use " + Span(assetPath) + " here";
            var result    = ChatLinkify.Apply(input, _ => null, _ => null, ResolveAsset);

            Assert.IsTrue(result.Contains("<link=\"asset:Assets/Materials/Sky.mat\">"), $"Got: {result}");
            Assert.IsTrue(result.Contains("</link>"), $"Got: {result}");
        }

        [Test]
        public void CodeSpan_BareName_AssetResolverReturnsNull_Unchanged()
        {
            // "Player" does NOT start with "Assets/" — no false-positive link.
            var input  = "Edit " + Span("Player") + " now";
            var result = ChatLinkify.Apply(input, _ => null, _ => null, ResolveAsset);

            Assert.AreEqual(input, result);
        }

        [Test]
        public void CodeSpan_ObjectPriority_OverAsset()
        {
            // If obj resolver matches, asset resolver must NOT run.
            var input  = Span("Assets/Foo.prefab");
            var result = ChatLinkify.Apply(input, _ => "/SceneObj", _ => null, ResolveAsset);

            Assert.IsTrue(result.Contains("<link=\"obj:/SceneObj\">"), $"Got: {result}");
            Assert.IsFalse(result.Contains("asset:"), $"Got: {result}");
        }

        [Test]
        public void CodeSpan_ScriptPriority_OverAsset()
        {
            // If script resolver matches, asset resolver must NOT run.
            var input  = Span("Assets/Scripts/Foo.cs");
            var result = ChatLinkify.Apply(input, _ => null, _ => "Assets/Scripts/Foo.cs", ResolveAsset);

            Assert.IsTrue(result.Contains("<link=\"script:Assets/Scripts/Foo.cs\">"), $"Got: {result}");
            Assert.IsFalse(result.Contains("asset:"), $"Got: {result}");
        }

        // ── ApplyPlainPaths ───────────────────────────────────────────────────

        [Test]
        public void ApplyPlainPaths_BareAssetPath_Linkified()
        {
            var input  = "Use Assets/Materials/Sky.mat in your scene";
            var result = ChatLinkify.ApplyPlainPaths(input, ResolveAsset);

            Assert.IsTrue(result.Contains("<link=\"asset:Assets/Materials/Sky.mat\">"), $"Got: {result}");
            Assert.IsTrue(result.Contains("<u>Assets/Materials/Sky.mat</u>"),           $"Got: {result}");
        }

        [Test]
        public void ApplyPlainPaths_AlreadyLinked_NotDoubled()
        {
            var input  = "<link=\"asset:Assets/Foo.mat\"><u>Assets/Foo.mat</u></link>";
            var result = ChatLinkify.ApplyPlainPaths(input, ResolveAsset);

            var linkCount = System.Text.RegularExpressions.Regex.Matches(result, "<link=").Count;
            Assert.AreEqual(1, linkCount, $"Got: {result}");
        }

        [Test]
        public void ApplyPlainPaths_NonExistentPath_NotLinked()
        {
            // Resolver returns null for paths that don't exist
            var input  = "See Assets/NotExist/Foo.mat for details";
            var result = ChatLinkify.ApplyPlainPaths(input, _ => null);

            Assert.AreEqual(input, result);
        }

        [Test]
        public void ApplyPlainPaths_NoAssetPath_Unchanged()
        {
            var input  = "Just plain text with no asset references";
            var result = ChatLinkify.ApplyPlainPaths(input, ResolveAsset);

            Assert.AreEqual(input, result);
        }

        [Test]
        public void ApplyPlainPaths_NullInput_ReturnsNull()
        {
            Assert.IsNull(ChatLinkify.ApplyPlainPaths(null, ResolveAsset));
        }

        // ── Theme-change regression (Red test for the regex staleness defect) ──
        // Before fix: _span is static readonly — compiled once with the dark-theme color
        // (#9aa5ce). After switching to light theme (#3b5a8a), Apply no longer matches
        // code spans so linkification silently stops working.
        // After fix: GetSpanRegex() rebuilds when MarkdownInline.CodeColor changes.

        [Test]
        public void Apply_AfterThemeSwitchToLight_CodeSpanStillLinked()
        {
            bool wasD = MarkdownInlineFormatter.IsDarkTheme;
            try
            {
                MarkdownInlineFormatter.IsDarkTheme = false;
                var lightColor = MarkdownInline.CodeColor; // "#3b5a8a"
                var input  = $"<color={lightColor}>Script.cs</color>";
                var result = ChatLinkify.Apply(input, null, ResolveScript, null);
                Assert.IsTrue(result.Contains("<link="), $"Link not found after theme switch. Got: {result}");
            }
            finally { MarkdownInlineFormatter.IsDarkTheme = wasD; }
        }

        [Test]
        public void Apply_AfterThemeSwitchBackToDark_CodeSpanStillLinked()
        {
            bool wasD = MarkdownInlineFormatter.IsDarkTheme;
            try
            {
                // Switch to light first to prime any lazy-init, then back to dark
                MarkdownInlineFormatter.IsDarkTheme = false;
                MarkdownInlineFormatter.IsDarkTheme = true;
                var darkColor = MarkdownInline.CodeColor; // "#9aa5ce"
                var input  = $"<color={darkColor}>Script.cs</color>";
                var result = ChatLinkify.Apply(input, null, ResolveScript, null);
                Assert.IsTrue(result.Contains("<link="), $"Link not found after back-to-dark switch. Got: {result}");
            }
            finally { MarkdownInlineFormatter.IsDarkTheme = wasD; }
        }
    }
}
