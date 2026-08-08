using System;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityMCP.Editor.Testing
{
    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method,
        AllowMultiple = false, Inherited = true)]
    [Category(TestCategories.RequiresGraphics)]
    public sealed class RequiresGraphicsDeviceAttribute : NUnitAttribute, IApplyToTest
    {
        public void ApplyToTest(Test test)
        {
            if (test.RunState == RunState.NotRunnable) return;
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null) return;
            test.RunState = RunState.Ignored;
            test.Properties.Set("_SKIPREASON", "Requires graphics device (skipped in headless mode)");
        }
    }
}
