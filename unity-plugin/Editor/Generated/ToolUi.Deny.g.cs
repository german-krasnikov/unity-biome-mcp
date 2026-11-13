// Generated from dsl/tools.biome
// source-sha256=ef8eb2d7c3bba65c3484358fe680dbde4b719151b50f56ba6d58c13fdc3b5de4
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
