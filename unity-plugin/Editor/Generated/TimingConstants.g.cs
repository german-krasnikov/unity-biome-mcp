// Generated from dsl/data/timing.data-contract, dsl/data/timing.core-data
// source-sha256=d3a3d94fe5d0ddff17637b5e72c889662f2660f28a528afc234a8d131ec7794b
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
        public readonly double HardDeadlineS;
        public readonly double ReinstallTimeoutS;
        public readonly double UpdateCacheTtlS;
        public readonly double DefaultCommandTimeoutS;
        public readonly double PlayModeCacheTtlS;
        public readonly double LoginPathRetryTtlS;
        public readonly double FocusHintAfterS;
        public readonly double AliveCheckTtlS;
        public readonly double RetryDedupTtlS;
        public readonly double ReloadTier1PollS;
        public readonly double ReloadTier4PollS;
        public readonly double ReloadPollIntervalS;
        public readonly double ReloadTier2SleepS;
        public Timing(
            double startupGraceS,
            double domainReloadExpiryS,
            double backoffMinS,
            double backoffMaxS,
            double reloadBackoffS,
            double orphanCheckIntervalS,
            double busyTickS,
            double idleTickS,
            double probeGraceS,
            double hardDeadlineS,
            double reinstallTimeoutS,
            double updateCacheTtlS,
            double defaultCommandTimeoutS,
            double playModeCacheTtlS,
            double loginPathRetryTtlS,
            double focusHintAfterS,
            double aliveCheckTtlS,
            double retryDedupTtlS,
            double reloadTier1PollS,
            double reloadTier4PollS,
            double reloadPollIntervalS,
            double reloadTier2SleepS)
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
            this.HardDeadlineS = hardDeadlineS;
            this.ReinstallTimeoutS = reinstallTimeoutS;
            this.UpdateCacheTtlS = updateCacheTtlS;
            this.DefaultCommandTimeoutS = defaultCommandTimeoutS;
            this.PlayModeCacheTtlS = playModeCacheTtlS;
            this.LoginPathRetryTtlS = loginPathRetryTtlS;
            this.FocusHintAfterS = focusHintAfterS;
            this.AliveCheckTtlS = aliveCheckTtlS;
            this.RetryDedupTtlS = retryDedupTtlS;
            this.ReloadTier1PollS = reloadTier1PollS;
            this.ReloadTier4PollS = reloadTier4PollS;
            this.ReloadPollIntervalS = reloadPollIntervalS;
            this.ReloadTier2SleepS = reloadTier2SleepS;
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
                probeGraceS: 30.0,
                hardDeadlineS: 450.0,
                reinstallTimeoutS: 300.0,
                updateCacheTtlS: 86400.0,
                defaultCommandTimeoutS: 30.0,
                playModeCacheTtlS: 5.0,
                loginPathRetryTtlS: 30.0,
                focusHintAfterS: 15.0,
                aliveCheckTtlS: 30.0,
                retryDedupTtlS: 5.0,
                reloadTier1PollS: 40.0,
                reloadTier4PollS: 45.0,
                reloadPollIntervalS: 1.0,
                reloadTier2SleepS: 8.0
            ),
        };
    }
}
