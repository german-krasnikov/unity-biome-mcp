using System;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Pure classification of a raw UPM <c>Client.Add</c> failure message into an
    /// actionable reason (ARC-10 T2). No Unity API — testable without a real
    /// package resolve. Consumed by <c>UpmPluginUpdater</c> (ARC-10 T3) to turn
    /// "Unable to add package [...]" into text a user can act on.
    /// </summary>
    /// <remarks>
    /// Bucket substrings are best-effort guesses at real UPM/git wording, not a
    /// captured production message (ARC-10 §6 risk). <see cref="Reason.Unknown"/>
    /// never discards the raw message — it appends a generic actionable hint
    /// instead (ARC-10 T3 review minors a/c) so a bucket miss never leaves the
    /// user with zero guidance or ActionableText returning null.
    /// </remarks>
    internal static class UpmErrorClassifier
    {
        public enum Reason
        {
            GitRefMissing,
            UpmBusy,
            Network,
            Unknown
        }

        private static readonly string[] BusyMarkers =
        {
            "already being added",
            "pending list"
        };

        private static readonly string[] GitRefMarkers =
        {
            "couldn't find remote ref",
            "did not match any",
            "cannot find branch or tag"
        };

        // ARC-10 T3 review minor (b): a bare "ssl" substring false-positives on
        // unrelated text that merely contains the three letters in sequence (e.g.
        // "AddToClassList" in a stack trace: ...ClaSSListt -> "ssl"). Anchored to
        // the two-word phrases real curl/git SSL failures actually use.
        private static readonly string[] NetworkMarkers =
        {
            "could not resolve host",
            "unable to connect",
            "timed out",
            "ssl certificate",
            "ssl connect"
        };

        /// <summary>Buckets a raw UPM error message into a <see cref="Reason"/>.</summary>
        public static Reason Classify(string rawMessage)
        {
            if (string.IsNullOrEmpty(rawMessage))
                return Reason.Unknown;

            if (ContainsAny(rawMessage, BusyMarkers)) return Reason.UpmBusy;
            if (ContainsAny(rawMessage, GitRefMarkers)) return Reason.GitRefMissing;
            if (ContainsAny(rawMessage, NetworkMarkers)) return Reason.Network;
            return Reason.Unknown;
        }

        /// <summary>
        /// One user-facing sentence per <paramref name="reason"/>. <see cref="Reason.Unknown"/>
        /// (review minors a/c) never paraphrases what wasn't classified, but always appends a
        /// generic actionable hint and never returns null — this is the field-reported main
        /// case ("Unable to add package [url#vX]" classifies Unknown today, §1) so it must not
        /// leave the user with a bare, un-actionable UPM message.
        /// </summary>
        public static string ActionableText(Reason reason, string version, string rawMessage)
        {
            switch (reason)
            {
                case Reason.GitRefMissing:
                    return $"Version v{version} was not found in the plugin repository yet. " +
                           "It may not be tagged — wait a few minutes and try again.";
                case Reason.UpmBusy:
                    return "Another plugin update is already in progress. Wait for it to finish, then try again.";
                case Reason.Network:
                    return "Could not reach GitHub. Check your network connection and try again.";
                default:
                    var hint = $"Check that tag v{version} exists in the plugin repository, " +
                               "that no other UPM operation is in progress, and that your network connection is working.";
                    return string.IsNullOrEmpty(rawMessage) ? hint : $"{rawMessage} {hint}";
            }
        }

        private static bool ContainsAny(string haystack, string[] markers)
        {
            foreach (var marker in markers)
            {
                if (haystack.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }
    }
}
