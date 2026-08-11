// Post-processes rich text: wraps resolved <color=#9aa5ce> spans in <link> tags.
// PURE — zero UnityEngine/UnityEditor dependencies. NUnit-testable.
using System.Text.RegularExpressions;

namespace UnityMCP.Editor.Chat
{
    public static class ChatLinkify
    {
        internal delegate string Resolver(string name);

        // Matches bare asset paths in plain prose (must have extension, outside existing <link> tags).
        private static readonly Regex _plainPath = new Regex(
            @"Assets/[A-Za-z0-9_.\-/]+\.[A-Za-z0-9]+",
            RegexOptions.Compiled);

        // Matches <color=CODECOLOR>CONTENT</color> — non-greedy, handles nested tags.
        // Rebuilt lazily whenever MarkdownInline.CodeColor changes (e.g. dark→light theme).
        // static readonly would compile once and then silently stop matching after a theme switch.
        private static Regex _span;
        private static string _spanColor;

        private static Regex GetSpanRegex()
        {
            var current = MarkdownInline.CodeColor;
            if (_span == null || current != _spanColor)
            {
                _spanColor = current;
                _span = new Regex(
                    @"<color=" + Regex.Escape(current) + @">(.*?)</color>",
                    RegexOptions.Compiled | RegexOptions.Singleline);
            }
            return _span;
        }

        // Detects if a code span is already inside a <link ...> </link> pair.
        // We look for <link= that is NOT closed before our match position.
        private static readonly Regex _linkOpen  = new Regex(@"<link=",   RegexOptions.Compiled);
        private static readonly Regex _linkClose = new Regex(@"</link>",  RegexOptions.Compiled);

        /// <summary>
        /// Scans richText for inline-code color spans and wraps resolved ones in
        /// &lt;link&gt; tags with underline. Unresolved spans pass through unchanged.
        /// Priority: object > script > asset.
        /// Asset resolver should be path-based (span must start with "Assets/") to avoid
        /// false-positive links on bare names.
        /// </summary>
        internal static string Apply(string richText, Resolver resolveObject, Resolver resolveScript, Resolver resolveAsset)
        {
            if (richText == null) return null;
            if (richText == "") return "";

            return GetSpanRegex().Replace(richText, m =>
            {
                // Skip spans already inside a <link> tag by counting opens/closes before this match.
                var before     = richText.Substring(0, m.Index);
                var openCount  = _linkOpen.Matches(before).Count;
                var closeCount = _linkClose.Matches(before).Count;
                if (openCount > closeCount) return m.Value; // inside existing link

                var inner = m.Groups[1].Value;          // rich text inside the color span
                var name  = StripNoparse(inner);         // clean name for resolver lookup

                var payload = resolveObject?.Invoke(name);
                if (payload != null)
                    return WrapLink("obj:" + payload, m.Value);

                payload = resolveScript?.Invoke(name);
                if (payload != null)
                    return WrapLink("script:" + payload, m.Value);

                payload = resolveAsset?.Invoke(name);
                if (payload != null)
                    return WrapLink("asset:" + payload, m.Value);

                return m.Value; // unresolved — pass through unchanged
            });
        }

        /// <summary>
        /// Second pass: wraps bare <c>Assets/...</c> paths in prose (outside existing link tags)
        /// in &lt;link&gt; + &lt;u&gt; tags. Resolver must return non-null for the path to be linkified.
        /// </summary>
        internal static string ApplyPlainPaths(string richText, Resolver resolveAsset)
        {
            if (richText == null) return null;
            if (richText == "") return "";

            return _plainPath.Replace(richText, m =>
            {
                // Skip paths already inside a <link> tag.
                var before     = richText.Substring(0, m.Index);
                var openCount  = _linkOpen.Matches(before).Count;
                var closeCount = _linkClose.Matches(before).Count;
                if (openCount > closeCount) return m.Value;

                var path = m.Value;
                var resolved = resolveAsset?.Invoke(path);
                if (resolved == null) return m.Value;

                return "<link=\"asset:" + resolved + "\"><u>" + path + "</u></link>";
            });
        }

        // Wraps colorSpan with <link> + <u> to signal clickability visually.
        private static string WrapLink(string linkId, string colorSpan) =>
            "<link=\"" + linkId + "\"><u>" + colorSpan + "</u></link>";

        /// <summary>Strips &lt;noparse&gt;X&lt;/noparse&gt; pairs, returning the inner X.
        /// Used to reconstruct the plain identifier name for resolver lookup.</summary>
        internal static string StripNoparse(string s)
        {
            if (string.IsNullOrEmpty(s)) return s ?? "";
            // <noparse><</noparse> -> <  (MarkdownInline.Escape pattern)
            return Regex.Replace(s, @"<noparse>(.*?)</noparse>", "$1");
        }
    }
}
