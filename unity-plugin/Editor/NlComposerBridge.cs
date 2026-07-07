// NlComposerBridge — spawns CLI subprocess (claude/codex/etc.) to convert NL → DSL.
// Zero Unity Editor API usage in hot path → ThreadPool-safe.
using System;
using System.Threading.Tasks;
using UnityEditor;

namespace UnityMCP.Editor
{
    internal static class NlComposerBridge
    {
        // Test seams ─────────────────────────────────────────────────────────
#if UNITY_INCLUDE_TESTS
        internal static Func<string, string[], int, Task<string>> RunProcessOverride;
        internal static Func<SamplingConfig, string>              ResolveBinaryOverride;
#endif

        internal const string SystemPrompt =
            "You convert natural-language game test descriptions into Unity Playtest DSL.\n" +
            "Rules:\n" +
            "- Output ONLY valid DSL lines, one per line. No explanations, no markdown, no comments.\n" +
            "- [/path] in input = Unity scene object path. Strip brackets in output: [/Player] → /Player\n" +
            "- Query format for components: /path|Component|field (pipe-separated)\n" +
            "- Multi-language input OK (Russian, English, any). Always output DSL keywords in English.\n" +
            "- If intent is unclear → LOG # UNPARSED: <original text>\n" +
            "- One input can produce multiple DSL lines.\n" +
            "\n" +
            "=== DSL COMMANDS (full reference) ===\n" +
            "MOVE /path TO x,y,z             — move object to position over time\n" +
            "MOVE_PATH x1,y1,z1 > x2,y2,z2   — move along waypoints\n" +
            "TELEPORT /path x,y,z            — instant reposition\n" +
            "WAIT seconds                     — pause execution\n" +
            "WAIT_UNTIL query op value TIMEOUT s [ABORT] — wait for condition (AND/OR chains ok)\n" +
            "ASSERT query op value [AS \"msg\"] — check condition, fail test if false\n" +
            "ASSERT_CONSOLE_CLEAN [IGNORE \"pat\"] — no errors/warnings in console\n" +
            "ASSERT_NEAR /pathA /pathB dist   — objects within distance\n" +
            "INVOKE /path Component Method [args] — call method on component\n" +
            "SET /path Component field value  — set component field\n" +
            "CLICK /path [WAIT s]             — click UI element\n" +
            "LOG message                      — print to test log\n" +
            "SECTION \"label\"                  — group steps under label\n" +
            "TIMESCALE value                  — set Time.timeScale\n" +
            "MONITOR query                    — track value changes\n" +
            "CAPTURE label query              — snapshot value for later comparison\n" +
            "ASSERT_CAPTURED label MODE       — check captured value (INCREASED/DECREASED/UNCHANGED)\n" +
            "INVARIANT query op value         — condition must hold for entire test\n" +
            "SCREENSHOT                       — capture screenshot\n" +
            "ABORT_ON_FAIL true/false         — stop test on first failure\n" +
            "op: == != > < >= <=\n" +
            "\n" +
            "=== EXAMPLES ===\n" +
            "IN: move [/Player] to 0,0,0 then wait 2s\n" +
            "OUT:\nMOVE /Player TO 0,0,0\nWAIT 2\n\n" +
            "IN: перемести [/Player] в точку [/Enemy]\n" +
            "OUT:\nLOG # UNPARSED: need target coordinates, not object reference\n\n" +
            "IN: телепортируй [/Player] в 5,0,3\n" +
            "OUT:\nTELEPORT /Player 5,0,3\n\n" +
            "IN: подожди пока [/Player] здоровье (Health hp) станет меньше 10, таймаут 15 секунд\n" +
            "OUT:\nWAIT_UNTIL /Player|Health|hp < 10 TIMEOUT 15\n\n" +
            "IN: проверь что [/Enemy] мёртв (Health isDead == true)\n" +
            "OUT:\nASSERT /Enemy|Health|isDead == true\n\n" +
            "IN: вызови OnClick на [/UI/Button]\n" +
            "OUT:\nINVOKE /UI/Button Button OnClick\n\n" +
            "IN: нажми на [/Canvas/StartButton] и подожди 1 секунду\n" +
            "OUT:\nCLICK /Canvas/StartButton\nWAIT 1\n\n" +
            "IN: установи скорость [/Car] (Rigidbody velocity) в 0,0,0\n" +
            "OUT:\nSET /Car Rigidbody velocity 0,0,0\n\n" +
            "IN: assert console clean then screenshot\n" +
            "OUT:\nASSERT_CONSOLE_CLEAN\nSCREENSHOT\n\n" +
            "IN: проверь что [/A] и [/B] рядом (расстояние < 2)\n" +
            "OUT:\nASSERT_NEAR /A /B 2\n\n" +
            "IN: запомни позицию [/Coin] и потом проверь что изменилась\n" +
            "OUT:\nCAPTURE coin_pos /Coin|Transform|position\nWAIT 2\nASSERT_CAPTURED coin_pos CHANGED";

        // Returns DSL string or null on any failure / empty result.
        internal static async Task<string> ParseAsync(string nlText, SamplingConfig cfg)
        {
            var binary = ResolveBinary(cfg);
            if (string.IsNullOrEmpty(binary)) return null;

            var prompt = BuildPrompt(nlText);
            var args   = BuildArgs(binary, prompt, cfg);
            var fn     = GetRunner();
            var result = await fn(binary, args, (int)(cfg.Timeout * 1000));
            return string.IsNullOrWhiteSpace(result) ? null : StripMarkdown(result.Trim());
        }

        internal static string BuildPrompt(string nlText) =>
            SystemPrompt + "\n\nIN: " + nlText + "\nOUT:";

        internal static string StripMarkdown(string s)
        {
            if (s == null) return null;
            var lines = s.Split('\n');
            var clean = new System.Collections.Generic.List<string>();
            foreach (var line in lines)
            {
                var t = line.Trim();
                if (t.StartsWith("```")) continue;
                if (!string.IsNullOrWhiteSpace(t)) clean.Add(t);
            }
            return clean.Count > 0 ? string.Join("\n", clean) : null;
        }

        internal const string DefaultModel    = "haiku";
        internal const string ClaudePrintFlag = "--print";
        internal const string CodexPromptFlag = "--prompt";
        internal const string ModelFlag       = "--model";

        internal static string[] BuildArgs(string binary, string prompt, SamplingConfig cfg)
        {
            var model = string.IsNullOrEmpty(cfg.Model) ? DefaultModel : cfg.Model;
            var q     = ShellHelper.ShellQuoteSingle(prompt);
            return binary.Contains("claude")
                ? new[] { ClaudePrintFlag, q, ModelFlag, model }
                : new[] { CodexPromptFlag, q };
        }

        internal static string ResolveBinary(SamplingConfig cfg)
        {
            var backend = string.IsNullOrEmpty(cfg?.Backend) ? "claude" : cfg.Backend;
#if UNITY_INCLUDE_TESTS
            if (ResolveBinaryOverride != null) return ResolveBinaryOverride(cfg);
#endif
            var pref = EditorPrefs.GetString(ShellHelper.EditorPrefsKeyPrefix + backend, "");
            return !string.IsNullOrEmpty(pref) ? pref : backend;
        }

        private static Func<string, string[], int, Task<string>> GetRunner()
        {
#if UNITY_INCLUDE_TESTS
            if (RunProcessOverride != null) return RunProcessOverride;
#endif
            return RunProcessDefault;
        }

        private static Task<string> RunProcessDefault(string binary, string[] args, int timeoutMs)
        {
            var cmd = binary + " " + string.Join(" ", args);
            return ShellHelper.RunViaLoginShellAsync(cmd, timeoutMs);
        }
    }
}
