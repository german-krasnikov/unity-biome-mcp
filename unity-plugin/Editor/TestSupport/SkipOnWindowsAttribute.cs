using System;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using UnityEngine;

namespace UnityMCP.Editor.Testing
{
    /// <summary>
    /// Skips the test on Windows. Use for tests with known platform-specific failures
    /// (path separators, shell differences, subprocess behavior) that need a proper fix later.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method,
        AllowMultiple = false, Inherited = true)]
    public sealed class SkipOnWindowsAttribute : NUnitAttribute, IApplyToTest
    {
        private readonly string _reason;

        public SkipOnWindowsAttribute(string reason = "Known Windows platform incompatibility — fix tracked separately")
        {
            _reason = reason;
        }

        public void ApplyToTest(Test test)
        {
            if (test.RunState == RunState.NotRunnable) return;
            if (Application.platform != RuntimePlatform.WindowsEditor) return;
            test.RunState = RunState.Ignored;
            test.Properties.Set("_SKIPREASON", _reason);
        }
    }
}
