namespace UnityMCP.Editor
{
    // Directive-handling logic for the DSL header (`# @needs`/`@tags`/`@expect`/`@suite-only`).
    // Kept out of PlaytestParser.cs per R-04 / csharp-unity.md file-size convention — that file
    // is already >1400 lines. B09 (compile-time Play-bound-verb rejection under `@needs editmode`)
    // and C06 extend this same partial; PlaytestParser.cs itself gains only the single call site
    // in Parse() (B04).
    internal static partial class PlaytestParser
    {
        /// <summary>Scans the raw pre-INCLUDE script text for `# @directive` header lines.
        /// Single point of contact between the parser and <see cref="PlaytestHeaderScanner"/>.</summary>
        internal static PlaytestHeader ScanHeader(string script) => PlaytestHeaderScanner.Scan(script);
    }
}
