// Generated from dsl/tools.biome
// source-sha256=385b6968907572e67385e5e7dc2e105b35a7e35bfd730786d74a3fea06784905
// Edit the source, then run biome generate.

using System;
using System.Collections.Generic;

namespace UnityMCP.Editor
{
    internal static partial class ToolUi
    {
        internal static readonly HashSet<string> PlaytestDenied = new HashSet<string>(StringComparer.Ordinal)
        {
        "await_compile",
        "batch",
        "build",
        "execute_code",
        "package",
        "run_playtest",
        "run_tests",
        "smart_build",
        "source_patch_write",
        "sync_unity",
        };
    }
}
