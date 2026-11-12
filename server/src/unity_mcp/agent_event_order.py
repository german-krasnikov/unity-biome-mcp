# Generated from dsl/events.biome
# source-sha256=685c2d7e869ad4a31b6eb2dc7abab7af837e908ef756cf7864ce84a2f3682b61
# Edit the source, then run biome generate.

ORDER_CLASS: dict[str, str] = {
    "turn_started": "opens",
    "turn_completed": "closes",
    "assistant_delta": "opens",
    "thought_delta": "opens",
    "tool_call_started": "opens",
    "cost_update": "usage",
}
