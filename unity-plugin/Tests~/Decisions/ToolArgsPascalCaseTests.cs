using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace UnityMCP.Decisions.Tests
{
    // T2 slice 3 (PascalCase rename, Plans/typed-catalog-args.md): ToolArgs.g.cs now names every class
    // and method by CatalogToolBlocks.ToPascalCase of the catalog/wire key, dropping the `@` escape that
    // was only ever needed because the template could not case-transform a reserved word into a safe
    // identifier. This file is the proof that the drop is safe catalog-wide, and the regression guard
    // that keeps it safe: every check below is a property of dsl/tools.biome today (0 violations) that
    // would start failing the moment a future catalog edit introduces one.
    public sealed class ToolArgsPascalCaseTests
    {
        private static readonly HashSet<string> ObjectInstanceMembers = new HashSet<string>
        {
            "Equals", "ToString", "GetHashCode", "GetType", "ReferenceEquals", "MemberwiseClone", "Finalize",
        };

        private static string Catalog() => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "tools.biome"));

        // Pinned catalog examples (dsl/tools.biome): one row per edge case named in the review brief.
        [TestCase("clip_name", "ClipName", TestName = "snake_case key (animation ARG clip_name)")]
        [TestCase("agentClimb", "AgentClimb", TestName = "already-camel key (navmesh_query ARG agentClimb)")]
        [TestCase("pos3", "Pos3", TestName = "key with a digit (set_rect ARG pos3)")]
        [TestCase("path", "Path", TestName = "single-word key (used by most tools)")]
        [TestCase("get_spatial_context", "GetSpatialContext", TestName = "tool name with underscores")]
        public void ToPascalCase_realCatalogExamples_matchToolchainAlgorithm(string key, string expected)
        {
            Assert.That(CatalogToolBlocks.ToPascalCase(key), Is.EqualTo(expected));
        }

        // Algorithm details no current catalog key happens to exercise, but the next one might.
        [TestCase("foo__bar", "FooBar", TestName = "consecutive separators collapse to one split, not an empty part")]
        [TestCase("_foo", "Foo", TestName = "a leading separator produces no empty leading part")]
        [TestCase("___", "", TestName = "only separators produce the empty identifier")]
        [TestCase("3d_mode", "3dMode", TestName = "a digit may lead a part; the algorithm does not fix that")]
        public void ToPascalCase_algorithmEdgeCases_matchToolchainAlgorithm(string key, string expected)
        {
            Assert.That(CatalogToolBlocks.ToPascalCase(key), Is.EqualTo(expected));
        }

        // The 3 WIRE-renamed args named in the review brief: the generated class/method pair the brief asked
        // to verify by name, read against the real catalog (not a synthetic fixture).
        [TestCase("asset", "class", "AssetArgs", "Class")]
        [TestCase("inspect_uitk", "include_internal", "InspectUitkArgs", "IncludeInternal")]
        [TestCase("uitk_file", "class", "UitkFileArgs", "Class")]
        public void WireRenamedArgs_matchTheNamedClassAndMethod(string tool, string wireKey, string expectedClass, string expectedMethod)
        {
            var block = CatalogToolBlocks.Parse(Catalog()).Single(b => b.Name == tool);
            var arg = block.Args.Single(a => a.IsWire && a.SourceKey == wireKey);
            Assert.That(CatalogToolBlocks.ToPascalCase(tool) + "Args", Is.EqualTo(expectedClass));
            Assert.That(CatalogToolBlocks.ToPascalCase(arg.SourceKey), Is.EqualTo(expectedMethod));
        }

        [Test]
        public void RealCatalog_noTwoArgsOfOneToolPascalCaseToTheSameMethodName()
        {
            var violations = new List<string>();
            foreach (var block in CatalogToolBlocks.Parse(Catalog()))
            {
                var byMethod = block.Args.GroupBy(a => CatalogToolBlocks.ToPascalCase(a.SourceKey));
                foreach (var g in byMethod.Where(g => g.Count() > 1))
                    violations.Add($"{block.Name}: {string.Join(", ", g.Select(a => a.SourceKey))} all PascalCase to {g.Key}");
            }
            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }

        [Test]
        public void RealCatalog_noTwoToolsPascalCaseToTheSameClassName()
        {
            var byClass = CatalogToolBlocks.Parse(Catalog()).GroupBy(b => CatalogToolBlocks.ToPascalCase(b.Name) + "Args");
            var violations = byClass.Where(g => g.Count() > 1)
                .Select(g => $"{g.Key}: {string.Join(", ", g.Select(b => b.Name))}").ToArray();
            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }

        [Test]
        public void RealCatalog_noMethodNamePascalCasesToAnObjectInstanceMember()
        {
            var violations = CatalogToolBlocks.Parse(Catalog())
                .SelectMany(b => b.Args.Select(a => (b.Name, a.SourceKey, Method: CatalogToolBlocks.ToPascalCase(a.SourceKey))))
                .Where(x => ObjectInstanceMembers.Contains(x.Method))
                .Select(x => $"{x.Name}: {x.SourceKey} -> {x.Method}").ToArray();
            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }

        [Test]
        public void RealCatalog_noMethodNamePascalCasesToItsOwnEnclosingClassName()
        {
            var violations = CatalogToolBlocks.Parse(Catalog())
                .SelectMany(b => b.Args.Select(a => (ClassName: CatalogToolBlocks.ToPascalCase(b.Name) + "Args", b.Name, a.SourceKey, Method: CatalogToolBlocks.ToPascalCase(a.SourceKey))))
                .Where(x => x.Method == x.ClassName)
                .Select(x => $"{x.Name}: {x.SourceKey} -> {x.Method} == class {x.ClassName}").ToArray();
            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }

        [Test]
        public void RealCatalog_everyGeneratedIdentifierIsNonEmptyAndLetterLeading()
        {
            var violations = new List<string>();
            foreach (var block in CatalogToolBlocks.Parse(Catalog()))
            {
                var className = CatalogToolBlocks.ToPascalCase(block.Name) + "Args";
                if (className.Length == 0 || !char.IsLetter(className[0]))
                    violations.Add($"class for tool {block.Name}: '{className}'");
                foreach (var a in block.Args)
                {
                    var method = CatalogToolBlocks.ToPascalCase(a.SourceKey);
                    if (method.Length == 0 || !char.IsLetter(method[0]))
                        violations.Add($"{block.Name}: {a.SourceKey} -> '{method}'");
                }
            }
            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }

        // Double-red proof: the four checks above pass today because dsl/tools.biome has zero violations,
        // not because the detection logic is inert. Each row below feeds a small hand-built catalog snippet
        // (never the real file) through the same CatalogToolBlocks.Parse + ToPascalCase pipeline and proves
        // the violation it names is actually caught.

        [Test]
        public void CollisionDetector_catchesTwoArgsOfOneToolPascalCasingToTheSameMethodName()
        {
            const string snippet = "TOOL fixture CORE RO \"fixture\"\n  ARG clip_name str DEFAULT None\n  ARG clipName str DEFAULT None\n";
            var block = CatalogToolBlocks.Parse(snippet).Single();
            var dup = block.Args.GroupBy(a => CatalogToolBlocks.ToPascalCase(a.SourceKey)).Where(g => g.Count() > 1).ToArray();
            Assert.That(dup, Is.Not.Empty, "clip_name and clipName must both PascalCase to ClipName");
            Assert.That(dup[0].Key, Is.EqualTo("ClipName"));
        }

        [Test]
        public void CollisionDetector_catchesTwoToolsPascalCasingToTheSameClassName()
        {
            const string snippet = "TOOL foo_bar CORE RO \"fixture\"\nTOOL foo__bar CORE RO \"fixture\"\n";
            var blocks = CatalogToolBlocks.Parse(snippet);
            var byClass = blocks.GroupBy(b => CatalogToolBlocks.ToPascalCase(b.Name) + "Args").Where(g => g.Count() > 1).ToArray();
            Assert.That(byClass, Is.Not.Empty, "foo_bar and foo__bar (collapsed separators) must both PascalCase to FooBarArgs");
            Assert.That(byClass[0].Key, Is.EqualTo("FooBarArgs"));
        }

        [Test]
        public void CollisionDetector_catchesAMethodNameThatPascalCasesToAnObjectInstanceMember()
        {
            const string snippet = "TOOL fixture CORE RO \"fixture\"\n  ARG equals str DEFAULT None\n";
            var block = CatalogToolBlocks.Parse(snippet).Single();
            var hit = block.Args.Select(a => CatalogToolBlocks.ToPascalCase(a.SourceKey)).Where(ObjectInstanceMembers.Contains).ToArray();
            Assert.That(hit, Is.EquivalentTo(new[] { "Equals" }));
        }

        [Test]
        public void CollisionDetector_catchesAMethodNameEqualToItsEnclosingClassName()
        {
            const string snippet = "TOOL x CORE RO \"fixture\"\n  ARG x_args str DEFAULT None\n";
            var block = CatalogToolBlocks.Parse(snippet).Single();
            var className = CatalogToolBlocks.ToPascalCase(block.Name) + "Args";
            var hit = block.Args.Select(a => CatalogToolBlocks.ToPascalCase(a.SourceKey)).Where(m => m == className).ToArray();
            Assert.That(hit, Is.EquivalentTo(new[] { "XArgs" }));
        }

        [Test]
        public void CollisionDetector_catchesADigitLeadingIdentifierAfterTheTransform()
        {
            const string snippet = "TOOL fixture CORE RO \"fixture\"\n  ARG param3d str DEFAULT None WIRE \"3d_mode\"\n";
            var block = CatalogToolBlocks.Parse(snippet).Single();
            var method = CatalogToolBlocks.ToPascalCase(block.Args.Single().SourceKey);
            Assert.That(method, Is.EqualTo("3dMode"));
            Assert.That(char.IsLetter(method[0]), Is.False, "a digit-leading identifier is not a valid C# identifier");
        }

        [Test]
        public void CollisionDetector_catchesAnEmptyIdentifierAfterTheTransform()
        {
            const string snippet = "TOOL fixture CORE RO \"fixture\"\n  ARG onlysep str DEFAULT None WIRE \"___\"\n";
            var block = CatalogToolBlocks.Parse(snippet).Single();
            var method = CatalogToolBlocks.ToPascalCase(block.Args.Single().SourceKey);
            Assert.That(method, Is.Empty, "a wire key made only of separators PascalCases to the empty identifier");
        }
    }
}
