// Tests for MermaidLayout. Pure, NUnit-testable.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class MermaidLayoutTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private static MermaidGraph Chain(MermaidDir dir, params string[] ids)
        {
            var g = new MermaidGraph { Dir = dir };
            foreach (var id in ids) g.EnsureNode(id);
            for (int i = 0; i < ids.Length - 1; i++)
                g.Edges.Add(new MermaidEdge { From = ids[i], To = ids[i + 1], Arrow = true });
            return g;
        }

        [Test]
        public void LinearChain_TD_LayersIncrement()
        {
            var g = Chain(MermaidDir.TD, "A", "B", "C");
            var r = MermaidLayout.Compute(g);
            var layerA = NodeLayer(r, "A");
            var layerB = NodeLayer(r, "B");
            var layerC = NodeLayer(r, "C");
            Assert.Less(layerA, layerB);
            Assert.Less(layerB, layerC);
        }

        [Test]
        public void Root_IsLayerZero()
        {
            var g = Chain(MermaidDir.TD, "A", "B");
            var r = MermaidLayout.Compute(g);
            // A has no incoming edges → layer 0
            var rect = FindRect(r, "A");
            Assert.AreEqual(0f, rect.Y, 1f); // Y=0 for TD layer 0
        }

        [Test]
        public void Diamond_MergeNodeLayer()
        {
            // A→B, A→C, B→D, C→D  — D must be deeper than B and C
            var g = new MermaidGraph { Dir = MermaidDir.TD };
            foreach (var id in new[] { "A", "B", "C", "D" }) g.EnsureNode(id);
            g.Edges.Add(new MermaidEdge { From = "A", To = "B", Arrow = true });
            g.Edges.Add(new MermaidEdge { From = "A", To = "C", Arrow = true });
            g.Edges.Add(new MermaidEdge { From = "B", To = "D", Arrow = true });
            g.Edges.Add(new MermaidEdge { From = "C", To = "D", Arrow = true });
            var r = MermaidLayout.Compute(g);
            Assert.Greater(NodeLayer(r, "D"), NodeLayer(r, "B"));
            Assert.Greater(NodeLayer(r, "D"), NodeLayer(r, "C"));
        }

        [Test]
        public void Cycle_DoesNotInfiniteLoop()
        {
            var g = new MermaidGraph { Dir = MermaidDir.TD };
            g.EnsureNode("A"); g.EnsureNode("B");
            g.Edges.Add(new MermaidEdge { From = "A", To = "B", Arrow = true });
            g.Edges.Add(new MermaidEdge { From = "B", To = "A", Arrow = true });
            LayoutResult r = null;
            Assert.DoesNotThrow(() => r = MermaidLayout.Compute(g));
            Assert.IsNotNull(r);
        }

        [Test]
        public void SelfLoop_DoesNotInfiniteLoop()
        {
            var g = new MermaidGraph { Dir = MermaidDir.TD };
            g.EnsureNode("A");
            g.Edges.Add(new MermaidEdge { From = "A", To = "A", Arrow = true });
            LayoutResult r = null;
            Assert.DoesNotThrow(() => r = MermaidLayout.Compute(g));
            Assert.IsNotNull(r);
        }

        [Test]
        public void LR_XIncreasesPerLayer()
        {
            var g = Chain(MermaidDir.LR, "A", "B", "C");
            var r = MermaidLayout.Compute(g);
            var rA = FindRect(r, "A");
            var rB = FindRect(r, "B");
            var rC = FindRect(r, "C");
            Assert.Less(rA.X, rB.X);
            Assert.Less(rB.X, rC.X);
        }

        [Test]
        public void BottomTop_YInverted()
        {
            // BT = bottom to top: root (no incoming) should have HIGHER Y than its child
            var g = Chain(MermaidDir.BT, "A", "B");
            var r = MermaidLayout.Compute(g);
            var rA = FindRect(r, "A");
            var rB = FindRect(r, "B");
            // A is the source → layer 0; in BT that means larger Y (bottom)
            Assert.Greater(rA.Y, rB.Y);
        }

        [Test]
        public void RL_XInverted()
        {
            var g = Chain(MermaidDir.RL, "A", "B");
            var r = MermaidLayout.Compute(g);
            var rA = FindRect(r, "A");
            var rB = FindRect(r, "B");
            // A is source (layer 0); in RL layer 0 has larger X (right)
            Assert.Greater(rA.X, rB.X);
        }

        [Test]
        public void EdgeEndpoints_OnNodeBorder()
        {
            var g = Chain(MermaidDir.TD, "A", "B");
            var r  = MermaidLayout.Compute(g);
            var el = r.Edges[0];
            var rA = FindRect(r, "A");
            var rB = FindRect(r, "B");
            // Start point must be on a border of A (within tolerance)
            bool onBorderA = IsOnBorder(el.X1, el.Y1, rA);
            bool onBorderB = IsOnBorder(el.X2, el.Y2, rB);
            Assert.IsTrue(onBorderA, $"Start not on border of A: ({el.X1},{el.Y1}) vs {rA}");
            Assert.IsTrue(onBorderB, $"End not on border of B: ({el.X2},{el.Y2}) vs {rB}");
        }

        [Test]
        public void Arrow_PropagatedToEdgeLine()
        {
            var g = Chain(MermaidDir.TD, "A", "B");
            var r = MermaidLayout.Compute(g);
            Assert.IsTrue(r.Edges[0].Arrow);
        }

        [Test]
        public void WidthHeight_BoundAllNodes()
        {
            var g = Chain(MermaidDir.TD, "A", "B", "C");
            var r = MermaidLayout.Compute(g);
            foreach (var nr in r.Nodes)
            {
                Assert.LessOrEqual(nr.X + nr.W, r.Width  + 1f);
                Assert.LessOrEqual(nr.Y + nr.H, r.Height + 1f);
            }
        }

        // ── BUG 2: per-node sizing ───────────────────────────────────────────

        [Test]
        public void MeasureNode_SingleLine_WidthProportionalToLength()
        {
            var (w1, _) = MermaidLayout.MeasureNode("Hi");
            var (w2, _) = MermaidLayout.MeasureNode("A much longer label text");
            Assert.Greater(w2, w1);
        }

        [Test]
        public void MeasureNode_MultiLine_HeightGrows()
        {
            var (_, h1) = MermaidLayout.MeasureNode("One");
            var (_, h2) = MermaidLayout.MeasureNode("One\nTwo\nThree");
            Assert.Greater(h2, h1);
        }

        [Test]
        public void MeasureNode_ClampedToMax()
        {
            var (w, h) = MermaidLayout.MeasureNode(
                new string('X', 200) + "\n" + new string('Y', 200) +
                "\n1\n2\n3\n4\n5\n6\n7\n8\n9\n10");
            Assert.LessOrEqual(w, 280f);
            Assert.LessOrEqual(h, 120f);
        }

        [Test]
        public void VariableWidth_NodesDoNotOverlap()
        {
            var g = new MermaidGraph { Dir = MermaidDir.TD };
            g.GetOrAdd("A", "Root", NodeShape.Rect);
            g.GetOrAdd("B", "Short", NodeShape.Rect);
            g.GetOrAdd("C", "This is a very long label that should be wider", NodeShape.Rect);
            g.Edges.Add(new MermaidEdge { From = "A", To = "B", Arrow = true });
            g.Edges.Add(new MermaidEdge { From = "A", To = "C", Arrow = true });
            var r = MermaidLayout.Compute(g);
            var rB = FindRect(r, "B");
            var rC = FindRect(r, "C");
            // B and C are in the same layer (siblings) — must not overlap
            bool overlaps = rB.X < rC.X + rC.W && rC.X < rB.X + rB.W
                         && rB.Y < rC.Y + rC.H && rC.Y < rB.Y + rB.H;
            Assert.IsFalse(overlaps, $"B={rB} and C={rC} overlap");
        }

        // ── helpers ─────────────────────────────────────────────────────────

        private static float NodeLayer(LayoutResult r, string id)
        {
            // For TD: layer = Y / (nodeH + gap). For others use row index via Y ordering.
            // Simpler: just return Y for TD (monotone per layer).
            return FindRect(r, id).Y;
        }

        private static NodeRect FindRect(LayoutResult r, string id)
        {
            foreach (var nr in r.Nodes)
                if (nr.Id == id) return nr;
            throw new System.Exception("Node not found: " + id);
        }

        private static bool IsOnBorder(float px, float py, NodeRect rect, float tol = 2f)
        {
            bool onLeft   = Math.Abs(px - rect.X) < tol;
            bool onRight  = Math.Abs(px - (rect.X + rect.W)) < tol;
            bool onTop    = Math.Abs(py - rect.Y) < tol;
            bool onBottom = Math.Abs(py - (rect.Y + rect.H)) < tol;
            bool insideX  = px >= rect.X - tol && px <= rect.X + rect.W + tol;
            bool insideY  = py >= rect.Y - tol && py <= rect.Y + rect.H + tol;
            return (onLeft || onRight || onTop || onBottom) && insideX && insideY;
        }
    }
}
