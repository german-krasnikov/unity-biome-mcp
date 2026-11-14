# Generated from dsl/data/llmfeatures.data-contract, dsl/data/llmfeatures.core-data
# source-sha256=58728bb519f03df4b84b1d475229d02cb99127b14f292291cd61c6e9b5b094d3
# Edit the source, then run biome generate.

from dataclasses import dataclass


@dataclass(frozen=True, slots=True, kw_only=True)
class LlmFeature:
    feature: str
    model: str
    maxTurns: int
    timeoutS: int
    maxTokens: int
    backend: str


LLM_FEATURE_ROWS: tuple[LlmFeature, ...] = (
    LlmFeature(
        feature="visual_verify",
        model="haiku",
        maxTurns=2,
        timeoutS=15,
        maxTokens=0,
        backend="claude",
    ),
    LlmFeature(
        feature="screenshot_describe",
        model="haiku",
        maxTurns=2,
        timeoutS=20,
        maxTokens=0,
        backend="claude",
    ),
    LlmFeature(
        feature="visual_diff",
        model="haiku",
        maxTurns=2,
        timeoutS=25,
        maxTokens=0,
        backend="claude",
    ),
    LlmFeature(
        feature="summarize",
        model="haiku",
        maxTurns=1,
        timeoutS=15,
        maxTokens=0,
        backend="claude",
    ),
    LlmFeature(
        feature="do_intent",
        model="haiku",
        maxTurns=1,
        timeoutS=15,
        maxTokens=0,
        backend="claude",
    ),
    LlmFeature(
        feature="distiller",
        model="haiku",
        maxTurns=1,
        timeoutS=15,
        maxTokens=500,
        backend="claude",
    ),
    LlmFeature(
        feature="ui_intent",
        model="haiku",
        maxTurns=1,
        timeoutS=15,
        maxTokens=0,
        backend="claude",
    ),
    LlmFeature(
        feature="vfx_intent",
        model="haiku",
        maxTurns=1,
        timeoutS=15,
        maxTokens=0,
        backend="claude",
    ),
    LlmFeature(
        feature="animator_intent",
        model="haiku",
        maxTurns=1,
        timeoutS=15,
        maxTokens=0,
        backend="claude",
    ),
    LlmFeature(
        feature="nl_composer",
        model="haiku",
        maxTurns=1,
        timeoutS=10,
        maxTokens=512,
        backend="claude",
    ),
)
