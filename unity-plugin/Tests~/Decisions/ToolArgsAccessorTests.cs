using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Decisions.Tests
{
    // T2 slice 1: unity-plugin/Editor/Generated/ToolArgs.g.cs is a thin, per-tool accessor class generated from
    // dsl/tools.biome (TEMPLATE csargs). No handler calls it yet (slice 2+). These tests are the behaviour-preservation
    // oracle of Plans/typed-catalog-args.md: a differential check against the pre-existing extraction expression
    // (kept here as a literal, never deleted when a handler migrates) for every accessor RETURN TYPE the template
    // emits, plus a structural check that the catalog and the generated class agree on the accessor set.
    public sealed class ToolArgsAccessorTests
    {
        private static void AssertMatchesOracle<T>(Func<string, T> oracle, Func<string, T> accessor, params string[] fixtures)
        {
            foreach (var json in fixtures)
                Assert.That(accessor(json), Is.EqualTo(oracle(json)), $"mismatch for fixture: {json}");
        }

        // str: set_property's "component" (ARG component str).
        [Test]
        public void Str_accessor_matches_ExtractString_oracle()
        {
            AssertMatchesOracle(
                j => JsonHelper.ExtractString(j, "component"),
                j => set_propertyArgs.component(j),
                "{}", "{\"component\":null}", "{\"component\":\"Transform\"}", "{\"component\":\"Box\\\"Collider\"}");
        }

        // ostr: animation's "path" (ARG path ostr).
        [Test]
        public void Ostr_accessor_matches_ExtractString_oracle()
        {
            AssertMatchesOracle(
                j => JsonHelper.ExtractString(j, "path"),
                j => animationArgs.path(j),
                "{}", "{\"path\":null}", "{\"path\":\"/Player\"}");
        }

        // Any: set_property's "value" (ARG value Any) -- confirmed by every current C# caller using ExtractString.
        [Test]
        public void Any_accessor_matches_ExtractString_oracle()
        {
            AssertMatchesOracle(
                j => JsonHelper.ExtractString(j, "value"),
                j => set_propertyArgs.value(j),
                "{}", "{\"value\":null}", "{\"value\":\"0,0,0\"}", "{\"value\":true}");
        }

        // int: brief_build's "budget" (ARG budget int DEFAULT 2000).
        [Test]
        public void Int_accessor_matches_ExtractInt_oracle()
        {
            AssertMatchesOracle(
                j => JsonHelper.ExtractInt(j, "budget"),
                j => brief_buildArgs.budget(j),
                "{}", "{\"budget\":null}", "{\"budget\":500}", "{\"budget\":\"500\"}");
        }

        // oint: animation's "int_param" (ARG int_param oint).
        [Test]
        public void Oint_accessor_matches_ParseOptInt_oracle()
        {
            AssertMatchesOracle(
                j => JsonHelper.ParseOptInt(j, "int_param"),
                j => animationArgs.int_param(j),
                "{}", "{\"int_param\":null}", "{\"int_param\":3}", "{\"int_param\":\"3\"}");
        }

        // float: get_spatial_context's "radius" (ARG radius float DEFAULT 5.0).
        [Test]
        public void Float_accessor_matches_ExtractFloat_oracle()
        {
            AssertMatchesOracle(
                j => JsonHelper.ExtractFloat(j, "radius"),
                j => get_spatial_contextArgs.radius(j),
                "{}", "{\"radius\":null}", "{\"radius\":2.5}", "{\"radius\":\"2.5\"}");
        }

        // ofloat: animation's "time" (ARG time ofloat).
        [Test]
        public void Ofloat_accessor_matches_ParseOptFloat_oracle()
        {
            AssertMatchesOracle(
                j => JsonHelper.ParseOptFloat(j, "time"),
                j => animationArgs.time(j),
                "{}", "{\"time\":null}", "{\"time\":1.5}", "{\"time\":\"1.5\"}");
        }

        // bool: animation's "compact" (ARG compact bool) -- ExtractString(...) == "true" tolerates both a bare and a quoted token.
        [Test]
        public void Bool_accessor_matches_string_equality_oracle()
        {
            AssertMatchesOracle(
                j => JsonHelper.ExtractString(j, "compact") == "true",
                j => animationArgs.compact(j),
                "{}", "{\"compact\":null}", "{\"compact\":true}", "{\"compact\":\"true\"}", "{\"compact\":false}");
        }

        // obool: animator's "has_exit_time" (ARG has_exit_time obool).
        [Test]
        public void Obool_accessor_matches_ParseOptBool_oracle()
        {
            AssertMatchesOracle(
                j => JsonHelper.ParseOptBool(j, "has_exit_time"),
                j => animatorArgs.has_exit_time(j),
                "{}", "{\"has_exit_time\":null}", "{\"has_exit_time\":true}", "{\"has_exit_time\":\"true\"}", "{\"has_exit_time\":false}");
        }

        // Direct characterization of the moved JsonHelper.ParseOpt* (CommandRouter.MediaHandlers.cs:9-25 before this
        // slice): current behaviour, not a fix. float.Parse/int.Parse on a malformed token throws -- that is what the
        // pre-move code did too (no try/catch), and every one of its 27 call sites in MediaHandlers.cs is unchanged.
        [Test]
        public void ParseOptFloat_malformed_token_throws_FormatException()
        {
            Assert.Throws<FormatException>(() => JsonHelper.ParseOptFloat("{\"time\":\"not-a-number\"}", "time"));
        }

        [Test]
        public void ParseOptInt_malformed_token_throws_FormatException()
        {
            Assert.Throws<FormatException>(() => JsonHelper.ParseOptInt("{\"int_param\":\"not-a-number\"}", "int_param"));
        }

        [TestCase("{}")]
        [TestCase("{\"time\":null}")]
        [TestCase("{\"time\":2.5}")]
        [TestCase("{\"time\":\"2.5\"}")]
        public void ParseOptFloat_is_null_only_when_key_is_absent_or_null(string json)
        {
            var result = JsonHelper.ParseOptFloat(json, "time");
            var keyIsAbsentOrNull = JsonHelper.ExtractString(json, "time") == null;
            Assert.That(result.HasValue, Is.EqualTo(!keyIsAbsentOrNull));
        }

        [TestCase("{}")]
        [TestCase("{\"int_param\":null}")]
        [TestCase("{\"int_param\":2}")]
        [TestCase("{\"int_param\":\"2\"}")]
        public void ParseOptInt_is_null_only_when_key_is_absent_or_null(string json)
        {
            var result = JsonHelper.ParseOptInt(json, "int_param");
            var keyIsAbsentOrNull = JsonHelper.ExtractString(json, "int_param") == null;
            Assert.That(result.HasValue, Is.EqualTo(!keyIsAbsentOrNull));
        }

        [TestCase("{}", null)]
        [TestCase("{\"compact\":null}", null)]
        [TestCase("{\"compact\":true}", true)]
        [TestCase("{\"compact\":\"true\"}", true)]
        [TestCase("{\"compact\":false}", false)]
        public void ParseOptBool_tolerates_bare_and_quoted_tokens(string json, bool? expected)
        {
            Assert.That(JsonHelper.ParseOptBool(json, "compact"), Is.EqualTo(expected));
        }

        // Structural oracle: the catalog (dsl/tools.biome, copied beside the test assembly) and the generated
        // ToolArgs.g.cs agree on the accessor set for every TOOL/PYTOOL/INTERNAL block -- not a hand list, so a
        // template that drops or renames an ARG's accessor fails here without anyone updating this test. A LOCAL
        // ARG (Python-only; excluded from cstools' Required/Optional too, so C# never receives it on the wire) has
        // no accessor. A WIRE-renamed ARG's accessor is named and keyed by the wire name, never the catalog name.
        [Test]
        public void Every_non_local_catalog_arg_has_exactly_one_accessor_named_by_its_wire_key()
        {
            var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "tools.biome"));
            var headRe = new Regex(@"^(?:TOOL|PYTOOL|INTERNAL) ([a-z][a-z0-9_]*)\b", RegexOptions.Multiline);
            var argRe = new Regex(@"^  ARG ([A-Za-z][A-Za-z0-9_]*) (?:str|ostr|int|oint|float|ofloat|bool|obool|Any)\b(.*)$", RegexOptions.Multiline);

            var heads = headRe.Matches(source).Cast<Match>().ToList();
            Assert.That(heads, Is.Not.Empty, "no TOOL/PYTOOL/INTERNAL head found: this test would pass without checking anything");

            for (var i = 0; i < heads.Count; i++)
            {
                var name = heads[i].Groups[1].Value;
                var blockStart = heads[i].Index;
                var blockEnd = i + 1 < heads.Count ? heads[i + 1].Index : source.Length;
                var block = source.Substring(blockStart, blockEnd - blockStart);
                var expected = argRe.Matches(block).Cast<Match>()
                    .Where(m => !Regex.IsMatch(m.Groups[2].Value, @"(?:^|\s)LOCAL(?:\s|$)"))
                    .Select(m =>
                    {
                        var wire = Regex.Match(m.Groups[2].Value, "WIRE \"([^\"]+)\"");
                        return wire.Success ? wire.Groups[1].Value : m.Groups[1].Value;
                    })
                    .OrderBy(n => n)
                    .ToArray();

                var type = Type.GetType($"UnityMCP.Editor.{name}Args");
                Assert.That(type, Is.Not.Null, $"no generated UnityMCP.Editor.{name}Args for catalog tool '{name}'");

                var actual = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(m => m.DeclaringType == type && !m.IsSpecialName)
                    .Select(m => m.Name)
                    .OrderBy(n => n)
                    .ToArray();

                CollectionAssert.AreEqual(expected, actual, $"accessor set mismatch for tool '{name}'");
            }
        }

        // WIRE-renamed ARGs (dsl/tools.biome): the catalog name must never appear on the C# plane. Each case
        // proves the accessor reads the wire key AND returns nothing when only the catalog name is present.
        [TestCase("class_name", "class")]
        public void Asset_class_accessor_reads_the_wire_key_not_the_catalog_name(string catalogName, string wireKey)
        {
            Assert.That(assetArgs.@class("{\"" + wireKey + "\":\"ScriptableObject\"}"), Is.EqualTo("ScriptableObject"));
            Assert.That(assetArgs.@class("{\"" + catalogName + "\":\"ScriptableObject\"}"), Is.Null,
                "reading the catalog name instead of the wire key would silently return the wrong value in a real call");
        }

        [TestCase("show_unity_private", "include_internal")]
        public void InspectUitk_include_internal_accessor_reads_the_wire_key_not_the_catalog_name(string catalogName, string wireKey)
        {
            Assert.That(inspect_uitkArgs.@include_internal("{\"" + wireKey + "\":true}"), Is.True);
            Assert.That(inspect_uitkArgs.@include_internal("{\"" + catalogName + "\":true}"), Is.Null,
                "reading the catalog name instead of the wire key would silently return null/false in a real call");
        }

        [TestCase("cls", "class")]
        public void UitkFile_class_accessor_reads_the_wire_key_not_the_catalog_name(string catalogName, string wireKey)
        {
            Assert.That(uitk_fileArgs.@class("{\"" + wireKey + "\":\"my-class\"}"), Is.EqualTo("my-class"));
            Assert.That(uitk_fileArgs.@class("{\"" + catalogName + "\":\"my-class\"}"), Is.Null,
                "reading the catalog name instead of the wire key would silently return the wrong value in a real call");
        }

        // LOCAL ARGs never reach C#: get_component's "full"/"component" and screenshot's "describe"/"raw" are
        // Python-only (consumed before _send); no UnityMCP.Editor.get_componentArgs/screenshotArgs method exists
        // for them. Proven by absence via reflection, not by a differential call (there is nothing to call).
        [TestCase("get_componentArgs", new[] { "full", "component" })]
        [TestCase("screenshotArgs", new[] { "describe", "raw" })]
        public void Local_args_have_no_accessor(string className, string[] localArgNames)
        {
            var type = Type.GetType($"UnityMCP.Editor.{className}");
            Assert.That(type, Is.Not.Null);
            var methodNames = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(m => m.DeclaringType == type && !m.IsSpecialName)
                .Select(m => m.Name)
                .ToArray();
            foreach (var local in localArgNames)
                Assert.That(methodNames, Does.Not.Contain(local), $"LOCAL arg '{local}' must have no C# accessor");
        }
    }
}
