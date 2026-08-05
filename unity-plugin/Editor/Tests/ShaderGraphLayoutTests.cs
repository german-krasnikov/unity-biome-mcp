using System.Collections.Generic;
using NUnit.Framework;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public sealed class ShaderGraphLayoutTests : UnityMcpTestBase
    {
        // ── Seam helpers ──────────────────────────────────────────────────────

        void SetReadSeam(string content)
        {
            ShaderGraphHelper._layoutReadOverride = _ => content;
            RegisterCleanup(() => ShaderGraphHelper._layoutReadOverride = null);
        }

        string _lastWritten;

        void SetWriteSeam()
        {
            _lastWritten = null;
            ShaderGraphHelper._layoutWriteOverride = (_, c) => _lastWritten = c;
            RegisterCleanup(() => ShaderGraphHelper._layoutWriteOverride = null);
        }

        // ── Mock content builders ─────────────────────────────────────────────

        static string NodeBlock(string id, string type, string name, float x, float y, float w, float h)
        {
            string F(float v) => v.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
            return $"{{\"m_ObjectId\": \"{id}\", \"m_Type\": \"{type}\", \"m_Name\": \"{name}\", " +
                   $"\"m_DrawState\": {{\"m_Expanded\": true, \"m_Position\": {{\"serializedVersion\": \"2\", " +
                   $"\"x\": {F(x)}, \"y\": {F(y)}, \"width\": {F(w)}, \"height\": {F(h)}}}}}}}";
        }

        static ShaderGraphHelper.NodeLayoutInfo N(string id, float x, float y, float w = 200f, float h = 100f) =>
            new ShaderGraphHelper.NodeLayoutInfo { id = id, x = x, y = y, w = w, h = h };

        static ShaderGraphHelper.EdgeInfo E(string outId, string inId) =>
            new ShaderGraphHelper.EdgeInfo { outputNodeId = outId, inputNodeId = inId };

        const string FakePath = "fake/graph.shadergraph";

        // ── ParseNodePositions ────────────────────────────────────────────────

        [Test]
        public void ParseNodePositions_EmptyContent_ReturnsEmpty()
        {
            Assert.That(ShaderGraphHelper.ParseNodePositions("").Count, Is.EqualTo(0));
        }

        [Test]
        public void ParseNodePositions_SingleNode_ExtractsPosition()
        {
            var content = NodeBlock("abc", "UnityEditor.ShaderGraph.SomeNode", "MyNode", 10, 20, 200, 100);
            var result = ShaderGraphHelper.ParseNodePositions(content);
            Assert.That(result.Count, Is.EqualTo(1));
            var n = result[0];
            Assert.That(n.id,   Is.EqualTo("abc"));
            Assert.That(n.type, Is.EqualTo("SomeNode"));
            Assert.That(n.name, Is.EqualTo("MyNode"));
            Assert.That(n.x,    Is.EqualTo(10f));
            Assert.That(n.y,    Is.EqualTo(20f));
            Assert.That(n.w,    Is.EqualTo(200f));
            Assert.That(n.h,    Is.EqualTo(100f));
        }

        [Test]
        public void ParseNodePositions_MultipleNodes_ExtractsAll()
        {
            var content = NodeBlock("id1", "T.A", "N1", 0, 0, 200, 100) + "\n" +
                          NodeBlock("id2", "T.B", "N2", 300, 0, 200, 100) + "\n" +
                          NodeBlock("id3", "T.C", "N3", 600, 0, 200, 100);
            Assert.That(ShaderGraphHelper.ParseNodePositions(content).Count, Is.EqualTo(3));
        }

        [Test]
        public void ParseNodePositions_SkipsBlockNodes_WhenWHZero()
        {
            var block   = NodeBlock("blk", "UnityEditor.ShaderGraph.BlockNode", "Vertex.Pos", 0, 0, 0, 0);
            var regular = NodeBlock("reg", "T.SomeNode", "Regular", 100, 100, 200, 100);
            var result  = ShaderGraphHelper.ParseNodePositions(block + "\n" + regular);
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].id, Is.EqualTo("reg"));
        }

        // ── CountOverlaps ─────────────────────────────────────────────────────

        [Test]
        public void CountOverlaps_NoOverlap_ReturnsZero()
        {
            var nodes = new List<ShaderGraphHelper.NodeLayoutInfo> { N("a", 0, 0, 200, 100), N("b", 300, 0, 200, 100) };
            Assert.That(ShaderGraphHelper.CountOverlaps(nodes), Is.EqualTo(0));
        }

        [Test]
        public void CountOverlaps_TwoOverlapping_ReturnsOne()
        {
            var nodes = new List<ShaderGraphHelper.NodeLayoutInfo> { N("a", 0, 0), N("b", 0, 0) };
            Assert.That(ShaderGraphHelper.CountOverlaps(nodes), Is.EqualTo(1));
        }

        [Test]
        public void CountOverlaps_AllAtOrigin_ReturnsNChoose2()
        {
            var nodes = new List<ShaderGraphHelper.NodeLayoutInfo>();
            for (int i = 0; i < 5; i++) nodes.Add(N($"n{i}", 0, 0));
            Assert.That(ShaderGraphHelper.CountOverlaps(nodes), Is.EqualTo(10)); // C(5,2)
        }

        [Test]
        public void CountOverlaps_Touching_ReturnsZero()
        {
            // a.x + a.w == b.x — strict inequality a.x+a.w > b.x is false → no overlap
            var nodes = new List<ShaderGraphHelper.NodeLayoutInfo> { N("a", 0, 0, 100, 100), N("b", 100, 0, 100, 100) };
            Assert.That(ShaderGraphHelper.CountOverlaps(nodes), Is.EqualTo(0));
        }

        // ── ComputeLayout ─────────────────────────────────────────────────────

        [Test]
        public void ComputeLayout_LinearChain_ColumnsLeftToRight()
        {
            var nodes = new List<ShaderGraphHelper.NodeLayoutInfo> { N("a", 0, 0), N("b", 0, 0), N("c", 0, 0) };
            var edges = new List<ShaderGraphHelper.EdgeInfo> { E("a", "b"), E("b", "c") };
            var result = ShaderGraphHelper.ComputeLayout(nodes, edges);
            var byId = new Dictionary<string, ShaderGraphHelper.NodeLayoutInfo>();
            foreach (var n in result) byId[n.id] = n;
            Assert.That(byId["a"].x, Is.LessThan(byId["b"].x));
            Assert.That(byId["b"].x, Is.LessThan(byId["c"].x));
        }

        [Test]
        public void ComputeLayout_DiamondGraph_CorrectLayers()
        {
            var nodes = new List<ShaderGraphHelper.NodeLayoutInfo> { N("a", 0,0), N("b", 0,0), N("c", 0,0), N("d", 0,0) };
            var edges = new List<ShaderGraphHelper.EdgeInfo> { E("a","b"), E("a","c"), E("b","d"), E("c","d") };
            var result = ShaderGraphHelper.ComputeLayout(nodes, edges);
            var byId = new Dictionary<string, ShaderGraphHelper.NodeLayoutInfo>();
            foreach (var n in result) byId[n.id] = n;
            Assert.That(byId["a"].x, Is.LessThan(byId["b"].x));
            Assert.That(byId["b"].x, Is.EqualTo(byId["c"].x));   // same column
            Assert.That(byId["b"].x, Is.LessThan(byId["d"].x));
        }

        [Test]
        public void ComputeLayout_DisconnectedNodes_AllInLayer0()
        {
            var nodes = new List<ShaderGraphHelper.NodeLayoutInfo> { N("a", 100, 200), N("b", 300, 400) };
            var result = ShaderGraphHelper.ComputeLayout(nodes, new List<ShaderGraphHelper.EdgeInfo>());
            var byId = new Dictionary<string, ShaderGraphHelper.NodeLayoutInfo>();
            foreach (var n in result) byId[n.id] = n;
            Assert.That(byId["a"].x, Is.EqualTo(byId["b"].x));   // same column = layer 0
        }

        [Test]
        public void ComputeLayout_ZeroOverlapsAfter()
        {
            var nodes = new List<ShaderGraphHelper.NodeLayoutInfo>();
            for (int i = 0; i < 5; i++) nodes.Add(N($"n{i}", 0, 0));
            var result = ShaderGraphHelper.ComputeLayout(nodes, new List<ShaderGraphHelper.EdgeInfo>());
            Assert.That(ShaderGraphHelper.CountOverlaps(result), Is.EqualTo(0));
        }

        [Test, Timeout(1000)]
        public void ComputeLayout_CyclicEdges_DoesNotHang()
        {
            var nodes = new List<ShaderGraphHelper.NodeLayoutInfo>
            {
                N("a", 0, 0), N("b", 0, 0)
            };
            var edges = new List<ShaderGraphHelper.EdgeInfo>
            {
                E("a", "b"), E("b", "a")
            };
            var result = ShaderGraphHelper.ComputeLayout(nodes, edges);
            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(ShaderGraphHelper.CountOverlaps(result), Is.EqualTo(0));
        }

        // ── ApplyLayout ───────────────────────────────────────────────────────

        [Test]
        public void ApplyLayout_RoundTrip_PositionsMatch()
        {
            var content = NodeBlock("n1", "T.A", "Node1", 0, 0, 200, 100) + "\n" +
                          NodeBlock("n2", "T.B", "Node2", 0, 0, 200, 100);
            var nodes   = ShaderGraphHelper.ParseNodePositions(content);
            var laid    = ShaderGraphHelper.ComputeLayout(nodes, new List<ShaderGraphHelper.EdgeInfo>());
            var applied = ShaderGraphHelper.ApplyLayout(content, laid);
            var reparsed = ShaderGraphHelper.ParseNodePositions(applied);
            var byId = new Dictionary<string, ShaderGraphHelper.NodeLayoutInfo>();
            foreach (var n in reparsed) byId[n.id] = n;
            foreach (var expected in laid)
            {
                Assert.That(byId[expected.id].x, Is.EqualTo(expected.x).Within(0.2f));
                Assert.That(byId[expected.id].y, Is.EqualTo(expected.y).Within(0.2f));
            }
        }

        [Test]
        public void ApplyLayout_PreservesNonPositionData()
        {
            var content  = NodeBlock("n1", "UnityEditor.ShaderGraph.SomeNode", "MyNode", 0, 0, 200, 100);
            var newPos   = new List<ShaderGraphHelper.NodeLayoutInfo> { N("n1", 500, 300) };
            var result   = ShaderGraphHelper.ApplyLayout(content, newPos);
            StringAssert.Contains("UnityEditor.ShaderGraph.SomeNode", result);
            StringAssert.Contains("MyNode", result);
        }

        // ── GetLayout / SetLayout / AutoLayout ────────────────────────────────

        [Test]
        public void GetLayout_ReturnsCompactText()
        {
            SetReadSeam(NodeBlock("abc", "UnityEditor.ShaderGraph.AddNode", "MyAdd", 10, 20, 200, 100));
            var result = ShaderGraphHelper.GetLayout(FakePath);
            StringAssert.Contains("layout: 1 nodes", result);
            StringAssert.Contains("[abc]", result);
            StringAssert.Contains("AddNode", result);
            StringAssert.Contains("\"MyAdd\"", result);
            StringAssert.Contains("@ 10,20", result);
            StringAssert.Contains("200x100", result);
        }

        [Test]
        public void SetLayout_UpdatesPositions()
        {
            SetReadSeam(NodeBlock("n1", "T.SomeNode", "Foo", 0, 0, 200, 100));
            SetWriteSeam();
            ShaderGraphHelper.SetLayout(FakePath, "[n1] SomeNode \"Foo\" @ 500,300 200x100");
            Assert.That(_lastWritten, Is.Not.Null);
            var reparsed = ShaderGraphHelper.ParseNodePositions(_lastWritten);
            Assert.That(reparsed.Count, Is.EqualTo(1));
            Assert.That(reparsed[0].x, Is.EqualTo(500f).Within(0.2f));
            Assert.That(reparsed[0].y, Is.EqualTo(300f).Within(0.2f));
        }

        [Test]
        public void AutoLayout_ReturnsReport()
        {
            // Two nodes at origin → 1 overlap before layout
            var content = NodeBlock("n1", "T.NodeA", "NodeA", 0, 0, 200, 100) + "\n" +
                          NodeBlock("n2", "T.NodeB", "NodeB", 0, 0, 200, 100);
            SetReadSeam(content);
            SetWriteSeam();
            var report = ShaderGraphHelper.AutoLayout(FakePath);
            StringAssert.Contains("auto_layout:", report);
            StringAssert.Contains("nodes repositioned", report);
            StringAssert.Contains("overlaps resolved", report);
        }
    }
}
