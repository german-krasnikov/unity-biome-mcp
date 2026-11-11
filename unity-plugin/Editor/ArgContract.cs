using System.Collections.Generic;

namespace UnityMCP.Editor
{
    // Gate for CommandValidator.Validate on the DIRECT (non-batch) dispatch path. BatchHelper.cs
    // already validates every batch sub-command unconditionally (BatchHelper.cs:144); this brings
    // CommandRouter.Process/ProcessAsync to the same strictness. Scaffold commit (F1c 1/2): the
    // roster starts empty, so Check() is a no-op for every command until a later commit enables it.
    internal static class ArgContract
    {
        private static readonly HashSet<string> _enabled = new HashSet<string>
        {
        };

        internal static string Check(string cmd, string argsJson) =>
            _enabled.Contains(cmd) ? CommandValidator.Validate(cmd, argsJson) : null;

        internal static IEnumerable<string> EnabledForTest => _enabled;
    }
}
