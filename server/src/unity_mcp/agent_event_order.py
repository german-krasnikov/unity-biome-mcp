# Generated from dsl/events.biome
# source-sha256=7bbcda5cee8f82a31af5ab53ea89e690fff3cc4819c3b1a0421cef4523ed6227
# Edit the source, then run biome generate.

ORDER_CLASS: dict[str, str] = {
    "turn_started": "opens",
    "turn_completed": "closes",
    "assistant_delta": "opens",
    "thought_delta": "opens",
    "tool_call_started": "opens",
    "cost_update": "usage",
}
