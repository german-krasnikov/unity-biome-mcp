// TDD: CommandValidator.AutoUsage — high-arity truncation and null-array safety.
using System;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class CommandValidatorAutoUsageTests : SceneTestBase
    {
        // ── AutoUsage: high-arity truncation ─────────────────────────────────

        [Test]
        public void AutoUsage_SixOptionalParams_ShowsFiveAndPlusOneMore()
        {
            var optional = new[] { "a", "b", "c", "d", "e", "f" };
            var result = CommandValidator.AutoUsage("cmd", Array.Empty<string>(), optional);
            StringAssert.Contains("[+1 more]", result);
            StringAssert.Contains("[a=...]", result);
            StringAssert.Contains("[e=...]", result);
            StringAssert.DoesNotContain("[f=...]", result);
        }

        [Test]
        public void AutoUsage_TenOptionalParams_ShowsFiveAndPlusFiveMore()
        {
            var optional = new[] { "a", "b", "c", "d", "e", "f", "g", "h", "i", "j" };
            var result = CommandValidator.AutoUsage("cmd", Array.Empty<string>(), optional);
            StringAssert.Contains("[+5 more]", result);
            // Only first 5 shown
            StringAssert.Contains("[a=...]", result);
            StringAssert.Contains("[e=...]", result);
            StringAssert.DoesNotContain("[f=...]", result);
        }

        // ── AutoUsage: null array safety ──────────────────────────────────────

        [Test]
        public void AutoUsage_NullRequiredArray_DoesNotThrow()
        {
            var result = CommandValidator.AutoUsage("cmd", null, new[] { "opt1" });
            StringAssert.Contains("cmd", result);
            StringAssert.Contains("[opt1=...]", result);
        }

        [Test]
        public void AutoUsage_NullOptionalArray_DoesNotThrow()
        {
            var result = CommandValidator.AutoUsage("cmd", new[] { "path" }, null);
            StringAssert.Contains("cmd", result);
            StringAssert.Contains("path=...", result);
        }
    }
}
