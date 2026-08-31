using System;
using System.IO;
using UnityMCP.Editor.SourcePatch;

namespace UnityMCP.Editor
{
    /// <summary>
    /// ON/OFF policy for Source Patch (§3.3/§6 P0-70): the `editor(action=
    /// "mutation_mode")` command body, the `mcp_status` projection, and the
    /// OnReady write dispatch. One coordinator transition table
    /// (<see cref="SourcePatchStateMachine"/>) remains the sole policy source —
    /// this class only sequences calls into it and into
    /// <see cref="SourcePatchHost"/>, it never re-implements legality.
    /// </summary>
    internal static class SourcePatchModePolicy
    {
        /// <summary>Intent is a pure derivation, never a second persisted field
        /// (Refactor requirement: no duplicate mode decision).</summary>
        internal static bool IsIntentOn =>
            SourcePatchHost.CurrentState == SourcePatchState.OnReady ||
            SourcePatchHost.CurrentState == SourcePatchState.Busy;

        internal static string SetMutationIntent(bool? enable)
        {
            if (enable == null) return $"mutation_mode:{(IsIntentOn ? "true" : "false")}";
            return enable.Value ? EnableOn() : RequestDisable();
        }

        internal static string StatusProjection()
        {
            var state = SourcePatchHost.CurrentState;
            var providerPresent = SourcePatchProviderSlot.TryGet(out _);
            SourcePatchReceiptStore.TryRead(out var receipt);
            var op = state == SourcePatchState.Disabling && receipt != null ? receipt.OpId
                : state == SourcePatchState.Busy ? "apply-in-progress"
                : "none";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"source_patch_intent={(IsIntentOn ? "on" : "off")}");
            sb.AppendLine($"source_patch_provider={(providerPresent ? "installed" : "absent")}");
            sb.AppendLine($"source_patch_state={state}");
            sb.AppendLine($"source_patch_op={op}");
            sb.AppendLine($"source_patch_recovery={(state == SourcePatchState.Recovery ? "true" : "false")}");
            return sb.ToString().TrimEnd();
        }

        /// <summary>WriteText's OnReady branch (§6 P0-70 "ON order"). "validate"
        /// is SourcePatchRequest.TryCreate's own structural check; "preflight"
        /// and "admit" happen inside provider.Apply (canonical-Roslyn classifier
        /// + a real DynamicAssemblyCompiler.Compile, P0-60) — this method adds no
        /// separate preflight round-trip. "CAS Before -&gt; acquire lease -&gt; raw
        /// write/readback After -&gt; apply once" is SourcePatchCoordinator.TryApply,
        /// unmodified from P0-40.</summary>
        internal static string TryApplyWrite(string argsJson)
        {
            var coordinator = SourcePatchHost.Coordinator;
            if (coordinator == null)
            {
                // Armed OnReady with no live coordinator only happens when a test
                // forces CurrentState directly, bypassing SetMutationIntent — an
                // inconsistent state in production. Fail closed, never guess.
                SourcePatchHost.CurrentState = SourcePatchState.Recovery;
                throw new InvalidOperationException(
                    "state=OnReady: no armed coordinator — inconsistent state, entering Recovery");
            }

            var path = JsonHelper.ExtractString(argsJson, "path");
            var content = JsonHelper.ExtractString(argsJson, "content") ?? "";
            var newBytes = System.Text.Encoding.UTF8.GetBytes(content);
            var beforeBytes = File.ReadAllBytes(Path.GetFullPath(path));

            if (!SourcePatchRequest.TryCreate(path, beforeBytes, newBytes, out var request))
                throw new InvalidOperationException("source patch request is invalid: path must be an existing .cs file");

            var outcome = coordinator.TryApply(request);
            // Mirror the coordinator's own internal transitions (e.g. Busy ->
            // OnReady on success, or -> Recovery on Drift/Uncertain) back onto
            // the host-level facade SourcePatchHost.CurrentState reads —
            // otherwise mcp_status would report a stale OnReady after a
            // Drift/Uncertain outcome moved the real coordinator to Recovery.
            SourcePatchHost.CurrentState = coordinator.CurrentState;

            switch (outcome)
            {
                case SourcePatchOperationResult.Applied:
                    return $"ok:write\npath:{path}\nsize:{newBytes.Length}";
                case SourcePatchOperationResult.RolledBack:
                    throw new InvalidOperationException("source patch rejected the replacement body; no effect");
                case SourcePatchOperationResult.Drift:
                    throw new InvalidOperationException("source patch detected external drift; entering Recovery");
                case SourcePatchOperationResult.Uncertain:
                    throw new InvalidOperationException("source patch outcome is uncertain; entering Recovery");
                default: // RejectedInvalidState — coordinator's own state moved off OnReady concurrently
                    throw new InvalidOperationException("source patch coordinator is not ready");
            }
        }

        private static string EnableOn()
        {
            var current = SourcePatchHost.CurrentState;
            if (current == SourcePatchState.OnReady) return "mutation_mode:true"; // idempotent, zero effect

            if (current != SourcePatchState.Off)
                throw new InvalidOperationException(
                    current == SourcePatchState.Unavailable
                        ? "source patch provider absent — cannot enable"
                        : $"state={current}: cannot enable source patch now");

            if (!SourcePatchProviderSlot.TryGet(out var provider))
                throw new InvalidOperationException("source patch provider absent — cannot enable");

            if (!SourcePatchStateMachine.IsLegalTransition(current, SourcePatchState.OnReady))
                throw new InvalidOperationException($"state={current}: illegal transition to OnReady");

            SourcePatchHost.Coordinator = new SourcePatchCoordinator(
                new UnitySourcePatchBytesPort(),
                provider,
                new UnityAutoRefreshLeasePort(),
                new SyncHelperCompileEvidencePort(),
                SourcePatchState.OnReady);
            SourcePatchHost.CurrentState = SourcePatchState.OnReady;
            return "mutation_mode:true";
        }

        private static string RequestDisable()
        {
            var current = SourcePatchHost.CurrentState;
            if (current == SourcePatchState.Off || current == SourcePatchState.Unavailable)
                return "mutation_mode:false"; // idempotent, no sync (no redispatch)
            if (current == SourcePatchState.Disabling)
                return "requested"; // already in flight — no redispatch, no second receipt/sync
            if (current != SourcePatchState.OnReady)
                throw new InvalidOperationException($"state={current}: cannot disable now");

            // Typed bounded provider stop: the coordinator never routes another
            // Apply once state leaves OnReady (WriteText's Busy/Disabling/Recovery
            // branches already throw pre-effect above this call) — no explicit
            // provider method is invoked. Detour reversal is the one Domain
            // Reload triggered below; in-process detour undo is an explicit
            // non-goal (§1.2). ISourcePatchProvider is immutable for
            // FINAL_FSR_ADAPTER_SHA — this policy never calls into it here.
            SourcePatchReceiptStore.Write(BuildReceipt());
            SourcePatchHost.CurrentState = SourcePatchState.Disabling;
            SourcePatchHost.Coordinator = null;
            SyncHelper.TriggerSync(false);
            return "requested";
        }

        private static SourcePatchDisableReceipt BuildReceipt() =>
            new SourcePatchDisableReceipt(
                Guid.NewGuid().ToString("N"),
                System.Diagnostics.Process.GetCurrentProcess().Id,
                CurrentProjectPath(),
                SyncHelper.CurrentEpoch + 1);

        internal static string CurrentProjectPath() =>
            Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
    }
}
