using System;

namespace UnityMCP.Editor
{
    // Groups the rarely-varying trailing params of CommandRegistry.Register/RegisterAction/
    // RegisterAsync (M6, ROI reliability sprint). Plain mutable struct with public fields —
    // mirrors CommandRegistry.Entry's own style, no readonly/init-only (keeps object-initializer
    // syntax working without introducing a C# 9 feature unused elsewhere in this codebase).
    //
    // B3 (review sprint v0.70): demoted to internal. The 3-arg Register(cmd, handler,
    // CommandOptions) overloads had zero production call sites outside CommandRegistry.cs
    // itself (grep-confirmed) — every one of the 90+ real registrations goes through the
    // legacy bool-params overloads, which build a CommandOptions internally and forward.
    // CommandOptions is now purely an internal plumbing detail between CommandRegistry's
    // own overloads, not a public API surface.
    internal struct CommandOptions
    {
        public bool Mutating;
        public bool Runtime;
        // CSV, same shape as the legacy `required`/`optional` string params (split via
        // CommandRegistry.Split before landing on Entry.Required/Optional).
        public string Required;
        public string Optional;
        // Only meaningful for Register(); ignored by RegisterAction/RegisterAsync.
        public bool SpecialDispatch;
        // File handler for commands that return file paths instead of text
        // (e.g. screenshot). Set via Register(..., fileHandler: ...).
        // When non-null, Process() calls it instead of ExecuteCommand().
        public Func<string, string, string> FileHandler;  // (id, argsJson) → response
        public bool AlwaysAllowed;
        public bool AllowedDuringCompile;
        public string Description;
        public int MaxResponseChars;
    }
}
