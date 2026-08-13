// Model preset data types and defaults — extracted from BackendConfig.cs.
using System;
using System.Collections.Generic;

namespace UnityMCP.Editor.Chat
{
    [Serializable]
    internal sealed class ModelPresetEntry
    {
        public string label         = "";
        public string modelId       = "";
        public int    contextWindow = 0;  // 0 = use pattern-matching fallback
    }

    [Serializable]
    internal sealed class ModelPresetsConfig
    {
        public ModelPresetEntry[] Claude      = new ModelPresetEntry[0];
        public ModelPresetEntry[] Codex       = new ModelPresetEntry[0];
        public ModelPresetEntry[] Antigravity = new ModelPresetEntry[0];
        public ModelPresetEntry[] Kimi        = new ModelPresetEntry[0];
        public ModelPresetEntry[] OpenCode    = new ModelPresetEntry[0];

        internal ModelPresetEntry[] For(BackendKind kind)
        {
            switch (kind)
            {
                case BackendKind.Claude:      return Claude;
                case BackendKind.Codex:       return Codex;
                case BackendKind.Antigravity: return Antigravity;
                case BackendKind.Kimi:        return Kimi;
                case BackendKind.OpenCode:    return OpenCode;
                default: return new ModelPresetEntry[0];
            }
        }

        internal void Set(BackendKind kind, ModelPresetEntry[] entries)
        {
            switch (kind)
            {
                case BackendKind.Claude:      Claude      = entries; break;
                case BackendKind.Codex:       Codex       = entries; break;
                case BackendKind.Antigravity: Antigravity = entries; break;
                case BackendKind.Kimi:        Kimi        = entries; break;
                case BackendKind.OpenCode:    OpenCode    = entries; break;
            }
        }
    }

    internal static class ModelPresetDefaults
    {
        internal const string CustomSentinel = "__custom__";

        internal static readonly Dictionary<BackendKind, (string label, string modelId, int contextWindow)[]> All
            = new Dictionary<BackendKind, (string, string, int)[]>
        {
            [BackendKind.Claude] = new[]
            {
                ("Default",    "",                    0),
                ("Fable 5",    "claude-fable-5",      1_000_000),
                ("Opus 5",     "claude-opus-5",       1_000_000),
                ("Opus 4.8",   "claude-opus-4-8",     1_000_000),
                ("Opus 4.7",   "claude-opus-4-7",     1_000_000),
                ("Opus 4.6",   "claude-opus-4-6",     1_000_000),
                ("Sonnet 5",   "claude-sonnet-5",     1_000_000),
                ("Sonnet 4.6", "claude-sonnet-4-6",   1_000_000),
                ("Haiku 4.5",  "claude-haiku-4-5",    200_000),
                ("Custom...",  CustomSentinel,         0),
            },
            [BackendKind.Codex] = new[]
            {
                ("Default",      "",              0),
                ("GPT-5.6 Sol",  "gpt-5.6-sol",  1_050_000),
                ("GPT-5.5",      "gpt-5.5",       1_050_000),
                ("GPT-5.4",      "gpt-5.4",       1_050_000),
                ("GPT-5.4 Mini", "gpt-5.4-mini",  1_050_000),
                ("GPT-5",        "gpt-5",          400_000),
                ("o3-pro",       "o3-pro",         200_000),
                ("o3",           "o3",             200_000),
                ("o4-mini",      "o4-mini",        200_000),
                ("GPT-4.1",      "gpt-4.1",       1_000_000),
                ("GPT-4.1 Mini", "gpt-4.1-mini",  1_000_000),
                ("GPT-4o",       "gpt-4o",         128_000),
                ("Custom...",    CustomSentinel,    0),
            },
            [BackendKind.Antigravity] = new[]
            {
                ("Default",   "", 0),
                ("Custom...", CustomSentinel, 0),
            },
            [BackendKind.Kimi] = new[]
            {
                ("Default",   "",               0),
                ("K2.7 Code", "kimi-for-coding", 0),
                ("K2.6",      "k2p6",            0),
                ("K2.5",      "k2p5",            0),
                ("Custom...", CustomSentinel,    0),
            },
            [BackendKind.OpenCode] = new[]
            {
                ("Default",                  "",                                    0),
                ("Anthropic: Sonnet 4",      "anthropic/claude-sonnet-4-20250514",  0),
                ("Anthropic: Haiku 3.5",     "anthropic/claude-haiku-3-5-latest",   0),
                ("OpenAI: GPT-4o",           "openai/gpt-4o",                       0),
                ("OpenAI: o3-mini",          "openai/o3-mini",                      0),
                ("Google: Gemini 2.5 Flash", "google/gemini-2.5-flash",             0),
                ("Google: Gemini 2.5 Pro",   "google/gemini-2.5-pro",               0),
                ("xAI: Grok 3",              "xai/grok-3",                          0),
                ("Ollama: Llama 3",          "ollama/llama3",                        0),
                ("Custom...",                CustomSentinel,                         0),
            },
        };

        internal static (string label, string modelId, int contextWindow)[] For(BackendKind kind)
            => All.TryGetValue(kind, out var p) ? p : new[] { ("Default", "", 0), ("Custom...", CustomSentinel, 0) };

        internal static (string label, string modelId)[] ForDropdown(BackendKind kind)
        {
            var full   = For(kind);
            var result = new (string, string)[full.Length];
            for (int i = 0; i < full.Length; i++)
                result[i] = (full[i].label, full[i].modelId);
            return result;
        }

        internal static Dictionary<BackendKind, (string label, string modelId)[]> AllDropdown
        {
            get
            {
                var d = new Dictionary<BackendKind, (string, string)[]>(All.Count);
                foreach (var kv in All)
                    d[kv.Key] = ForDropdown(kv.Key);
                return d;
            }
        }
    }
}
