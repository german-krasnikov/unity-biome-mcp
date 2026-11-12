namespace UnityMCP.Playtest.Core
{
    // PT-6: the one private PlaytestParser member StepBuilder needs — SetPosition parses "to" into
    // Position/RawPosition (literal vs @-expression). Exposed internal, not public: only this test
    // lane (same assembly, no separate Core reference) calls it. Adapted from
    // Plans/DSL/EVAL3_PROTOTYPES/playtest/proto_builder/Proxy.cs.
    public static partial class PlaytestParser
    {
        internal static void SetPositionProxy(PlaytestStep s, string token) => SetPosition(s, token);
    }
}
