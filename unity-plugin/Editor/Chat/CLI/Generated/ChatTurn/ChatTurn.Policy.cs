// Generated from dsl/chat/chatturn.logic
// source-sha256=78c4368f2e9821b428c717c626db20eae897a5e74d7c0b5bfdfb23027c835386
// Edit the source, then run biome generate.

#nullable enable
namespace UnityMCP.Editor.Chat.Turn {
public static class Policy {
    public interface ICanSendInputs { string ChatTurnPhase(); }
    public readonly struct CanSendInput { public string ChatTurnPhase { get; } public CanSendInput(string ChatTurnPhase) { this.ChatTurnPhase = ChatTurnPhase; } }
    public static bool CanSend(ICanSendInputs input) {
        var value0 = input.ChatTurnPhase();
        if (value0 is null || (value0 != "idle" && value0 != "sending" && value0 != "receiving")) throw new System.ArgumentOutOfRangeException("chatTurn.phase", "LOGIC_INPUT_CONTRACT");
        return Decision_canSend.Evaluate(value0);
    }
    public static bool CanSend(CanSendInput input) {
        var value0 = input.ChatTurnPhase;
        if (value0 is null || (value0 != "idle" && value0 != "sending" && value0 != "receiving")) throw new System.ArgumentOutOfRangeException("chatTurn.phase", "LOGIC_INPUT_CONTRACT");
        return Decision_canSend.Evaluate(value0);
    }
    public interface IActiveInputs { string ChatTurnPhase(); }
    public readonly struct ActiveInput { public string ChatTurnPhase { get; } public ActiveInput(string ChatTurnPhase) { this.ChatTurnPhase = ChatTurnPhase; } }
    public static bool Active(IActiveInputs input) {
        var value0 = input.ChatTurnPhase();
        if (value0 is null || (value0 != "idle" && value0 != "sending" && value0 != "receiving")) throw new System.ArgumentOutOfRangeException("chatTurn.phase", "LOGIC_INPUT_CONTRACT");
        return Decision_active.Evaluate(value0);
    }
    public static bool Active(ActiveInput input) {
        var value0 = input.ChatTurnPhase;
        if (value0 is null || (value0 != "idle" && value0 != "sending" && value0 != "receiving")) throw new System.ArgumentOutOfRangeException("chatTurn.phase", "LOGIC_INPUT_CONTRACT");
        return Decision_active.Evaluate(value0);
    }
    public interface IInputs : ICanSendInputs, IActiveInputs { }

        private static class Decision_canSend
        {
            public static bool Evaluate(string value0)
            {
                if (value0 == "idle") return true;
                return false;
            }
        }

        private static class Decision_active
        {
            public static bool Evaluate(string value0)
            {
                if (value0 == "idle") return false;
                return true;
            }
        }

}
}
