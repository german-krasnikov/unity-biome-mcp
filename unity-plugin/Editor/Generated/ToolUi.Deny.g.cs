// Generated from dsl/tools.biome
// source-sha256=a289d8e839f1aa9c56508f5b97d6ed475b9c881415eb3e2c725523f041e39a22
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
