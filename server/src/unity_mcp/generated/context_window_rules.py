# Generated from dsl/data/contextwindow.data-contract, dsl/data/contextwindow.core-data
# source-sha256=8f3432d3c670c531f5eaf77809bd829e4810e55dd46e21ee3926b14710ce5943
# Edit the source, then run biome generate.

from dataclasses import dataclass


@dataclass(frozen=True, slots=True, kw_only=True)
class ContextWindowRule:
    pattern: str
    mode: str
    window: int


CONTEXT_WINDOW_RULE_ROWS: tuple[ContextWindowRule, ...] = (
    ContextWindowRule(pattern="fable", mode="contains", window=1000000),
    ContextWindowRule(pattern="mythos", mode="contains", window=1000000),
    ContextWindowRule(pattern="opus-5", mode="contains", window=1000000),
    ContextWindowRule(pattern="sonnet-5", mode="contains", window=1000000),
    ContextWindowRule(pattern="opus-4-8", mode="contains", window=1000000),
    ContextWindowRule(pattern="opus-4-7", mode="contains", window=1000000),
    ContextWindowRule(pattern="opus-4-6", mode="contains", window=1000000),
    ContextWindowRule(pattern="sonnet-4-6", mode="contains", window=1000000),
    ContextWindowRule(pattern="opus", mode="contains", window=200000),
    ContextWindowRule(pattern="sonnet", mode="contains", window=200000),
    ContextWindowRule(pattern="haiku", mode="contains", window=200000),
    ContextWindowRule(pattern="gpt-5.4", mode="contains", window=1050000),
    ContextWindowRule(pattern="gpt-5.5", mode="contains", window=1050000),
    ContextWindowRule(pattern="gpt-5.6", mode="contains", window=1050000),
    ContextWindowRule(pattern="gpt-5", mode="contains", window=400000),
    ContextWindowRule(pattern="gpt-4.1", mode="contains", window=1000000),
    ContextWindowRule(pattern="gpt-4", mode="contains", window=128000),
    ContextWindowRule(pattern="o3", mode="starts", window=200000),
    ContextWindowRule(pattern="o4", mode="starts", window=200000),
    ContextWindowRule(pattern="gemini", mode="contains", window=1000000),
    ContextWindowRule(pattern="kimi", mode="contains", window=128000),
    ContextWindowRule(pattern="moonshot", mode="contains", window=128000),
    ContextWindowRule(pattern="codex", mode="contains", window=192000),
)
