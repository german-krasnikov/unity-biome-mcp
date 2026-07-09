// Performance and stress tests for VAL/VAR/INCLUDE system.
using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlaytestAliasStressTests
    {
        // D1 — 100 VAL aliases: parse completes under 200ms
        [Test]
        public void Parse_100ValAliases_CompletesUnder200ms()
        {
            // NVals generates "VAL $alias0 /Path/Object0|Comp0|field0" — use bare $alias0 in query
            var script = AliasHelpers.NVals(100, suffix: "ASSERT $alias0 == active");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = PlaytestParser.Parse(script);
            sw.Stop();
            Assert.Less(sw.ElapsedMilliseconds, 200L, $"Took {sw.ElapsedMilliseconds}ms");
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("/Path/Object0|Comp0|field0", result[0].Query);
        }

        // D2 — Deep nested VAL chain depth 10: resolves correctly
        [Test]
        public void Parse_Val_NestedChainDepth10_ResolvesToRoot()
        {
            var script = AliasHelpers.ChainVals(10);
            var result = PlaytestParser.Parse(script);
            Assert.AreEqual(1, result.Count);
            // $v10 → $v9 → ... → $v0 = ROOT
            StringAssert.StartsWith("ROOT", result[0].Message);
        }

        // D3 — Same $name used 500 times: parse completes under 500ms
        [Test]
        public void Parse_Val_SingleAliasUsed500Times_CompletesUnder500ms()
        {
            var sb = new StringBuilder("VAL $obj /World/Player\n");
            for (int i = 0; i < 500; i++)
                sb.AppendLine($"ASSERT $obj|Health|hp == {i}");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = PlaytestParser.Parse(sb.ToString());
            sw.Stop();
            Assert.Less(sw.ElapsedMilliseconds, 500L);
            Assert.AreEqual(500, result.Count);
            Assert.AreEqual("/World/Player|Health|hp", result[0].Query);
        }

        // D4 — 50 VARs: ExpandVars only calls ReadValue for referenced var
        [Test]
        public void VarRegistry_50Vars_ExpandVarsCallsReadValueExactlyOnce()
        {
            int callCount = 0;
            var registry = new PlaytestVarRegistry((p, c, f) => { callCount++; return "42"; });
            for (int i = 0; i < 50; i++)
                registry.Register($"v{i}", $"@/Path{i}|Comp|field");
            // Only $v0 referenced in text — ReadValue called only once
            registry.ExpandVars("$v0 > 10");
            Assert.AreEqual(1, callCount, "Should resolve only the referenced var, not all 50");
        }

        // D5 — INCLUDE chain max depth 5 throws
        [Test]
        public void Parse_Include_MaxDepth5_ThrowsMaxDepthExceeded()
        {
            var files = new Dictionary<string, string>();
            for (int i = 1; i <= 6; i++)
                files[$"inc{i}.defs"] = i < 6 ? $"INCLUDE inc{i+1}.defs" : "VAL $leaf ok";
            var resolver = AliasHelpers.FileMap(files);
            var ex = Assert.Throws<ArgumentException>(
                () => PlaytestParser.Parse("INCLUDE inc1.defs\nLOG $leaf", resolver));
            StringAssert.Contains("depth", ex.Message.ToLower());
        }
    }
}
