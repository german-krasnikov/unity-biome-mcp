using System;
using System.Collections.Generic;
using UnityMCP.Playtest.Core;

namespace UnityMCP.Editor
{
    /// <summary>
    /// C02 — Editor-side Biome command policy for MCP DSL steps. Post-parse, pre-execution
    /// compile-time validation: walks every StepType.Mcp step and rejects reload/recursive/
    /// mode-transition commands and Edit-mode-incompatible runtime commands BEFORE Run()
    /// opens its isolation group or ticks a single step. Reuses the existing CommandRegistry
    /// (IsRegistered/IsRuntime) as the single source of truth — no second registry, no
    /// ScenarioApiDescriptor. Unregistered commands are NOT rejected here — deferred to
    /// CommandRouter's own runtime error path (C03's ReportsFailNotCrash contract).
    /// </summary>
    internal static class PlaytestMcpPolicy
    {
        // Reload / recursive-test / mode-transition commands: allowing these from inside a
        // script would let a script trigger a domain reload, start a nested test run, or
        // rebuild the Player mid-DSL-execution. Hard denylist — independent of registration.
        private static readonly HashSet<string> _hardDenylist = new HashSet<string>(StringComparer.Ordinal)
        {
            "execute_code", "create_script", "sync_unity", "await_compile",
            "smart_build", "run_tests", "run_playtest", "package", "build",
        };

        internal const string HardDenialReason =
            "denied — reload/recursive/mode-transition command not allowed inside a playtest";

        /// <summary>
        /// C04 — runtime defense-in-depth: the same hard denylist <see cref="Validate"/>
        /// enforces at compile time, exposed so ExecuteStep can re-check immediately before
        /// dispatch (belt and suspenders for a step that was constructed directly and never
        /// went through Validate/parsing).
        /// </summary>
        internal static bool IsHardDenied(string cmd) => _hardDenylist.Contains(cmd);

        /// <summary>
        /// Validates every MCP step in <paramref name="steps"/>. Appends one message per
        /// violation to <paramref name="errors"/> (creating the list on first violation).
        /// Returns the possibly-created list (same instance if no violation was found).
        /// </summary>
        internal static List<string> Validate(List<PlaytestStep> steps, List<string> errors, bool isEditModeRun)
        {
            foreach (var step in steps)
            {
                if (step.Type != StepType.Mcp) continue;
                var reason = CheckStep(step, isEditModeRun);
                if (reason == null) continue;
                errors ??= new List<string>();
                errors.Add($"MCP {step.Method}: {reason}");
            }
            return errors;
        }

        private static string CheckStep(PlaytestStep step, bool isEditModeRun)
        {
            var cmd = step.Method;
            if (_hardDenylist.Contains(cmd))
                return HardDenialReason;
            if (cmd == "editor")
            {
                var action = JsonHelper.ExtractString(step.Args, "action");
                if (action == "play" || action == "stop" || action == "pause" || action == "unpause")
                    return "denied — Play Mode transitions are not allowed inside a playtest";
            }
            if (isEditModeRun && CommandRegistry.IsRuntime(cmd))
                return "requires Play Mode — not usable in an Edit-mode playtest";
            return null;
        }
    }
}
