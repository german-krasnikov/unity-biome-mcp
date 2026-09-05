using System.Runtime.CompilerServices;

// D13/D14: unity-test-project/Assets/Tests/Editor/Tests.asmdef (UnityMCP.TestProject)
// needs to call the Player executor's step-handling methods (EvaluateAssert,
// ExecuteSet, ExecuteInvoke) and read the private StepResult struct directly —
// there is no live-Player build available locally to exercise them any other
// way (same class of gap Core/AssemblyInfo.cs already documents for its own
// InternalsVisibleTo grants). Those members stay `internal`, not `public`:
// nothing outside this bridge needs them.
[assembly: InternalsVisibleTo("UnityMCP.TestProject")]
