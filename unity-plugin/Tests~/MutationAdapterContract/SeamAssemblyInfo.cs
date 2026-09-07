// Test-only assembly attribute for the Seam compile-contract assembly
// (unity-plugin/Tests~/MutationAdapterContract/). NOT part of production:
// this file lives in the Tests~ tree and is compiled only into
// UnityMCP.Editor.SourcePatch.Seam, never into the real
// UnityMCP.Editor.SourcePatch Unity assembly. It exists so
// AdapterContract.Tests.csproj (a separate assembly, referencing Seam via
// ProjectReference) can call the existing internal
// SourcePatchProviderSlot.ResetForTests() to isolate registration state
// between NUnit tests, without touching the production source file
// unity-plugin/Editor/SourcePatch/SourcePatchProviderSlot.cs.
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("AdapterContract.Tests")]
