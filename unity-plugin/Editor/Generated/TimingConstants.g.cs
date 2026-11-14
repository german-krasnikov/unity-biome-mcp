// Generated from dsl/data/timing.data-contract, dsl/data/timing.core-data
// source-sha256=653b5c70d84e13cd544b89453472abb9231214a1f3eb47da38800a6e245c5d15
// Edit the source, then run biome generate.

#nullable enable
namespace UnityMCP.Editor
{
    public sealed class Timing
    {
        public readonly double StartupGraceS;
        public readonly double DomainReloadExpiryS;
        public readonly double BackoffMinS;
        public readonly double BackoffMaxS;
        public readonly double ReloadBackoffS;
        public readonly double OrphanCheckIntervalS;
        public readonly double BusyTickS;
        public readonly double IdleTickS;
        public readonly double ProbeGraceS;
        public Timing(
            double startupGraceS,
            double domainReloadExpiryS,
            double backoffMinS,
            double backoffMaxS,
            double reloadBackoffS,
            double orphanCheckIntervalS,
            double busyTickS,
            double idleTickS,
            double probeGraceS)
        {
            this.StartupGraceS = startupGraceS;
            this.DomainReloadExpiryS = domainReloadExpiryS;
            this.BackoffMinS = backoffMinS;
            this.BackoffMaxS = backoffMaxS;
            this.ReloadBackoffS = reloadBackoffS;
            this.OrphanCheckIntervalS = orphanCheckIntervalS;
            this.BusyTickS = busyTickS;
            this.IdleTickS = idleTickS;
            this.ProbeGraceS = probeGraceS;
        }
    }

    public static class TimingRows
    {
        public static readonly Timing[] All =
        {
            new Timing(
                startupGraceS: 90.0,
                domainReloadExpiryS: 90.0,
                backoffMinS: 5.0,
                backoffMaxS: 60.0,
                reloadBackoffS: 1.0,
                orphanCheckIntervalS: 1.0,
                busyTickS: 5.0,
                idleTickS: 2.0,
                probeGraceS: 30.0
            ),
        };
    }
}
