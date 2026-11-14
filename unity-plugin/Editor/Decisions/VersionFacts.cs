using System.Text.RegularExpressions;

namespace UnityMCP.Editor.Decisions
{
    /// <summary>
    /// The facts of the decision isNewer (dsl/decisions/versionorder.contract): parse X.Y.Z[-pre] on each side, compare
    /// the triples, and note whether either side carries a pre-release suffix. The rule is VersionOrder.Policy.IsNewer
    /// (dsl/decisions/versionorder.logic); nothing here decides. Replaces the three hand-written copies (UpdateChecker.
    /// IsNewer, ChangelogReader.IsNewerThan, ReleaseDiff.IsNewer). Pure: no Unity, no I/O.
    /// </summary>
    internal static class VersionFacts
    {
        static readonly Regex Pattern = new Regex(@"^(\d+)\.(\d+)\.(\d+)(?:-([0-9A-Za-z.]+))?$");

        internal static bool IsNewer(string candidate, string current) =>
            VersionOrder.Policy.IsNewer(Read(candidate, current));

        internal static VersionOrder.Policy.IsNewerInput Read(string candidate, string current)
        {
            var a = Pattern.Match(candidate ?? "");
            var b = Pattern.Match(current ?? "");
            var parsed = a.Success && b.Success;
            var triple = "equal";
            if (parsed)
            {
                var ta = new System.Version(int.Parse(a.Groups[1].Value), int.Parse(a.Groups[2].Value), int.Parse(a.Groups[3].Value));
                var tb = new System.Version(int.Parse(b.Groups[1].Value), int.Parse(b.Groups[2].Value), int.Parse(b.Groups[3].Value));
                triple = ta > tb ? "greater" : ta < tb ? "less" : "equal";
            }
            return new VersionOrder.Policy.IsNewerInput(
                VerCandPre: a.Groups[4].Success, VerCurPre: b.Groups[4].Success, VerParsed: parsed, VerTriple: triple);
        }
    }
}
