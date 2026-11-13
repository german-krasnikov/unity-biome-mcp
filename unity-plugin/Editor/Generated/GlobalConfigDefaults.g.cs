// Generated from dsl/data/globalconfig.data-contract, dsl/data/globalconfig.core-data
// source-sha256=f56445d7d4e87e42366dada229f1b9ef14e56447235afad2893ab951740a6350
// Edit the source, then run biome generate.

#nullable enable
namespace UnityMCP.Editor
{
    public sealed class GlobalConfigDefaults
    {
        public readonly int IdleTimeoutMin;
        public readonly bool IdleAutoSuspend;
        public readonly bool BridgeTerminateOrphan;
        public readonly int BridgeOrphanGraceMin;
        public GlobalConfigDefaults(
            int idleTimeoutMin,
            bool idleAutoSuspend,
            bool bridgeTerminateOrphan,
            int bridgeOrphanGraceMin)
        {
            this.IdleTimeoutMin = idleTimeoutMin;
            this.IdleAutoSuspend = idleAutoSuspend;
            this.BridgeTerminateOrphan = bridgeTerminateOrphan;
            this.BridgeOrphanGraceMin = bridgeOrphanGraceMin;
        }
    }

    public static class GlobalConfigDefaultsRows
    {
        public static readonly GlobalConfigDefaults[] All =
        {
            new GlobalConfigDefaults(
                idleTimeoutMin: 30,
                idleAutoSuspend: true,
                bridgeTerminateOrphan: true,
                bridgeOrphanGraceMin: 2
            ),
        };
    }
}
