using System.Collections.Generic;

namespace UnityMCP.Editor
{
    // C03 — executes MCP DSL steps through the real CommandRouter.ProcessAsync path via a
    // polled Phase.WaitingMcp, mirroring Phase.Moving's async-TCS pattern exactly.
    internal static partial class PlaytestRunner
    {
        /// <summary>Builds the synthetic command envelope for one MCP step, using the one
        /// execution run_id + step index as the request id.</summary>
        internal static string BuildMcpEnvelope(string runId, int stepIdx, string cmd, string argsJson)
        {
            var id = $"pt-{runId}-{stepIdx}";
            var args = string.IsNullOrEmpty(argsJson) ? "{}" : argsJson;
            return "{\"id\":\"" + JsonHelper.EscapeJson(id) + "\",\"cmd\":\"" +
                   JsonHelper.EscapeJson(cmd ?? "") + "\",\"args\":" + args + "}";
        }

        /// <summary>
        /// Applies a completed CommandRouter response to the step ledger. `data` is always a
        /// JSON string in this codebase's envelope (JsonHelper.FormatResponse), so
        /// ExtractString already returns the handler's exact returned text (which may itself
        /// be a plain scalar, free text, or a compact JSON blob) — no separate value-type
        /// projection is needed. On success, `data` becomes the step message and, when
        /// ResultVar is set, the value a later ASSERT $name can read back.
        /// </summary>
        internal static void ApplyMcpResult(PlaytestStep step, int stepIdx, string responseJson,
            List<string> results, PlaytestVarRegistry varRegistry, ref int passed, ref int failed)
        {
            var label = $"[{stepIdx + 1}]";
            var ok = JsonHelper.ExtractString(responseJson, "ok") == "true";
            if (ok)
            {
                var data = JsonHelper.ExtractString(responseJson, "data") ?? "";
                if (!string.IsNullOrEmpty(step.ResultVar))
                    varRegistry?.SetCaptured(step.ResultVar, data);
                var into = !string.IsNullOrEmpty(step.ResultVar) ? $" INTO ${step.ResultVar}" : "";
                results.Add($"{label} MCP {step.Method}{into} — PASS ({data})");
                passed++;
            }
            else
            {
                var err = JsonHelper.ExtractString(responseJson, "err") ?? "unknown error";
                results.Add($"{label} MCP {step.Method} — FAIL: {err}");
                failed++;
            }
        }
    }
}
