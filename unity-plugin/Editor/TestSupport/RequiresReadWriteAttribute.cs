using System;
using NUnit.Framework;

namespace UnityMCP.Editor.Testing
{
    /// <summary>
    /// Marks a test that requires a read-write worker. UnityMcpTestBase calls
    /// Assert.Ignore automatically when IsReadOnly=true at SetUp time.
    /// Apply to the class for all mutating fixtures, or to individual methods.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method,
        AllowMultiple = false, Inherited = true)]
    public sealed class RequiresReadWriteAttribute : NUnitAttribute
    {
        public RequiresReadWriteAttribute(string reason)
        {
            if (string.IsNullOrEmpty(reason))
                throw new ArgumentException("Reason is required", nameof(reason));
            Reason = reason;
        }

        public string Reason { get; }
    }
}
