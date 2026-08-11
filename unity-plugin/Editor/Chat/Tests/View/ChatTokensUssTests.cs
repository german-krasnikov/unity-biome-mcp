// TDD barrier tests for Chat.Tokens.uss and IsDarkTheme theming (Phase 0.3).
using NUnit.Framework;
using UnityEditor;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChatTokensUssTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private bool _savedDarkTheme;

        [SetUp]
        public void SaveThemeState()
        {
            _savedDarkTheme = MarkdownInlineFormatter.IsDarkTheme;
        }

        [TearDown]
        public void RestoreThemeState()
        {
            MarkdownInlineFormatter.IsDarkTheme = _savedDarkTheme;
        }

        [Test]
        public void TokensUss_FileExists_LoadsWithoutError()
        {
            var ss = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.StyleSheet>(
                "Packages/com.unity-biome-mcp.editor/Editor/Chat/View/Chat.Tokens.uss");
            Assert.IsNotNull(ss, "Chat.Tokens.uss must exist and be importable");
        }

        [Test]
        public void MarkdownInlineFormatter_IsDarkTheme_SetToTrue_ReadsTrue()
        {
            // Static property must be settable and readable. Compiled default is true
            // but tests run in any Editor skin; we set explicitly to avoid fragility.
            MarkdownInlineFormatter.IsDarkTheme = true;
            Assert.IsTrue(MarkdownInlineFormatter.IsDarkTheme);
        }

        [Test]
        public void MarkdownInlineFormatter_CodeColor_IsDark_IsLavender()
        {
            MarkdownInlineFormatter.IsDarkTheme = true;
            Assert.AreEqual("#9aa5ce", MarkdownInlineFormatter.CodeColor);
        }

        [Test]
        public void MarkdownInlineFormatter_CodeColor_IsLight_IsDarker()
        {
            MarkdownInlineFormatter.IsDarkTheme = false;
            Assert.AreNotEqual("#9aa5ce", MarkdownInlineFormatter.CodeColor,
                "Light theme CodeColor must differ from dark theme value");
        }
    }
}
