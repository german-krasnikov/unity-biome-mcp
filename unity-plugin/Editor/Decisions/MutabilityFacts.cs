namespace UnityMCP.Editor.Decisions
{
    /// <summary>
    /// The facts of the decision isMutating (dsl/decisions/mutability.contract) that a wire call states, read the way the Python side reads
    /// them (middleware_types.is_write): which of the registry's bases the command has, whether its action is one of its read actions, and the
    /// literal of the switch arguments. The rule is Mutability.Policy.IsMutating (dsl/decisions/mutability.logic); nothing here decides.
    /// Pure: no registry, no Unity. CommandRegistry.IsMutating supplies the base.
    /// </summary>
    internal static class MutabilityFacts
    {
        /// <param name="callBase">"read" or "write" by the registration's mutating flag, "unknown" for a command that is not registered.</param>
        internal static Mutability.Policy.IsMutatingInput Read(string cmd, string argsJson, string callBase)
        {
            var action = JsonHelper.ExtractString(argsJson, "action") ?? "";
            // navmesh is the wire command that the tool navmesh_query sends; the generated table of read actions is keyed by tool name.
            var actionRead = ActionReads.IsRead(cmd == "navmesh" ? "navmesh_query" : cmd, action);
            return new Mutability.Policy.IsMutatingInput(
                ArgsAbortOnFail: Switch(argsJson, "abort_on_fail"),
                ArgsAction: action,
                ArgsClear: Switch(argsJson, "clear"),
                ArgsFix: Switch(argsJson, "fix"),
                ArgsHasEnable: JsonHelper.ExtractString(argsJson, "enable") != null,
                ArgsReset: Switch(argsJson, "reset"),
                CallActionRead: actionRead,
                CallBase: callBase,
                CallCmd: cmd ?? "",
                CallUnknownIsWrite: false);
        }

        // absent (no key, or JSON null), off (the literal false) or on (any other value); the contract's enum for a switch argument
        private static string Switch(string argsJson, string key)
        {
            var value = JsonHelper.ExtractString(argsJson, key);
            return value == null ? "absent" : value == "false" ? "off" : "on";
        }
    }
}
