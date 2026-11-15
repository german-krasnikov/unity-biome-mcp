// Generated from dsl/tools.biome
// source-sha256=6de6b4caaca35bf930d6987f5702fc0a1ea4bfa2cd356e4abf9a93271f817036
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
