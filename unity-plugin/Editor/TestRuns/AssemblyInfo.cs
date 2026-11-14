using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("UnityMCP.Editor")]
[assembly: InternalsVisibleTo("UnityMCP.Editor.TestSupport")]
[assembly: InternalsVisibleTo("UnityMCP.Editor.Tests.TestRuns")]
// TestIsolationBaseTests.cs calls UnityTestRunEnvironmentController.Prepare (internal) directly.
[assembly: InternalsVisibleTo("UnityMCP.Editor.Chat.Tests.View")]
// Temporary (Phase 4 Task C/D): the 15 TestRuns-related test files still
// physically live in Editor/Tests/ (base UnityMCP.Editor.Tests.asmdef) and
// reach internal TestRuns members directly. Task E moves them into their own
// UnityMCP.Editor.Tests.TestRuns asmdef, at which point this grant (and the
// matching "UnityMCP.Editor.TestRuns" reference in UnityMCP.Editor.Tests.asmdef)
// is removed -- base Tests ends up with zero remaining TestRuns references.
[assembly: InternalsVisibleTo("UnityMCP.Editor.Tests")]
