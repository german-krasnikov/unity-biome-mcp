using System.Runtime.CompilerServices;

// Phase 4 Task E: TestRunAssemblyFingerprintTests.cs (now in
// UnityMCP.Editor.Tests.TestRuns.asmdef) calls the internal TempDirScope helper
// (Tests/TempDirScope.cs) directly.
[assembly: InternalsVisibleTo("UnityMCP.Editor.Tests.TestRuns")]
