using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;

namespace UnityMCP.Decisions.Tests
{
    // Every row of the examples of a module goes through the generated Policy of that module (UnityMCP.Editor.Decisions.<Module>.Policy).
    // The rows are dsl/decisions/<module>.json: printed from <module>.examples, never authored; the expectation of a row was
    // transcribed from a test of the project, with its file and line in the provenance. The same rows run on the generated Python
    // (server/tests/test_decisions_examples.py). The policy is found by reflection, and the input struct is built from the names of
    // its constructor parameters, so a fact the contract adds or renames is a failure of every row, not a silent default.
    public sealed class RowsTests
    {
        // The modules that are printed into C#; retry is Python only (step 7.2) and has rows but no policy here.
        private static readonly string[] Modules = { "mutability", "verdict" };

        public static IEnumerable<TestCaseData> Rows()
        {
            foreach (var module in Modules)
            {
                var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "rows", module + ".json")));
                foreach (var decision in document.RootElement.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.Array))
                    foreach (var row in decision.Value.EnumerateArray())
                        yield return new TestCaseData(module, decision.Name, row.Clone())
                            .SetName($"{module}.{decision.Name}.{row.GetProperty("id").GetString()}");
            }
        }

        [TestCaseSource(nameof(Rows))]
        public void Row_gives_its_expectation(string module, string decision, JsonElement row)
        {
            var policy = Type.GetType($"UnityMCP.Editor.Decisions.{Pascal(module)}.Policy", throwOnError: true);
            var inputType = policy.GetNestedType(Pascal(decision) + "Input");
            Assert.That(inputType, Is.Not.Null, "no input struct for " + decision);
            var constructor = inputType.GetConstructors().Single();
            var facts = row.GetProperty("facts");
            var arguments = constructor.GetParameters().Select(parameter =>
            {
                var fact = facts.EnumerateObject().SingleOrDefault(f => string.Equals(f.Name, parameter.Name, StringComparison.OrdinalIgnoreCase));
                Assert.That(fact.Name, Is.Not.Null, "the row has no fact " + parameter.Name);
                return Convert(fact.Value, parameter.ParameterType);
            }).ToArray();
            Assert.That(arguments.Length, Is.EqualTo(facts.EnumerateObject().Count()), "the row has facts that the input does not take");

            var got = policy.GetMethod(Pascal(decision), new[] { inputType }).Invoke(null, new[] { constructor.Invoke(arguments) });

            Assert.That(got, Is.EqualTo(Convert(row.GetProperty("expected"), got.GetType())), row.GetProperty("provenance").GetString());
        }

        [Test]
        public void Every_module_has_rows()
        {
            foreach (var module in Modules)
                Assert.That(Rows().Count(c => c.TestName.StartsWith(module + ".")), Is.GreaterThan(0), module);
        }

        private static string Pascal(string name) => char.ToUpperInvariant(name[0]) + name.Substring(1);

        private static object Convert(JsonElement value, Type type) =>
            type == typeof(bool) ? value.GetBoolean()
            : type == typeof(double) ? value.GetDouble()
            : type == typeof(string) ? value.GetString()
            : throw new NotSupportedException("a fact of type " + type);
    }
}
