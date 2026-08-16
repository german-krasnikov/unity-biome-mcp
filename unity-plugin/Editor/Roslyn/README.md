# Roslyn compile preflight

This directory implements the Unity side of the public `compile_preflight`
tool. It parses and type-checks proposed C# source in memory before a caller
writes the file or triggers a Unity compilation.

## Components

- `RoslynLoader.cs` locates and lazily loads the Roslyn assemblies bundled with
  the current Unity installation. `CodeExecutor` uses the same loader.
- `RoslynWorkspace.cs` builds a reflection-based compilation with references to
  the currently loaded, allowed assemblies. Its reference cache is discarded
  on domain reload.
- `CompilePreflightCommand.cs` validates `file_path` and `new_content`, obtains
  diagnostics, and appends Unity-specific serialization hints.
- `RoslynFormat.cs` owns the compact text response.
- `UnityPreflightHints.cs` warns about unsupported serialized dictionaries,
  likely non-serializable field types, and removed serialized fields that lack
  `FormerlySerializedAs`.

Only `compile_preflight` is registered. Earlier plans mentioned
`find_references` and `semantic_at`; neither is a public tool or Unity command.
Object-reference traversal is provided separately by `find_references_to`.

## Contract

The Python wrapper sends a project-relative `file_path` and the complete
proposed `new_content`. No source file is written. The response is one of:

- `OK preflight (<milliseconds>ms)`, optionally followed by `WARN:` lines;
- `ERR preflight`, followed by compiler diagnostics;
- `[ROSLYN UNAVAILABLE: <reason>]` when the bundled assemblies cannot be used;
- `err: ...` for missing required input.

The check reflects the assemblies loaded by the open Editor and is a fast
preflight, not a substitute for Unity's full compilation and domain reload.

## Tests

Unity coverage lives in
`unity-plugin/Editor/Tests/Roslyn/CompilePreflightTests.cs`. Python wrapper and
format fixtures live under `server/tests/`. Run the focused EditMode fixture
through the repository's durable test runner, then run the relevant Python
tests when the cross-language contract changes.
