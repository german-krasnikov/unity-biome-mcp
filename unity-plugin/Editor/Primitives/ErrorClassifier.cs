using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Maps exception types to structured error categories so Python/LLM
    /// can distinguish validation errors from null-refs, timeouts, etc.
    /// </summary>
    internal static class ErrorClassifier
    {
        internal static string Classify(Exception e)
        {
            if (e is System.Reflection.TargetInvocationException tie && tie.InnerException != null)
                e = tie.InnerException;
            return e switch
            {
                StaleCacheException          => "STALE_CACHE",  // must precede InvalidOperationException
                ProviderUnavailableException => "UNAVAILABLE",  // must precede InvalidOperationException
                ArgumentNullException        => "VALIDATION",
                ArgumentException            => "VALIDATION",
                KeyNotFoundException         => "NOT_FOUND",
                FileNotFoundException        => "NOT_FOUND",
                IOException                  => "INTERNAL",
                InvalidOperationException    => "STATE",
                TimeoutException             => "TIMEOUT",
                MissingReferenceException    => "NULL_REF",
                NullReferenceException       => "NULL_REF",
                _                            => "INTERNAL"
            };
        }

        internal static string FormatError(Exception e)
        {
            if (e is System.Reflection.TargetInvocationException tie && tie.InnerException != null)
                e = tie.InnerException;
            return $"{Classify(e)}: {e.Message}";
        }
    }

    /// <summary>
    /// Thrown when a path or ref resolved successfully before but is no longer valid —
    /// caller should call get_hierarchy to refresh and retry.
    /// </summary>
    internal class StaleCacheException : InvalidOperationException
    {
        public StaleCacheException(string message) : base(message) { }
    }

    /// <summary>
    /// Thrown when a capability's sole implementing provider (e.g. Chat.CLI's
    /// SearchContextProvider) was never registered — an expected condition, not an internal
    /// error, so it must log a warning (not an error) and never be conflated with a generic
    /// InvalidOperationException (PR-05 05.3, Finding 3).
    /// </summary>
    internal sealed class ProviderUnavailableException : InvalidOperationException
    {
        public ProviderUnavailableException(string message) : base(message) { }
    }
}
