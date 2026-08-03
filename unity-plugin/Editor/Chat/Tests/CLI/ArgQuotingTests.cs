using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ArgQuotingTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ── Quote (platform-dispatch — existing, macOS/Posix context) ────────────
        [Test] public void Simple_Unchanged()          => Assert.AreEqual("code-reviewer",        ArgQuoting.Quote("code-reviewer"));
        [Test] public void Plain_Unchanged()            => Assert.AreEqual("plain",                ArgQuoting.Quote("plain"));
        [Test] public void Empty_QuotedEmpty()          => Assert.AreEqual("\"\"",                 ArgQuoting.Quote(""));
        [Test] public void Null_QuotedEmpty()           => Assert.AreEqual("\"\"",                 ArgQuoting.Quote(null));
        [Test] public void Space_Wrapped()              => Assert.AreEqual("\"a b\"",              ArgQuoting.Quote("a b"));
        [Test] public void PathWithSpace_Wrapped()      => Assert.AreEqual("\"/Users/John Doe/x\"", ArgQuoting.Quote("/Users/John Doe/x"));
        [Test] public void EmbeddedQuotes_Escaped()     => Assert.AreEqual("\"say \\\"hi\\\"\"",   ArgQuoting.Quote("say \"hi\""));
        [Test] public void Backslash_EscapedAndWrapped() => Assert.AreEqual("\"a\\\\b\"",          ArgQuoting.Quote("a\\b"));
        [Test] public void Dollar_WrappedNotEscaped()   => Assert.AreEqual("\"$HOME\"",            ArgQuoting.Quote("$HOME"));

        // ── QuotePosix — explicit tests ──────────────────────────────────────────
        [Test] public void QuotePosix_PlainPath_Unchanged()
            => Assert.AreEqual("/usr/bin/python3", ArgQuoting.QuotePosix("/usr/bin/python3"));

        [Test] public void QuotePosix_Backslash_Doubled()
            => Assert.AreEqual("\"a\\\\b\"", ArgQuoting.QuotePosix("a\\b"));

        [Test] public void QuotePosix_EmbeddedQuote_Escaped()
            => Assert.AreEqual("\"say \\\"hi\\\"\"", ArgQuoting.QuotePosix("say \"hi\""));

        [Test] public void QuotePosix_PathWithSpaces_Wrapped()
            => Assert.AreEqual("\"/Users/John Doe/x\"", ArgQuoting.QuotePosix("/Users/John Doe/x"));

        // ── QuoteWindows — MSVC CRT rules ────────────────────────────────────────

        // Normal Windows path: backslashes before non-quote are literal, NOT doubled.
        [Test] public void QuoteWindows_NormalPath_BackslashesLiteral()
            => Assert.AreEqual("\"C:\\Users\\YURSSS\\python.exe\"",
                ArgQuoting.QuoteWindows("C:\\Users\\YURSSS\\python.exe"));

        // Trailing backslash before closing " must be doubled.
        [Test] public void QuoteWindows_TrailingBackslash_Doubled()
            => Assert.AreEqual("\"C:\\Dir\\\\\"", ArgQuoting.QuoteWindows("C:\\Dir\\"));

        // Backslash immediately before an embedded " must be doubled then quote escaped.
        [Test] public void QuoteWindows_BackslashBeforeEmbeddedQuote_DoubledPlusEscape()
            => Assert.AreEqual("\"say\\\\\\\"hi\\\"\"", ArgQuoting.QuoteWindows("say\\\"hi\""));

        // TOML command value: mcp_servers.unity.command="C:\\Users\\python.exe"
        // Backslashes before non-quote are literal; embedded " becomes \".
        [Test] public void QuoteWindows_TomlCommandValue_Roundtrips()
        {
            // Input: mcp_servers.unity.command="C:\\Users\\python.exe"
            // (where \\ is the TOML escape for a single backslash)
            var input    = "mcp_servers.unity.command=\"C:\\\\Users\\\\python.exe\"";
            var quoted   = ArgQuoting.QuoteWindows(input);
            // CRT roundtrip: outer quotes stripped, \" → ", \\ (before non-quote) → \\
            // So CRT delivers: mcp_servers.unity.command="C:\\Users\\python.exe" — correct TOML
            StringAssert.StartsWith("\"", quoted);
            StringAssert.EndsWith("\"", quoted);
            // The embedded " after = must be escaped as \"
            StringAssert.Contains("\\\"", quoted);
        }

        // TOML args array: mcp_servers.unity.args=["-m","unity_mcp.server"]
        [Test] public void QuoteWindows_TomlArgsArray_Roundtrips()
        {
            var input  = "mcp_servers.unity.args=[\"-m\",\"unity_mcp.server\"]";
            var quoted = ArgQuoting.QuoteWindows(input);
            StringAssert.StartsWith("\"", quoted);
            StringAssert.EndsWith("\"", quoted);
            StringAssert.Contains("\\\"", quoted);
        }

        // Plain path with no special chars — just wrapped, no escaping needed.
        [Test] public void QuoteWindows_PlainPath_WrappedOnly()
            => Assert.AreEqual("\"C:\\Users\\dev\\python.exe\"",
                ArgQuoting.QuoteWindows("C:\\Users\\dev\\python.exe"));

        // Two trailing backslashes → four (doubled).
        [Test] public void QuoteWindows_TwoTrailingBackslashes_Quadrupled()
            => Assert.AreEqual("\"dir\\\\\\\\\"", ArgQuoting.QuoteWindows("dir\\\\"));
    }
}
