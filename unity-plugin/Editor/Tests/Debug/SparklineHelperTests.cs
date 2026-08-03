// TDD: SparklineHelper Unicode sparkline generation.
using System.Collections.Generic;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class SparklineHelperTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Null_ReturnsEmpty() =>
            Assert.AreEqual("", SparklineHelper.Generate(null));

        [Test]
        public void EmptyList_ReturnsEmpty() =>
            Assert.AreEqual("", SparklineHelper.Generate(new List<float>()));

        [Test]
        public void SingleValue_ReturnsOneChar() =>
            Assert.AreEqual(1, SparklineHelper.Generate(new[] { 5f }).Length);

        [Test]
        public void AllSame_ReturnsMiddleBlocks()
        {
            // range==0 → index = Blocks.Length/2 = 4 → '▅'
            var result = SparklineHelper.Generate(new[] { 3f, 3f, 3f, 3f }, width: 4);
            Assert.AreEqual(4, result.Length);
            foreach (var c in result) Assert.AreEqual('▅', c);
        }

        [Test]
        public void Ascending8_FirstLowLastHigh()
        {
            var result = SparklineHelper.Generate(new[] { 0f, 1f, 2f, 3f, 4f, 5f, 6f, 7f });
            Assert.AreEqual('▁', result[0]);
            Assert.AreEqual('█', result[7]);
        }

        [Test]
        public void Width_TruncatesToWidth()
        {
            var values = new float[20];
            for (int i = 0; i < 20; i++) values[i] = i;
            Assert.AreEqual(8, SparklineHelper.Generate(values, width: 8).Length);
        }

        [Test]
        public void Width_UsesLastNValues()
        {
            // Last 8 values of 0..19 are 12..19 → ascending → first='▁', last='█'
            var values = new float[20];
            for (int i = 0; i < 20; i++) values[i] = i;
            var result = SparklineHelper.Generate(values, width: 8);
            Assert.AreEqual('▁', result[0]);
            Assert.AreEqual('█', result[7]);
        }

        [Test]
        public void Descending_FirstHighLastLow()
        {
            var result = SparklineHelper.Generate(new[] { 7f, 6f, 5f, 4f, 3f, 2f, 1f, 0f });
            Assert.AreEqual('█', result[0]);
            Assert.AreEqual('▁', result[7]);
        }

        [Test]
        public void NegativeValues_Works()
        {
            var result = SparklineHelper.Generate(new[] { -5f, -3f, -1f, 0f });
            Assert.AreEqual(4, result.Length);
            Assert.AreEqual('▁', result[0]);
            Assert.AreEqual('█', result[3]);
        }

        [Test]
        public void FewerValuesThanWidth_ReturnsValuesCount()
        {
            var result = SparklineHelper.Generate(new[] { 1f, 2f, 3f });
            Assert.AreEqual(3, result.Length);
        }
    }
}
