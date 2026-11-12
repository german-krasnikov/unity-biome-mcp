// Generated from dsl/decisions/versionorder.logic
// source-sha256=98759a13063a38f1172f16849cd254912daf41c78eaa38802818008f2245ff4c
// Edit the source, then run biome generate.

#nullable enable
namespace UnityMCP.Editor.Decisions.VersionOrder {
public static class Policy {
    public interface IIsNewerInputs { bool VerCandPre(); bool VerCurPre(); bool VerParsed(); string VerTriple(); }
    public readonly struct IsNewerInput { public bool VerCandPre { get; } public bool VerCurPre { get; } public bool VerParsed { get; } public string VerTriple { get; } public IsNewerInput(bool VerCandPre, bool VerCurPre, bool VerParsed, string VerTriple) { this.VerCandPre = VerCandPre; this.VerCurPre = VerCurPre; this.VerParsed = VerParsed; this.VerTriple = VerTriple; } }
    public static bool IsNewer(IIsNewerInputs input) {
        var value2 = input.VerCandPre();
        var value3 = input.VerCurPre();
        var value0 = input.VerParsed();
        var value1 = input.VerTriple();
        if (value1 is null || (value1 != "less" && value1 != "equal" && value1 != "greater")) throw new System.ArgumentOutOfRangeException("ver.triple", "LOGIC_INPUT_CONTRACT");
        return Decision_isNewer.Evaluate(value2, value3, value0, value1);
    }
    public static bool IsNewer(IsNewerInput input) {
        var value2 = input.VerCandPre;
        var value3 = input.VerCurPre;
        var value0 = input.VerParsed;
        var value1 = input.VerTriple;
        if (value1 is null || (value1 != "less" && value1 != "equal" && value1 != "greater")) throw new System.ArgumentOutOfRangeException("ver.triple", "LOGIC_INPUT_CONTRACT");
        return Decision_isNewer.Evaluate(value2, value3, value0, value1);
    }
    public interface IInputs : IIsNewerInputs { }

        private static class Decision_isNewer
        {
            public static bool Evaluate(bool value2, bool value3, bool value0, string value1)
            {
                // rule: unparsed (priority 5)
                if (!(value0 == true)) return false;
                // rule: stableUserNeverSeesRc (priority 10)
                if ((value2 == true) && (!(value3 == true))) return false;
                // rule: higher (priority 20)
                if (value1 == "greater") return true;
                // rule: releaseOverItsRc (priority 30)
                if (((value1 == "equal") && (value3 == true)) && (!(value2 == true))) return true;
                return false;
            }
        }

}
}
