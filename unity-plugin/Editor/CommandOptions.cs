namespace UnityMCP.Editor
{
    // Groups the rarely-varying trailing params of CommandRegistry.Register/RegisterAction/
    // RegisterAsync (M6, ROI reliability sprint). Plain mutable struct with public fields —
    // mirrors CommandRegistry.Entry's own style, no readonly/init-only (keeps object-initializer
    // syntax working without introducing a C# 9 feature unused elsewhere in this codebase).
    //
    // The 3-arg Register(cmd, handler, CommandOptions) overloads are the new preferred entry
    // point for NEW registrations. The legacy bool-params overloads stay untouched for the
    // 60+ existing call sites and now just build a CommandOptions and forward.
    public struct CommandOptions
    {
        public bool Mutating;
        public bool Runtime;
        // CSV, same shape as the legacy `required`/`optional` string params (split via
        // CommandRegistry.Split before landing on Entry.Required/Optional).
        public string Required;
        public string Optional;
        // Only meaningful for Register(); ignored by RegisterAction/RegisterAsync.
        public bool SpecialDispatch;
        public bool AlwaysAllowed;
        public bool AllowedDuringCompile;
        public string Description;
        public int MaxResponseChars;
    }
}
