// Generated from dsl/data/llmfeatures.data-contract, dsl/data/llmfeatures.core-data
// source-sha256=58728bb519f03df4b84b1d475229d02cb99127b14f292291cd61c6e9b5b094d3
// Edit the source, then run biome generate.

#nullable enable
namespace UnityMCP.Editor
{
    public sealed class LlmFeature
    {
        public readonly string Feature;
        public readonly string Model;
        public readonly int MaxTurns;
        public readonly int TimeoutS;
        public readonly int MaxTokens;
        public readonly string Backend;
        public LlmFeature(string feature, string model, int maxTurns, int timeoutS, int maxTokens, string backend)
        {
            this.Feature = feature;
            this.Model = model;
            this.MaxTurns = maxTurns;
            this.TimeoutS = timeoutS;
            this.MaxTokens = maxTokens;
            this.Backend = backend;
        }
    }

    public static class LlmFeatureRows
    {
        public static readonly LlmFeature[] All =
        {
            new LlmFeature(
                feature: "visual_verify",
                model: "haiku",
                maxTurns: 2,
                timeoutS: 15,
                maxTokens: 0,
                backend: "claude"
            ),
            new LlmFeature(
                feature: "screenshot_describe",
                model: "haiku",
                maxTurns: 2,
                timeoutS: 20,
                maxTokens: 0,
                backend: "claude"
            ),
            new LlmFeature(
                feature: "visual_diff",
                model: "haiku",
                maxTurns: 2,
                timeoutS: 25,
                maxTokens: 0,
                backend: "claude"
            ),
            new LlmFeature(
                feature: "summarize",
                model: "haiku",
                maxTurns: 1,
                timeoutS: 15,
                maxTokens: 0,
                backend: "claude"
            ),
            new LlmFeature(
                feature: "do_intent",
                model: "haiku",
                maxTurns: 1,
                timeoutS: 15,
                maxTokens: 0,
                backend: "claude"
            ),
            new LlmFeature(
                feature: "distiller",
                model: "haiku",
                maxTurns: 1,
                timeoutS: 15,
                maxTokens: 500,
                backend: "claude"
            ),
            new LlmFeature(
                feature: "ui_intent",
                model: "haiku",
                maxTurns: 1,
                timeoutS: 15,
                maxTokens: 0,
                backend: "claude"
            ),
            new LlmFeature(
                feature: "vfx_intent",
                model: "haiku",
                maxTurns: 1,
                timeoutS: 15,
                maxTokens: 0,
                backend: "claude"
            ),
            new LlmFeature(
                feature: "animator_intent",
                model: "haiku",
                maxTurns: 1,
                timeoutS: 15,
                maxTokens: 0,
                backend: "claude"
            ),
            new LlmFeature(
                feature: "nl_composer",
                model: "haiku",
                maxTurns: 1,
                timeoutS: 10,
                maxTokens: 512,
                backend: "claude"
            ),
        };
    }
}
