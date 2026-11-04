// Generated from dsl/decisions/guards.logic
// source-sha256=cf256335259db6c22be663a214b209a2d6b557d2481bec10c87535561d64a82c
// Edit the source, then run biome generate.

#nullable enable
namespace UnityMCP.Editor.Decisions.Guards {
public static class Policy {
    public interface IGuardVerdictInputs { bool GuardAllowedDuringCompile(); bool GuardAllowedInPlay(); bool GuardAlwaysAllowed(); bool GuardAuthDenied(); bool GuardBatchable(); bool GuardCompiling(); bool GuardMutating(); bool GuardPlayKnown(); bool GuardPlayMode(); bool GuardPythonOnly(); bool GuardReadOnly(); bool GuardReady(); bool GuardRuntime(); string GuardSite(); bool GuardToolEnabled(); }
    public static class GuardVerdictValues { public const string AsyncOnly = "asyncOnly"; public const string AuthDenied = "authDenied"; public const string Compiling = "compiling"; public const string Disabled = "disabled"; public const string Initializing = "initializing"; public const string NotInPlay = "notInPlay"; public const string Ok = "ok"; public const string PlayBlocked = "playBlocked"; public const string PythonOnly = "pythonOnly"; public const string ReadOnlyBlocked = "readOnlyBlocked"; }
    public readonly struct GuardVerdictInput { public bool GuardAllowedDuringCompile { get; } public bool GuardAllowedInPlay { get; } public bool GuardAlwaysAllowed { get; } public bool GuardAuthDenied { get; } public bool GuardBatchable { get; } public bool GuardCompiling { get; } public bool GuardMutating { get; } public bool GuardPlayKnown { get; } public bool GuardPlayMode { get; } public bool GuardPythonOnly { get; } public bool GuardReadOnly { get; } public bool GuardReady { get; } public bool GuardRuntime { get; } public string GuardSite { get; } public bool GuardToolEnabled { get; } public GuardVerdictInput(bool GuardAllowedDuringCompile, bool GuardAllowedInPlay, bool GuardAlwaysAllowed, bool GuardAuthDenied, bool GuardBatchable, bool GuardCompiling, bool GuardMutating, bool GuardPlayKnown, bool GuardPlayMode, bool GuardPythonOnly, bool GuardReadOnly, bool GuardReady, bool GuardRuntime, string GuardSite, bool GuardToolEnabled) { this.GuardAllowedDuringCompile = GuardAllowedDuringCompile; this.GuardAllowedInPlay = GuardAllowedInPlay; this.GuardAlwaysAllowed = GuardAlwaysAllowed; this.GuardAuthDenied = GuardAuthDenied; this.GuardBatchable = GuardBatchable; this.GuardCompiling = GuardCompiling; this.GuardMutating = GuardMutating; this.GuardPlayKnown = GuardPlayKnown; this.GuardPlayMode = GuardPlayMode; this.GuardPythonOnly = GuardPythonOnly; this.GuardReadOnly = GuardReadOnly; this.GuardReady = GuardReady; this.GuardRuntime = GuardRuntime; this.GuardSite = GuardSite; this.GuardToolEnabled = GuardToolEnabled; } }
    public static string GuardVerdict(IGuardVerdictInputs input) {
        var value6 = input.GuardAllowedDuringCompile();
        var value10 = input.GuardAllowedInPlay();
        var value13 = input.GuardAlwaysAllowed();
        var value4 = input.GuardAuthDenied();
        var value2 = input.GuardBatchable();
        var value5 = input.GuardCompiling();
        var value9 = input.GuardMutating();
        var value8 = input.GuardPlayKnown();
        var value7 = input.GuardPlayMode();
        var value3 = input.GuardPythonOnly();
        var value12 = input.GuardReadOnly();
        var value1 = input.GuardReady();
        var value11 = input.GuardRuntime();
        var value0 = input.GuardSite();
        if (value0 is null || (value0 != "top" && value0 != "batch" && value0 != "py")) throw new System.ArgumentOutOfRangeException("guard.site", "LOGIC_INPUT_CONTRACT");
        var value14 = input.GuardToolEnabled();
        return Decision_guardVerdict.Evaluate(value6, value10, value13, value4, value2, value5, value9, value8, value7, value3, value12, value1, value11, value0, value14);
    }
    public static string GuardVerdict(GuardVerdictInput input) {
        var value6 = input.GuardAllowedDuringCompile;
        var value10 = input.GuardAllowedInPlay;
        var value13 = input.GuardAlwaysAllowed;
        var value4 = input.GuardAuthDenied;
        var value2 = input.GuardBatchable;
        var value5 = input.GuardCompiling;
        var value9 = input.GuardMutating;
        var value8 = input.GuardPlayKnown;
        var value7 = input.GuardPlayMode;
        var value3 = input.GuardPythonOnly;
        var value12 = input.GuardReadOnly;
        var value1 = input.GuardReady;
        var value11 = input.GuardRuntime;
        var value0 = input.GuardSite;
        if (value0 is null || (value0 != "top" && value0 != "batch" && value0 != "py")) throw new System.ArgumentOutOfRangeException("guard.site", "LOGIC_INPUT_CONTRACT");
        var value14 = input.GuardToolEnabled;
        return Decision_guardVerdict.Evaluate(value6, value10, value13, value4, value2, value5, value9, value8, value7, value3, value12, value1, value11, value0, value14);
    }
    public interface IInputs : IGuardVerdictInputs { }

        private static class Decision_guardVerdict
        {
            public static string Evaluate(bool value6, bool value10, bool value13, bool value4, bool value2, bool value5, bool value9, bool value8, bool value7, bool value3, bool value12, bool value1, bool value11, string value0, bool value14)
            {
                // rule: topNotReady (priority 10)
                if ((value0 == "top") && (!(value1 == true))) return "initializing";
                // rule: topPythonOnly (priority 11)
                if ((value0 == "top") && (value3 == true)) return "pythonOnly";
                // rule: topAuth (priority 12)
                if ((value0 == "top") && (value4 == true)) return "authDenied";
                // rule: topCompiling (priority 13)
                if (((value0 == "top") && (value5 == true)) && (!(value6 == true))) return "compiling";
                // rule: topPlayBlocked (priority 14)
                if ((((value0 == "top") && (value7 == true)) && (value9 == true)) && (!(value10 == true))) return "playBlocked";
                // rule: topNotInPlay (priority 15)
                if (((value0 == "top") && (!(value7 == true))) && (value11 == true)) return "notInPlay";
                // rule: topReadOnly (priority 16)
                if (((value0 == "top") && (value12 == true)) && (value9 == true)) return "readOnlyBlocked";
                // rule: topDisabled (priority 17)
                if (((value0 == "top") && (!(value13 == true))) && (!(value14 == true))) return "disabled";
                // rule: batchAsyncOnly (priority 20)
                if ((value0 == "batch") && (!(value2 == true))) return "asyncOnly";
                // rule: batchPlayBlocked (priority 21)
                if ((((value0 == "batch") && (value7 == true)) && (value9 == true)) && (!(value10 == true))) return "playBlocked";
                // rule: batchNotInPlay (priority 22)
                if (((value0 == "batch") && (!(value7 == true))) && (value11 == true)) return "notInPlay";
                // rule: batchCompiling (priority 23)
                if ((((value0 == "batch") && (value5 == true)) && (value9 == true)) && (!(value6 == true))) return "compiling";
                // rule: batchReadOnly (priority 24)
                if (((value0 == "batch") && (value12 == true)) && (value9 == true)) return "readOnlyBlocked";
                // rule: batchDisabled (priority 25)
                if (((value0 == "batch") && (!(value13 == true))) && (!(value14 == true))) return "disabled";
                // rule: batchNotReady (priority 26)
                if ((value0 == "batch") && (!(value1 == true))) return "initializing";
                // rule: pyReadOnly (priority 30)
                if (((value0 == "py") && (value12 == true)) && (value9 == true)) return "readOnlyBlocked";
                // rule: pyNotInPlay (priority 31)
                if ((((value0 == "py") && (value8 == true)) && (!(value7 == true))) && (value11 == true)) return "notInPlay";
                // rule: pyPythonOnly (priority 32)
                if ((value0 == "py") && (value3 == true)) return "pythonOnly";
                return "ok";
            }
        }

}
}
