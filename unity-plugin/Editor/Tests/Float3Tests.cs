// TDD for D03 — Float3 engine-free value type in Core.
// Float3 replaces UnityEngine.Vector3 as PlaytestStep.Position's storage type (D04)
// so the Core parser assembly (noEngineReferences) can hold a position without
// referencing UnityEngine. [Serializable] is load-bearing: VisualStep.cs serializes
// PlaytestStep for the Composer UI. ToString() format is load-bearing too — D04's
// TELEPORT result line interpolates {step.Position} directly.
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class Float3Tests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Constructor_ThreeFloats_SetsXyzFields()
        {
            var v = new Float3(1f, 2f, 3f);
            Assert.AreEqual(1f, v.x);
            Assert.AreEqual(2f, v.y);
            Assert.AreEqual(3f, v.z);
        }

        // Double-red: also fails if [Serializable] is ever removed from Float3.
        [Test]
        public void Type_IsSerializable()
        {
            Assert.IsTrue(typeof(Float3).IsSerializable,
                "Float3 must be [Serializable] — VisualStep.cs serializes PlaytestStep, " +
                "which holds a Float3 Position field (D04).");
        }

        // Double-red: also fails if ToString ever drops a component or changes the separator.
        [Test]
        public void ToString_ReturnsCommaSeparatedXyz()
        {
            var v = new Float3(1f, 2f, 3f);
            Assert.AreEqual("1,2,3", v.ToString());
        }

        // Double-red: fails under a comma-decimal culture (e.g. de-DE) if ToString
        // does not force InvariantCulture per-component — "1,5,2,5,3,5" would be
        // ambiguous with the field separator itself.
        [Test]
        public void ToString_FractionalValues_UsesInvariantCultureDecimalPoint()
        {
            var saved = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture =
                    new System.Globalization.CultureInfo("de-DE");
                var v = new Float3(1.5f, 2.5f, 3.5f);
                Assert.AreEqual("1.5,2.5,3.5", v.ToString());
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = saved;
            }
        }
    }
}
