using NUnit.Framework;
using UnityMCP.Editor.TestRuns;

namespace UnityMCP.Editor.Tests
{
    /// <summary>
    /// TestRunSelectionArgs.ParseList supports both wire-native JSON arrays
    /// (Python A22 sends command_args["categories"] = [...]) and a
    /// comma-separated string (CLI ergonomics for a hand-typed command).
    /// </summary>
    [TestFixture]
    internal sealed class TestRunSelectionArgsTests
    {
        [Test]
        public void Execute_ParsesArraySelectionArgs_JsonArray()
        {
            var result = TestRunSelectionArgs.ParseList(
                "{\"categories\":[\"Fast\",\"!Stress\"]}", "categories");

            CollectionAssert.AreEqual(new[] { "Fast", "!Stress" }, result);
        }

        [Test]
        public void Execute_ParsesArraySelectionArgs_CommaSeparatedString()
        {
            var result = TestRunSelectionArgs.ParseList(
                "{\"categories\":\"Fast,!Stress\"}", "categories");

            CollectionAssert.AreEqual(new[] { "Fast", "!Stress" }, result);
        }

        [Test]
        public void Execute_ParsesArraySelectionArgs_MultipleKeysDoNotCollide()
        {
            // A comma-string value for one key must never make ParseList spill
            // into a later key's JSON array (ExtractArray-style forward scan
            // would incorrectly grab "tests"'s bracket here).
            const string json =
                "{\"categories\":\"Fast,!Stress\",\"tests\":[\"Suite.A\"]}";

            CollectionAssert.AreEqual(
                new[] { "Fast", "!Stress" }, TestRunSelectionArgs.ParseList(json, "categories"));
            CollectionAssert.AreEqual(
                new[] { "Suite.A" }, TestRunSelectionArgs.ParseList(json, "tests"));
        }

        [TestCase("{}")]
        [TestCase("{\"categories\":[]}")]
        [TestCase(null)]
        public void Execute_ParsesArraySelectionArgs_EmptyOrMissingYieldsArrayEmpty(string json)
        {
            CollectionAssert.AreEqual(
                System.Array.Empty<string>(), TestRunSelectionArgs.ParseList(json, "categories"));
        }
    }
}
