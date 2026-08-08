using System;
using NUnit.Framework;
using UnityMCP.Editor.TestRuns;

namespace UnityMCP.Editor.Testing
{
    /// <summary>
    /// Marks a test that may run only when explicitly selected in a disposable
    /// worker. UnityMcpTestBase enforces the marker before fixture setup while
    /// NUnit ExplicitAttribute keeps the test out of ordinary discovery runs.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method,
        AllowMultiple = false, Inherited = true)]
    [Category(TestCategories.WorkerOnly)]
    public sealed class BiomeWorkerOnlyAttribute : ExplicitAttribute
    {
        public BiomeWorkerOnlyAttribute(string reason)
            : base(RequireReason(reason))
        {
            Reason = reason.Trim();
        }

        public string Reason { get; }

        private static string RequireReason(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException(
                    "A worker-only test requires a specific reason.", nameof(reason));
            return reason.Trim();
        }
    }

    /// <summary>
    /// Runtime boundary for destructive helpers outside a fixture's base SetUp.
    /// This assembly exists only when tests are included.
    /// </summary>
    public static class UnityMcpWorkerTestBoundary
    {
        public static void Require(string operation)
        {
            if (string.IsNullOrWhiteSpace(operation))
                throw new ArgumentException(
                    "A worker-only operation description is required.", nameof(operation));
            UnityTestRunEnvironmentController.RequireDisposableWorker(operation);
        }
    }
}
