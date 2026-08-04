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
                StaleCacheException       => "STALE_CACHE",  // must precede InvalidOperationException
                ArgumentNullException     => "VALIDATION",
                ArgumentException         => "VALIDATION",
                KeyNotFoundException      => "NOT_FOUND",
                FileNotFoundException     => "NOT_FOUND",
                IOException               => "INTERNAL",
                InvalidOperationException => "STATE",
                TimeoutException          => "TIMEOUT",
                MissingReferenceException => "NULL_REF",
                NullReferenceException    => "NULL_REF",
                _                         => "INTERNAL"
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
}
