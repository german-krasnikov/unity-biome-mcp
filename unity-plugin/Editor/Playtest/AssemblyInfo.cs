using System.Runtime.CompilerServices;

// PlaytestRunner.Run/CreateRunId, PlaytestLinter, PlaytestRunState, PlaytestReceiptStore,
// PlaytestAliasHelpers, PlaytestMcpPolicy — all internal, consumed by the monolith's
// CommandRouter/AliasExpander/RuntimeHelper (Phase 5.3 extraction).
[assembly: InternalsVisibleTo("UnityMCP.Editor")]
// Test access to PlaytestRunner.Dispatcher, internal helpers, etc.
[assembly: InternalsVisibleTo("UnityMCP.Editor.Tests")]
// PlaytestCorpusEditModeTests + PlaytestIntegrationTests call PlaytestRunner.ReadValue,
// ExecuteSyncStep, Run (the last is public, the first two are internal).
[assembly: InternalsVisibleTo("UnityMCP.TestProject")]
// PlaytestCorpusPlayModeTests calls PlaytestRunner.Run (public — no IVT needed but kept
// symmetric with the Editor counterpart so both compile regardless of access changes).
[assembly: InternalsVisibleTo("UnityMCP.TestProject.PlayMode")]
// PlaytestAliasButton (Chat.CLI) calls PlaytestAliasWindow.Open() which is internal.
[assembly: InternalsVisibleTo("UnityMCP.Editor.Chat.CLI")]
