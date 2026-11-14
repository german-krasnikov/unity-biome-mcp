using System.Runtime.CompilerServices;

// MainThreadDispatcher and AtomicFile stay `internal` (Phase 4 Task B extraction out
// of UnityMCP.Editor) -- internal visibility is per-assembly, not per-namespace, so
// every assembly whose source directly calls either type needs an explicit grant here,
// even though both keep their original `namespace UnityMCP.Editor`.
[assembly: InternalsVisibleTo("UnityMCP.Editor")]
[assembly: InternalsVisibleTo("UnityMCP.Editor.Tests")]
[assembly: InternalsVisibleTo("UnityMCP.Editor.TestSupport")]
[assembly: InternalsVisibleTo("UnityMCP.Editor.Chat.CLI")]
[assembly: InternalsVisibleTo("UnityMCP.Editor.Chat.Tests.CLI")]
[assembly: InternalsVisibleTo("UnityMCP.Editor.Wizard")]
// TestRunObserver.cs/TestRunService.cs/TestRunStore.cs call MainThreadDispatcher.Enqueue
// and AtomicFile.Swap directly (Phase 4 Task C: UnityMCP.Editor.TestRuns.asmdef).
[assembly: InternalsVisibleTo("UnityMCP.Editor.TestRuns")]
// JsonHelper.cs/JsonHelper.Response.cs/BiomeLabel.cs joined this asmdef alongside
// MainThreadDispatcher/AtomicFile (same zero-dependency, repo-wide-cross-cutting
// shape; TestRunProtocol.cs's need for the DSL-generated *Values enums forced the
// Editor<->TestRuns cycle open, and JsonHelper/BiomeLabel were blocking the same way
// from several other assemblies once TestRuns left UnityMCP.Editor). AskUserCard.cs
// calls JsonHelper.EscapeJson (internal) directly.
[assembly: InternalsVisibleTo("UnityMCP.Editor.Chat.View")]
// unity-test-project's consumer test assembly (ValueParserTests.cs, MaterialShaderTests.cs,
// GuardsTests.cs, CodeExecutorTests.cs, PluginIntegrationTests.cs, ...) calls JsonHelper's
// and BiomeLabel's internal members directly, same as the Roslyn asmdef's grant.
[assembly: InternalsVisibleTo("UnityMCP.TestProject")]
