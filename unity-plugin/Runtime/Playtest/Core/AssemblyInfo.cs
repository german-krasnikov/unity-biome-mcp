using System.Runtime.CompilerServices;

// D06: PlaytestParser.cs/.Internals.cs/.Directives.cs/.Mcp.cs/.Subroutines.cs and
// PlaytestHeaderScanner.cs just relocated here from UnityMCP.Editor with their `internal`
// visibility unchanged (D06 is a pure move — publicizing the contract is D07's job). A dozen
// Editor-assembly consumers (PlaytestRunner*.cs, VisualStep.cs, PlaytestLinter.cs, etc.) still
// reference StepType/PlaytestStep/ParseResult/SourcedLine directly, and D07 keeps several
// helpers (ResolveQuery, ExpandIncludes, ExpandSigils, ...) `internal` permanently (YAGNI —
// nothing outside UnityMCP.Editor needs them public). This friend declaration is the standing
// bridge for that, matching the same pattern already used by SourcePatch/AssemblyInfo.cs.
[assembly: InternalsVisibleTo("UnityMCP.Editor")]

// UnityMCP.Editor.Tests (unity-plugin/Editor/Tests/) is its own separate asmdef, not
// auto-covered by the UnityMCP.Editor grant above (asmdef references are not transitive,
// same class of gap D01/D03's own evidence hit) — it references these `internal` Core types
// directly (e.g. PlaytestStep, StepType, IncludeResolver in PlaytestMcpPolicyTests.cs,
// PlaytestAliasTestHelpers.cs, PlaytestDslExporterTests.cs, PlaytestStepValidatorTests.cs).
[assembly: InternalsVisibleTo("UnityMCP.Editor.Tests")]

// unity-test-project/Assets/Tests/Editor/Runtime/*.cs (namespace UnityMCP.TestProject.Runtime)
// is a 5th consumer beyond the Editor assemblies above — same class of gap D04's own evidence
// found for CommandRouter.cs's pre-existing InternalsVisibleTo("UnityMCP.TestProject").
[assembly: InternalsVisibleTo("UnityMCP.TestProject")]
