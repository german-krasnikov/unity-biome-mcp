using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("UnityMCP.Editor")]
[assembly: InternalsVisibleTo("UnityMCP.Editor.TestSupport")]
[assembly: InternalsVisibleTo("UnityMCP.Editor.Tests.TestRuns")]
// TestIsolationBaseTests.cs calls UnityTestRunEnvironmentController.Prepare (internal) directly.
[assembly: InternalsVisibleTo("UnityMCP.Editor.Chat.Tests.View")]
