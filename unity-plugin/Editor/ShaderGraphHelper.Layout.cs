using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static partial class ShaderGraphHelper
    {
        internal struct NodeLayoutInfo
        {
            public string id;
            public string type;
            public string name;
            public float x, y, w, h;
        }

        internal struct EdgeInfo
        {
            public string outputNodeId;
            public int outputSlot;
            public string inputNodeId;
            public int inputSlot;
        }

        // Testability seam — override file I/O in unit tests
        internal static Func<string, string> _layoutReadOverride;
        internal static Action<string, string> _layoutWriteOverride;

        internal static List<NodeLayoutInfo> ParseNodePositions(string content)
        {
            var result = new List<NodeLayoutInfo>();
            foreach (var block in SplitBlocks(content))
            {
                var id = JsonHelper.ExtractString(block, "m_ObjectId");
                if (id == null) continue;

                var drawState = JsonHelper.ExtractObject(block, "m_DrawState");
                if (drawState == "{}") continue;

                var posObj = JsonHelper.ExtractObject(drawState, "m_Position");
                if (posObj == "{}") continue;

                var w = JsonHelper.ExtractFloat(posObj, "width");
                var h = JsonHelper.ExtractFloat(posObj, "height");
                if (w == 0f && h == 0f) continue; // skip BlockNodes

                result.Add(new NodeLayoutInfo
                {
                    id   = id,
                    type = ShortType(JsonHelper.ExtractString(block, "m_Type") ?? "Unknown"),
                    name = JsonHelper.ExtractString(block, "m_Name") ?? "",
                    x    = JsonHelper.ExtractFloat(posObj, "x"),
                    y    = JsonHelper.ExtractFloat(posObj, "y"),
                    w    = w,
                    h    = h
                });
            }
            return result;
        }

        internal static List<EdgeInfo> ParseEdges(string content)
        {
            var result = new List<EdgeInfo>();
            var blocks = SplitBlocks(content);
            var root = FindRoot(blocks);
            if (root == null) return result;

            var arr = JsonHelper.ExtractArray(root, "m_Edges");
            if (arr == "[]") return result;

            // Each edge is a top-level {…} in the array
            foreach (var edgeBlock in SplitBlocks(arr))
            {
                // Slot format: {"m_Node": {"m_Id": "…"}, "m_SlotId": N}
                // Use ExtractObject chain to reach the nested m_Id correctly
                var outSlot = JsonHelper.ExtractObject(edgeBlock, "m_OutputSlot");
                var inSlot  = JsonHelper.ExtractObject(edgeBlock, "m_InputSlot");
                if (outSlot == "{}" || inSlot == "{}") continue;

                var outNodeId = JsonHelper.ExtractString(JsonHelper.ExtractObject(outSlot, "m_Node"), "m_Id");
                var inNodeId  = JsonHelper.ExtractString(JsonHelper.ExtractObject(inSlot,  "m_Node"), "m_Id");
                if (outNodeId == null || inNodeId == null) continue;

                int.TryParse(JsonHelper.ExtractString(outSlot, "m_SlotId"), out int oSlot);
                int.TryParse(JsonHelper.ExtractString(inSlot,  "m_SlotId"), out int iSlot);
                result.Add(new EdgeInfo
                {
                    outputNodeId = outNodeId, outputSlot = oSlot,
                    inputNodeId  = inNodeId,  inputSlot  = iSlot
                });
            }
            return result;
        }

        internal static int CountOverlaps(List<NodeLayoutInfo> nodes)
        {
            int count = 0;
            for (int i = 0; i < nodes.Count; i++)
                for (int j = i + 1; j < nodes.Count; j++)
                {
                    var a = nodes[i]; var b = nodes[j];
                    if (a.x < b.x + b.w && a.x + a.w > b.x &&
                        a.y < b.y + b.h && a.y + a.h > b.y)
                        count++;
                }
            return count;
        }

        internal static List<NodeLayoutInfo> ComputeLayout(
            List<NodeLayoutInfo> nodes, List<EdgeInfo> edges,
            float hGap = 80f, float vGap = 50f)
        {
            if (nodes.Count == 0) return nodes;

            var inEdges  = new Dictionary<string, List<string>>();
            var outEdges = new Dictionary<string, List<string>>();
            foreach (var n in nodes) { inEdges[n.id] = new List<string>(); outEdges[n.id] = new List<string>(); }
            foreach (var e in edges)
            {
                if (!inEdges.ContainsKey(e.inputNodeId) || !outEdges.ContainsKey(e.outputNodeId)) continue;
                inEdges[e.inputNodeId].Add(e.outputNodeId);
                outEdges[e.outputNodeId].Add(e.inputNodeId);
            }

            // Layer = longest path from a source node
            var layers = new Dictionary<string, int>();
            foreach (var n in nodes) layers[n.id] = -1;

            var queue = new Queue<string>();
            foreach (var n in nodes)
                if (inEdges[n.id].Count == 0) { layers[n.id] = 0; queue.Enqueue(n.id); }
            if (queue.Count == 0) // all-source fallback (cycle or disconnected)
                foreach (var n in nodes) { layers[n.id] = 0; queue.Enqueue(n.id); }

            int maxIter = nodes.Count * nodes.Count; // N² — generous cap for any DAG
            int iter = 0;
            while (queue.Count > 0 && ++iter < maxIter)
            {
                var id = queue.Dequeue();
                foreach (var child in outEdges[id])
                {
                    var nl = layers[id] + 1;
                    if (nl > layers[child]) { layers[child] = nl; queue.Enqueue(child); }
                }
            }
            foreach (var n in nodes) if (layers[n.id] < 0) layers[n.id] = 0;

            var byLayer = new Dictionary<int, List<NodeLayoutInfo>>();
            foreach (var n in nodes)
            {
                var l = layers[n.id];
                if (!byLayer.ContainsKey(l)) byLayer[l] = new List<NodeLayoutInfo>();
                byLayer[l].Add(n);
            }
            foreach (var l in byLayer.Values) l.Sort((a, b) => a.y.CompareTo(b.y));

            // Column x = cumulative (max node width + hGap)
            var sortedLayers = new List<int>(byLayer.Keys);
            sortedLayers.Sort();
            var colX = new Dictionary<int, float>();
            float curX = 0f;
            foreach (var l in sortedLayers)
            {
                colX[l] = curX;
                float maxW = 0f;
                foreach (var n in byLayer[l]) maxW = Math.Max(maxW, n.w);
                curX += maxW + hGap;
            }

            var result = new List<NodeLayoutInfo>();
            foreach (var l in sortedLayers)
            {
                float curY = 0f;
                foreach (var n in byLayer[l])
                {
                    var u = n; u.x = colX[l]; u.y = curY;
                    result.Add(u);
                    curY += n.h + vGap;
                }
            }
            return result;
        }

        internal static string ApplyLayout(string content, List<NodeLayoutInfo> newPositions)
        {
            var byId = BuildIdMap(SplitBlocks(content));
            foreach (var node in newPositions)
            {
                if (!byId.TryGetValue(node.id, out var block)) continue;
                var drawState = JsonHelper.ExtractObject(block, "m_DrawState");
                if (drawState == "{}") continue;
                var posObj = JsonHelper.ExtractObject(drawState, "m_Position");
                if (posObj == "{}") continue;

                var newPos = BuildPositionRect(node.x, node.y, node.w, node.h);
                var newDrawState = drawState.Replace(posObj, newPos);
                var newBlock = block.Replace(drawState, newDrawState);
                content = content.Replace(block, newBlock);
                byId[node.id] = newBlock;
            }
            return content;
        }

        // No file I/O — used internally so SetLayout/AutoLayout don't re-read
        internal static string GetLayoutFromContent(string content)
        {
            var nodes = ParseNodePositions(content);
            var sb = new StringBuilder();
            sb.AppendLine($"layout: {nodes.Count} nodes");
            foreach (var n in nodes)
                sb.AppendLine($"[{n.id}] {n.type} \"{n.name}\" @ {LF0(n.x)},{LF0(n.y)} {LF0(n.w)}x{LF0(n.h)}");
            return sb.ToString().TrimEnd();
        }

        public static string GetLayout(string path)
        {
            ValidateSGPath(path);
            var content = _layoutReadOverride != null ? _layoutReadOverride(path) : File.ReadAllText(path);
            return GetLayoutFromContent(content);
        }

        public static string SetLayout(string path, string layout)
        {
            ValidateSGPath(path);
            if (string.IsNullOrEmpty(layout)) throw new ArgumentException("layout text is required");
            var updates = ParseLayoutText(layout);
            var content = _layoutReadOverride != null ? _layoutReadOverride(path) : File.ReadAllText(path);
            content = ApplyLayout(content, updates);
            SGWriteAndImport(path, content);
            return GetLayoutFromContent(content);
        }

        public static string AutoLayout(string path, float hGap = 80f, float vGap = 50f)
        {
            ValidateSGPath(path);
            if (IsShaderGraphWindowOpen())
                return "err: close the Shader Graph editor window before auto-layout";
            var content = _layoutReadOverride != null ? _layoutReadOverride(path) : File.ReadAllText(path);
            var nodes = ParseNodePositions(content);
            var edges = ParseEdges(content);
            var before = CountOverlaps(nodes);
            var laid = ComputeLayout(nodes, edges, hGap, vGap);
            content = ApplyLayout(content, laid);
            SGWriteAndImport(path, content);
            var after = CountOverlaps(laid);
            return $"auto_layout: {laid.Count} nodes repositioned, {before - after} overlaps resolved\nbefore: {before} overlaps, after: {after} overlaps\n{GetLayoutFromContent(content)}";
        }

        static string BuildPositionRect(float x, float y, float w, float h) =>
            $"{{\"serializedVersion\": \"2\", \"x\": {LF1(x)}, \"y\": {LF1(y)}, \"width\": {LF1(w)}, \"height\": {LF1(h)}}}";

        static string LF0(float v) => v.ToString("F0", System.Globalization.CultureInfo.InvariantCulture);
        static string LF1(float v) => v.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

        static List<NodeLayoutInfo> ParseLayoutText(string layout)
        {
            var result = new List<NodeLayoutInfo>();
            foreach (var line in layout.Split('\n'))
            {
                var t = line.Trim();
                if (!t.StartsWith("[")) continue;
                var idEnd = t.IndexOf(']');
                if (idEnd < 0) continue;
                var id = t.Substring(1, idEnd - 1);
                var at = t.IndexOf('@', idEnd);
                // Accept both "[id] Type "Name" @ x,y WxH" (full) and "[id] x,y WxH" (short)
                var posText = at >= 0
                    ? t.Substring(at + 1).Trim()
                    : t.Substring(idEnd + 1).Trim();
                var parts = posText.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 1) continue;
                var xy = parts[0].Split(',');
                if (xy.Length < 2) continue;
                if (!TryParseF(xy[0], out float x) || !TryParseF(xy[1], out float y)) continue;
                float w = 200f, h = 100f;
                if (parts.Length > 1)
                {
                    var wh = parts[1].Split('x');
                    if (wh.Length == 2) { TryParseF(wh[0], out w); TryParseF(wh[1], out h); }
                }
                result.Add(new NodeLayoutInfo { id = id, x = x, y = y, w = w, h = h });
            }
            return result;
        }

        static bool TryParseF(string s, out float v) =>
            float.TryParse(s, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out v);

        static void ValidateSGPath(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".shadergraph"))
                throw new ArgumentException($"Path must end with .shadergraph: {path}");
            if (_layoutReadOverride == null && !File.Exists(path))
                throw new FileNotFoundException($"ShaderGraph not found: {path}");
        }

        static void SGWriteAndImport(string path, string content)
        {
            if (_layoutWriteOverride != null) { _layoutWriteOverride(path, content); return; }
            File.WriteAllText(path, content);
            try { _importAsset(path, ImportAssetOptions.Default); }
            catch (Exception ex) { Debug.LogWarning($"ShaderGraph import warning for '{path}': {ex.Message}"); }
        }

        static bool IsShaderGraphWindowOpen()
        {
            try
            {
                var t = Type.GetType(
                    "UnityEditor.ShaderGraph.Drawing.MaterialGraphEditWindow, Unity.ShaderGraph.Editor");
                if (t == null) return false;
                var found = Resources.FindObjectsOfTypeAll(t);
                return found != null && found.Length > 0;
            }
            catch { return false; }
        }
    }
}
