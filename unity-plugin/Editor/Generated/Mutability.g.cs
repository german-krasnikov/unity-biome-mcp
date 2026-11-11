// Generated from dsl/decisions/mutability.logic
// source-sha256=bbb7f17ec975588d28322ea95ba3d8df008e3a102e89e7d2f7426afbf7ef3722
// Edit the source, then run biome generate.

#nullable enable
namespace UnityMCP.Editor.Decisions.Mutability {
public static class Policy {
    public interface IIsMutatingInputs { string ArgsAbortOnFail(); string ArgsAction(); string ArgsClear(); string ArgsFix(); bool ArgsHasEnable(); string ArgsReset(); bool CallActionRead(); string CallBase(); string CallCmd(); bool CallUnknownIsWrite(); }
    public readonly struct IsMutatingInput { public string ArgsAbortOnFail { get; } public string ArgsAction { get; } public string ArgsClear { get; } public string ArgsFix { get; } public bool ArgsHasEnable { get; } public string ArgsReset { get; } public bool CallActionRead { get; } public string CallBase { get; } public string CallCmd { get; } public bool CallUnknownIsWrite { get; } public IsMutatingInput(string ArgsAbortOnFail, string ArgsAction, string ArgsClear, string ArgsFix, bool ArgsHasEnable, string ArgsReset, bool CallActionRead, string CallBase, string CallCmd, bool CallUnknownIsWrite) { this.ArgsAbortOnFail = ArgsAbortOnFail; this.ArgsAction = ArgsAction; this.ArgsClear = ArgsClear; this.ArgsFix = ArgsFix; this.ArgsHasEnable = ArgsHasEnable; this.ArgsReset = ArgsReset; this.CallActionRead = CallActionRead; this.CallBase = CallBase; this.CallCmd = CallCmd; this.CallUnknownIsWrite = CallUnknownIsWrite; } }
    public static bool IsMutating(IIsMutatingInputs input) {
        var value8 = input.ArgsAbortOnFail();
        if (value8 is null || (value8 != "absent" && value8 != "off" && value8 != "on")) throw new System.ArgumentOutOfRangeException("args.abortOnFail", "LOGIC_INPUT_CONTRACT");
        var value4 = input.ArgsAction();
        if (value4 is null) throw new System.ArgumentOutOfRangeException("args.action", "LOGIC_INPUT_CONTRACT");
        var value9 = input.ArgsClear();
        if (value9 is null || (value9 != "absent" && value9 != "off" && value9 != "on")) throw new System.ArgumentOutOfRangeException("args.clear", "LOGIC_INPUT_CONTRACT");
        var value6 = input.ArgsFix();
        if (value6 is null || (value6 != "absent" && value6 != "off" && value6 != "on")) throw new System.ArgumentOutOfRangeException("args.fix", "LOGIC_INPUT_CONTRACT");
        var value5 = input.ArgsHasEnable();
        var value7 = input.ArgsReset();
        if (value7 is null || (value7 != "absent" && value7 != "off" && value7 != "on")) throw new System.ArgumentOutOfRangeException("args.reset", "LOGIC_INPUT_CONTRACT");
        var value3 = input.CallActionRead();
        var value0 = input.CallBase();
        if (value0 is null || (value0 != "read" && value0 != "write" && value0 != "unknown")) throw new System.ArgumentOutOfRangeException("call.base", "LOGIC_INPUT_CONTRACT");
        var value1 = input.CallCmd();
        if (value1 is null) throw new System.ArgumentOutOfRangeException("call.cmd", "LOGIC_INPUT_CONTRACT");
        var value2 = input.CallUnknownIsWrite();
        return Decision_isMutating.Evaluate(value8, value4, value9, value6, value5, value7, value3, value0, value1, value2);
    }
    public static bool IsMutating(IsMutatingInput input) {
        var value8 = input.ArgsAbortOnFail;
        if (value8 is null || (value8 != "absent" && value8 != "off" && value8 != "on")) throw new System.ArgumentOutOfRangeException("args.abortOnFail", "LOGIC_INPUT_CONTRACT");
        var value4 = input.ArgsAction;
        if (value4 is null) throw new System.ArgumentOutOfRangeException("args.action", "LOGIC_INPUT_CONTRACT");
        var value9 = input.ArgsClear;
        if (value9 is null || (value9 != "absent" && value9 != "off" && value9 != "on")) throw new System.ArgumentOutOfRangeException("args.clear", "LOGIC_INPUT_CONTRACT");
        var value6 = input.ArgsFix;
        if (value6 is null || (value6 != "absent" && value6 != "off" && value6 != "on")) throw new System.ArgumentOutOfRangeException("args.fix", "LOGIC_INPUT_CONTRACT");
        var value5 = input.ArgsHasEnable;
        var value7 = input.ArgsReset;
        if (value7 is null || (value7 != "absent" && value7 != "off" && value7 != "on")) throw new System.ArgumentOutOfRangeException("args.reset", "LOGIC_INPUT_CONTRACT");
        var value3 = input.CallActionRead;
        var value0 = input.CallBase;
        if (value0 is null || (value0 != "read" && value0 != "write" && value0 != "unknown")) throw new System.ArgumentOutOfRangeException("call.base", "LOGIC_INPUT_CONTRACT");
        var value1 = input.CallCmd;
        if (value1 is null) throw new System.ArgumentOutOfRangeException("call.cmd", "LOGIC_INPUT_CONTRACT");
        var value2 = input.CallUnknownIsWrite;
        return Decision_isMutating.Evaluate(value8, value4, value9, value6, value5, value7, value3, value0, value1, value2);
    }
    public interface IInputs : IIsMutatingInputs { }

        private static class Decision_isMutating
        {
            public static bool Evaluate(string value8, string value4, string value9, string value6, bool value5, string value7, bool value3, string value0, string value1, bool value2)
            {
                // rule: readBase (priority 10)
                if (value0 == "read") return false;
                // rule: unknownWrites (priority 11)
                if ((value0 == "unknown") && (value2 == true)) return true;
                // rule: unknownReads (priority 12)
                if (value0 == "unknown") return false;
                // rule: doctorIdle (priority 20)
                if ((value1 == "doctor") && (value6 != "on")) return false;
                // rule: metricsKept (priority 21)
                if ((value1 == "get_metrics") && (value7 != "on")) return false;
                // rule: waitPolls (priority 22)
                if ((value1 == "wait_until") && (value8 != "on")) return false;
                // rule: changesKept (priority 23)
                if ((value1 == "get_changes") && (value9 == "off")) return false;
                // rule: modeQuery (priority 24)
                if (((value1 == "editor") && (value4 == "mutation_mode")) && (!(value5 == true))) return false;
                // rule: actionIsRead (priority 30)
                if (value3 == true) return false;
                return true;
            }
        }

}
}
