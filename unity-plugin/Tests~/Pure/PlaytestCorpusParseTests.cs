using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityMCP.Playtest.Core;

namespace UnityMCP.Playtest.Core.PureTests
{
    // D16 — pure dotnet-test counterpart to B11's in-Unity golden parser corpus
    // (unity-plugin/Editor/Tests/PlaytestParserCorpusTests.cs). Same 11
    // MCPFeedbackFixture .playtest files, same hand-counted step counts (kept in
    // sync with that file's own comment: "every non-blank, non-comment,
    // non-INCLUDE/VAL/VAR line becomes exactly one PlaytestStep") — proven here
    // with zero Unity Editor, zero license, zero run_unity_tests.py. Distinct
    // fixture, not a duplicate: this one runs the actual repo-root file-system
    // discovery and an explicit resolver, because `dotnet test`'s working
    // directory is not the Unity project root the way Editor tests' relative
    // paths assume.
    [TestFixture]
    public class PlaytestCorpusParseTests
    {
        private const string FixtureRelDir = "unity-test-project/Assets/MCPFeedbackFixture/PlayTests";
        private const string DefsRelDir = "unity-test-project/Assets/PlaytestDefs";

        private static string _repoRoot;

        // Bounded parent walk from TestContext.CurrentContext.TestDirectory (the
        // test assembly's output dir under Tests~/Pure/bin/...), requiring both
        // the unity-plugin/ and unity-test-project/ sentinels together so this
        // can't false-positive on an unrelated ancestor directory.
        private static string RepoRoot()
        {
            if (_repoRoot != null) return _repoRoot;
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            for (var depth = 0; depth < 12 && dir != null; depth++, dir = dir.Parent)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "unity-plugin")) &&
                    Directory.Exists(Path.Combine(dir.FullName, "unity-test-project")))
                {
                    _repoRoot = dir.FullName;
                    return _repoRoot;
                }
            }
            throw new InvalidOperationException(
                "Could not locate repo root (unity-plugin/ + unity-test-project/ sentinels) walking up from " +
                TestContext.CurrentContext.TestDirectory);
        }

        private static string DefsDir =>
            Path.Combine(RepoRoot(), DefsRelDir.Replace('/', Path.DirectorySeparatorChar));

        private static string FixtureDir =>
            Path.Combine(RepoRoot(), FixtureRelDir.Replace('/', Path.DirectorySeparatorChar));

        // Explicit resolver: maps an INCLUDE filename only into PlaytestDefs/,
        // rejects traversal, reads UTF-8. Never `resolver: null` — Core's own
        // null-resolver fallback hardcodes a relative "Assets/PlaytestDefs/"
        // read, which resolves nowhere when the working directory is dotnet
        // test's own bin/ output folder instead of the Unity project root.
        private static string ResolvePlaytestDefs(string filename)
        {
            if (filename.Contains("..") || Path.IsPathRooted(filename))
                throw new ArgumentException($"INCLUDE '{filename}': path traversal not allowed");
            var baseDir = DefsDir;
            var baseWithSep = baseDir.EndsWith(Path.DirectorySeparatorChar.ToString())
                ? baseDir
                : baseDir + Path.DirectorySeparatorChar;
            var fullPath = Path.GetFullPath(Path.Combine(baseDir, filename));
            if (!fullPath.StartsWith(baseWithSep, StringComparison.Ordinal))
                throw new ArgumentException($"INCLUDE '{filename}': path outside PlaytestDefs/");
            return File.ReadAllText(fullPath, Encoding.UTF8);
        }

        private static string ReadFixture(string fileName) =>
            File.ReadAllText(Path.Combine(FixtureDir, fileName), Encoding.UTF8);

        // Step counts hand-counted from each fixture file, identical to B11's
        // golden corpus (PlaytestParserCorpusTests.cs) — the two must never
        // drift apart silently, since they assert the same files.
        [TestCase("A_shared_setup.playtest", 4)]
        [TestCase("B_shared_continue.playtest", 3)]
        [TestCase("C_shared_finish.playtest", 4)]
        [TestCase("DSL_types.playtest", 7)]
        [TestCase("F_independent_fail.playtest", 2)]
        [TestCase("I1_independent_pass.playtest", 5)]
        [TestCase("I2_independent_pass.playtest", 5)]
        [TestCase("I3_independent_pass.playtest", 4)]
        [TestCase("INVOKE_arguments.playtest", 5)]
        [TestCase("L_long_pass.playtest", 3)]
        [TestCase("MOVEMENT_profiles.playtest", 4)]
        public void Parse_MCPFeedbackFixtureCorpus_MatchesHandCountedStepCount(string fileName, int expectedStepCount)
        {
            var raw = ReadFixture(fileName);
            var result = PlaytestParser.Parse(raw, resolver: ResolvePlaytestDefs);
            Assert.IsNull(result.Errors, $"{fileName}: unexpected parse errors");
            Assert.AreEqual(expectedStepCount, result.Steps.Count, $"{fileName}: hand-counted step count mismatch");
        }

        // The 2 .defs files themselves: common.defs carries 13 VAL aliases and
        // zero executable steps (every line is a VAL definition, consumed
        // before step construction); game_core.defs is a UTF-8-BOM-only file
        // (0 VAL, 0 steps) used elsewhere only as a default alias-source path.
        [TestCase("common.defs", 13, 0)]
        [TestCase("game_core.defs", 0, 0)]
        public void Parse_DefsFile_ProducesExpectedValCountAndZeroSteps(
            string fileName, int expectedValCount, int expectedStepCount)
        {
            var raw = File.ReadAllText(Path.Combine(DefsDir, fileName), Encoding.UTF8);
            var result = PlaytestParser.Parse(raw, resolver: ResolvePlaytestDefs);
            Assert.IsNull(result.Errors, $"{fileName}: unexpected parse errors");
            Assert.AreEqual(expectedStepCount, result.Steps.Count, $"{fileName}: expected zero executable steps");
            Assert.AreEqual(expectedValCount, result.ValDefs?.Count ?? 0, $"{fileName}: VAL alias count mismatch");
        }
    }
}
