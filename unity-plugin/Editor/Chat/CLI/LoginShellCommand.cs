// Thin wrapper over ShellHelper — public API unchanged, tests pass without modification.
// Pure helper — no UnityEngine deps beyond ShellHelper, fully NUnit-testable.
using System.Diagnostics;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Chat
{
    public static class LoginShellCommand
    {
        /// <summary>
        /// Wraps s in single-quotes with POSIX-correct escaping of embedded single-quotes.
        /// Each ' becomes '\'' (close-quote, literal-quote, open-quote).
        /// </summary>
        public static string ShellQuoteSingle(string s) =>
            ShellHelper.ShellQuoteSingle(s);

        /// <summary>
        /// Produces the single-string Arguments value for ProcessStartInfo (Unity Mono compatible).
        /// Both script and arg are single-quoted so the OS re-parse cannot split or interpret them.
        /// </summary>
        public static string BuildArguments(string script, string arg) =>
            ShellHelper.BuildLoginShellArgs(script, arg);

        /// <summary>
        /// Factory: creates a ready-to-use ProcessStartInfo for login-shell invocation.
        /// macOS: /bin/zsh  Linux: /bin/bash or /bin/sh  Windows: null
        /// </summary>
        public static ProcessStartInfo Create(string script, string arg) =>
            ShellHelper.CreateLoginShellPsi(script, arg);
    }
}
