// Generated from dsl/data/toolverbs-external.data-contract, dsl/data/toolverbs-external.core-data
// source-sha256=d8a07b08541a092cc4a85c700649071b0f810a75a67fcce86b8d3b39b4585479
// Edit the source, then run biome generate.

#nullable enable
namespace UnityMCP.Editor.Chat
{
    public sealed class ExternalToolVerb
    {
        public readonly string Tool;
        public readonly string Verb;
        public ExternalToolVerb(string tool, string verb)
        {
            this.Tool = tool;
            this.Verb = verb;
        }
    }

    public static class ExternalToolVerbRows
    {
        public static readonly ExternalToolVerb[] All =
        {
            new ExternalToolVerb(tool: "Agent", verb: "Agent"),
            new ExternalToolVerb(tool: "Task", verb: "Agent"),
            new ExternalToolVerb(tool: "TodoWrite", verb: "Updated tasks"),
            new ExternalToolVerb(tool: "TaskCreate", verb: "Creating task"),
            new ExternalToolVerb(tool: "TaskUpdate", verb: "Updating task"),
            new ExternalToolVerb(tool: "Edit", verb: "Editing file"),
            new ExternalToolVerb(tool: "Write", verb: "Writing file"),
            new ExternalToolVerb(tool: "MultiEdit", verb: "Editing files"),
            new ExternalToolVerb(tool: "Bash", verb: "Running command"),
            new ExternalToolVerb(tool: "Read", verb: "Reading file"),
            new ExternalToolVerb(tool: "LS", verb: "Listing files"),
            new ExternalToolVerb(tool: "Glob", verb: "Searching files"),
            new ExternalToolVerb(tool: "Grep", verb: "Searching"),
            new ExternalToolVerb(tool: "WebFetch", verb: "Fetching"),
            new ExternalToolVerb(tool: "WebSearch", verb: "Searching web"),
        };
    }
}
