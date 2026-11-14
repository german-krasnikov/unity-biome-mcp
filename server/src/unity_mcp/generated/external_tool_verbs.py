# Generated from dsl/data/toolverbs-external.data-contract
# source-sha256=d8a07b08541a092cc4a85c700649071b0f810a75a67fcce86b8d3b39b4585479
# Edit the source, then run biome generate.

from dataclasses import dataclass


@dataclass(frozen=True, slots=True, kw_only=True)
class ExternalToolVerb:
    tool: str
    verb: str


EXTERNAL_TOOL_VERB_ROWS: tuple[ExternalToolVerb, ...] = (
    ExternalToolVerb(tool="Agent", verb="Agent"),
    ExternalToolVerb(tool="Task", verb="Agent"),
    ExternalToolVerb(tool="TodoWrite", verb="Updated tasks"),
    ExternalToolVerb(tool="TaskCreate", verb="Creating task"),
    ExternalToolVerb(tool="TaskUpdate", verb="Updating task"),
    ExternalToolVerb(tool="Edit", verb="Editing file"),
    ExternalToolVerb(tool="Write", verb="Writing file"),
    ExternalToolVerb(tool="MultiEdit", verb="Editing files"),
    ExternalToolVerb(tool="Bash", verb="Running command"),
    ExternalToolVerb(tool="Read", verb="Reading file"),
    ExternalToolVerb(tool="LS", verb="Listing files"),
    ExternalToolVerb(tool="Glob", verb="Searching files"),
    ExternalToolVerb(tool="Grep", verb="Searching"),
    ExternalToolVerb(tool="WebFetch", verb="Fetching"),
    ExternalToolVerb(tool="WebSearch", verb="Searching web"),
)
