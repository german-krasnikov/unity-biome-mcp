// Generated from dsl/decisions/permission.logic
// source-sha256=fcc95aab0a1953b89c2e7e2e8af446f07bb729c0c3ed7cd287e3f763d3a46645
// Edit the source, then run biome generate.

#nullable enable
namespace UnityMCP.Editor.Decisions.Permission {
public static class Policy {
    public interface IChatPermissionInputs { bool CallDangerous(); bool CallMutating(); string ChatMode(); }
    public static class ChatPermissionValues { public const string Allow = "allow"; public const string DenyAsk = "denyAsk"; public const string DenyDangerous = "denyDangerous"; public const string DenyUnknownMode = "denyUnknownMode"; }
    public readonly struct ChatPermissionInput { public bool CallDangerous { get; } public bool CallMutating { get; } public string ChatMode { get; } public ChatPermissionInput(bool CallDangerous, bool CallMutating, string ChatMode) { this.CallDangerous = CallDangerous; this.CallMutating = CallMutating; this.ChatMode = ChatMode; } }
    public static string ChatPermission(IChatPermissionInputs input) {
        var value2 = input.CallDangerous();
        var value1 = input.CallMutating();
        var value0 = input.ChatMode();
        if (value0 is null || (value0 != "external" && value0 != "ask" && value0 != "agent" && value0 != "fullAccess" && value0 != "unknown")) throw new System.ArgumentOutOfRangeException("chat.mode", "LOGIC_INPUT_CONTRACT");
        return Decision_chatPermission.Evaluate(value2, value1, value0);
    }
    public static string ChatPermission(ChatPermissionInput input) {
        var value2 = input.CallDangerous;
        var value1 = input.CallMutating;
        var value0 = input.ChatMode;
        if (value0 is null || (value0 != "external" && value0 != "ask" && value0 != "agent" && value0 != "fullAccess" && value0 != "unknown")) throw new System.ArgumentOutOfRangeException("chat.mode", "LOGIC_INPUT_CONTRACT");
        return Decision_chatPermission.Evaluate(value2, value1, value0);
    }
    public interface IInputs : IChatPermissionInputs { }

        private static class Decision_chatPermission
        {
            public static string Evaluate(bool value2, bool value1, string value0)
            {
                // rule: external (priority 10)
                if (value0 == "external") return "allow";
                // rule: unknownMode (priority 11)
                if (value0 == "unknown") return "denyUnknownMode";
                // rule: fullAccess (priority 12)
                if (value0 == "fullAccess") return "allow";
                // rule: agentDangerous (priority 20)
                if ((value0 == "agent") && (value2 == true)) return "denyDangerous";
                // rule: askBlocked (priority 30)
                if ((value0 == "ask") && ((value1 == true) || (value2 == true))) return "denyAsk";
                return "allow";
            }
        }

}
}
