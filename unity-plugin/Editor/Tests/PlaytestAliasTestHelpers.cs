// Shared test helpers for VAL/VAR/INCLUDE alias system tests.
// No instance state; pure static factories.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityMCP.Playtest.Core;

namespace UnityMCP.Editor.Tests
{
    internal static class AliasHelpers
    {
        /// <summary>Build a script with N VAL definitions, optionally followed by suffix lines.</summary>
        internal static string NVals(int count, string suffix = "") =>
            string.Join("\n", Enumerable.Range(0, count).Select(i =>
                $"VAL $alias{i} /Path/Object{i}|Comp{i}|field{i}")) + "\n" + suffix;

        /// <summary>Build a chain: $v0 → literal ROOT, $v1 → $v0, ... $vN → $v(N-1)</summary>
        internal static string ChainVals(int depth)
        {
            var sb = new StringBuilder();
            sb.AppendLine("VAL $v0 ROOT");
            for (int i = 1; i <= depth; i++)
                sb.AppendLine($"VAL $v{i} $v{i-1}");
            sb.AppendLine($"LOG $v{depth}");
            return sb.ToString();
        }

        /// <summary>VarRegistry backed by a flat dictionary for offline tests.</summary>
        internal static PlaytestVarRegistry StubRegistry(Dictionary<string, string> values)
            => new PlaytestVarRegistry((path, comp, field) => {
                var key = $"{path}|{comp}|{field}";
                return values.TryGetValue(key, out var v) ? v
                    : throw new ArgumentException($"No stub for {key}");
            });

        /// <summary>IncludeResolver from an inline filename→content dictionary.</summary>
        internal static IncludeResolver FileMap(Dictionary<string, string> files)
            => filename => files.TryGetValue(filename, out var c) ? c
                : throw new FileNotFoundException($"Test resolver: file not found: {filename}");
    }

    internal static class GridTestHelpers
    {
        /// <summary>Standard GridTest scene default values (EditMode defaults).</summary>
        internal static readonly Dictionary<(string path, string comp, string field), string> GridTestDefaults = new Dictionary<(string, string, string), string>
        {
            { ("/GridPlayer", "GridPlayer", "MoveSpeed"),  "5"     },
            { ("/GridPlayer", "GridPlayer", "GridSize"),   "10"    },
            { ("/GridPlayer", "GridPlayer", "PosX"),       "0"     },
            { ("/GridPlayer", "GridPlayer", "PosZ"),       "0"     },
            { ("/GridPlayer", "GridPlayer", "Score"),      "0"     },
            { ("/GridPlayer", "GridPlayer", "IsMoving"),   "False" },
            { ("/GridPlayer", "GridPlayer", "MoveCount"),  "0"     },
        };

        /// <summary>VarRegistry backed by GridTest scene defaults dictionary.</summary>
        internal static PlaytestVarRegistry MakeRegistryWithValues(
            Dictionary<(string path, string comp, string field), string> values)
            => new PlaytestVarRegistry((path, comp, field) =>
                values.TryGetValue((path, comp, field), out var v) ? v
                    : throw new ArgumentException($"Not found: {path}|{comp}|{field}"));
    }
}
