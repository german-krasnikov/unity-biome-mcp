using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("UnityMCP.Editor.Chat.CLI")]
[assembly: InternalsVisibleTo("UnityMCP.Editor.Chat.View")]
[assembly: InternalsVisibleTo("UnityMCP.Editor.Tests")]
[assembly: InternalsVisibleTo("UnityMCP.Editor.Tests.Roslyn")]
[assembly: InternalsVisibleTo("UnityMCP.Editor.TestSupport")]
[assembly: InternalsVisibleTo("UnityMCP.TestProject")]
// B22: PlayMode carrier for the 3 Play-bound MCPFeedbackFixture .playtest files
[assembly: InternalsVisibleTo("UnityMCP.TestProject.PlayMode")]
// grants Chat.Tests access to JsonHelper parse helpers
[assembly: InternalsVisibleTo("UnityMCP.Editor.Chat.Tests")]
[assembly: InternalsVisibleTo("UnityMCP.Editor.Chat.Tests.CLI")]
[assembly: InternalsVisibleTo("UnityMCP.Editor.Chat.Tests.View")]
[assembly: InternalsVisibleTo("UnityMCP.Editor.Wizard")]
[assembly: InternalsVisibleTo("UnityMCP.Editor.Wizard.Tests")]
[assembly: InternalsVisibleTo("UnityMCP.Worker.DomainReloadHarness")]
// Phase 4 Task E: DomainReloadOrderTests.cs/TestRunnerTests.cs (now in
// UnityMCP.Editor.Tests.TestRuns.asmdef) call CommandRegistry.Clear/InitDefaults,
// CommandRouter.IsAllowedDuringCompile and TestRunner.FinishRun directly -- all
// internal, all still in root UnityMCP.Editor (TestRunner.cs is the documented
// MCP facade that stays in Editor/ root per the architecture plan).
[assembly: InternalsVisibleTo("UnityMCP.Editor.Tests.TestRuns")]
