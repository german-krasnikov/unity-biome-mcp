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
    /// always falls back to the raw message verbatim — no info is lost when a
    /// bucket doesn't match.
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

        private static readonly string[] NetworkMarkers =
        {
            "could not resolve host",
            "unable to connect",
            "timed out",
            "ssl"
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
        /// returns <paramref name="rawMessage"/> verbatim — never paraphrase what wasn't classified.
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
                    return rawMessage;
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
