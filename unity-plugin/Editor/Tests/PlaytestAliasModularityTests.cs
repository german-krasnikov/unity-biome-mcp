// Extensibility seam tests: delegate injection, implicit cast, ShallowClone, HasAny gate.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityMCP.Playtest.Core;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlaytestAliasModularityTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // E1 — VarRegistry injectable delegate: unit-testable without Unity runtime
        [Test]
        public void VarRegistry_InjectableDelegate_ResolvesBypassesUnityRuntime()
        {
            var callLog = new List<string>();
            var registry = new PlaytestVarRegistry((path, comp, field) => {
                callLog.Add($"{path}|{comp}|{field}");
                return field == "hp" ? "75" : "0";
            });
            registry.Register("hp", "@/Player|Health|hp");
            var expanded = registry.ExpandVars("$hp");
            Assert.AreEqual("75", expanded);
            Assert.AreEqual(1, callLog.Count);
            Assert.AreEqual("/Player|Health|hp", callLog[0]);
        }

        // E2 — IncludeResolver delegate: filesystem not touched during test
        [Test]
        public void Parse_Include_CustomResolver_FileSystemNotTouched()
        {
            bool resolverCalled = false;
            IncludeResolver spy = filename => {
                resolverCalled = true;
                Assert.AreEqual("myfile.defs", filename);
                return "VAL $x /TestPath";
            };
            var result = PlaytestParser.Parse("INCLUDE myfile.defs\nLOG $x", spy);
            Assert.IsTrue(resolverCalled);
            Assert.AreEqual("/TestPath", result[0].Message);
        }

        // E3 — ParseResult implicit cast to List<PlaytestStep> preserves backward compat
        [Test]
        public void ParseResult_ImplicitCastToList_PreservesAllSteps()
        {
            List<PlaytestStep> steps = PlaytestParser.Parse("WAIT 1\nLOG ok");
            Assert.AreEqual(2, steps.Count);
            Assert.AreEqual(StepType.Wait, steps[0].Type);
            Assert.AreEqual(StepType.Log, steps[1].Type);
        }

        // E4 — PlaytestStep.ShallowClone: all fields copied, mutation isolated, arrays shared
        [Test]
        public void PlaytestStep_ShallowClone_AllFieldsCopied_MutationIsolated()
        {
            var original = new PlaytestStep {
                Type = StepType.Assert, Path = "/A", Query = "$hp", Op = "==",
                Value = "100", Timeout = 5f, Message = "msg", Label = "lbl",
                IsOr = true, AbortOnFail = true, RawLine = "raw",
                Component = "Health", Method = "Check", Args = "x",
                Queries = new[] { "q1" }, BatchOps = new[] { ">=" }, BatchValues = new[] { "0" }
            };
            var clone = original.ShallowClone();
            // Mutate clone
            clone.Query = "MUTATED";
            clone.Value = "999";
            // Original unchanged
            Assert.AreEqual("$hp", original.Query);
            Assert.AreEqual("100", original.Value);
            // Clone has new values
            Assert.AreEqual("MUTATED", clone.Query);
            // Array references shared (shallow clone — by design)
            Assert.AreSame(original.Queries, clone.Queries);
            Assert.AreSame(original.BatchOps, clone.BatchOps);
        }

        // E5 — New keyword (CAPTURE) is transparent to alias expansion
        [Test]
        public void Parse_Val_ExpandedInCaptureStep_NewKeywordTransparent()
        {
            var script = "VAL $tracked /Player/Character\nCAPTURE hp_snapshot $tracked|Health|hp";
            var result = PlaytestParser.Parse(script);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(StepType.Capture, result[0].Type);
            Assert.AreEqual("/Player/Character|Health|hp", result[0].Query);
        }

        // E6 — VarRegistry.HasAny gate: false when empty, true after Register
        [Test]
        public void VarRegistry_HasAny_FalseWithNoRegistrations_TrueAfterRegister()
        {
            var registry = new PlaytestVarRegistry((_, __, ___) => "x");
            Assert.IsFalse(registry.HasAny);
            registry.Register("hp", "@/Player|Health|hp");
            Assert.IsTrue(registry.HasAny);
        }
    }
}
