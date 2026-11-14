// Generated from dsl/tools.biome
// source-sha256=22d5b56170705618537a6cda2e383e185768772939d760f99df4e8e641e19e58
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
