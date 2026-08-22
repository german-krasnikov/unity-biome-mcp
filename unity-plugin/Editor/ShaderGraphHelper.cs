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
        public static string Get(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".shadergraph"))
                throw new ArgumentException($"Path must end with .shadergraph: {path}");
            if (!File.Exists(path))
                throw new FileNotFoundException($"ShaderGraph not found: {path}");

            var sb = new StringBuilder();
            sb.AppendLine($"ShaderGraph: {path}");

            // Use Unity API to get compiled shader info
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (shader != null)
            {
                sb.Append("compiled: ").AppendLine(shader.name);
                sb.Append("passes: ").AppendLine(shader.passCount.ToString());
                sb.Append("errors: ").AppendLine(ShaderUtil.ShaderHasError(shader) ? "yes" : "none");
            }

            // Parse raw file for graph structure — no public Unity API for nodes/edges
            var content = File.ReadAllText(path);
            var blocks = SplitBlocks(content);
            var byId = BuildIdMap(blocks);
            var root = FindRoot(blocks);
            if (root != null)
                AppendGraphStructure(sb, root, byId);

            return sb.ToString().TrimEnd();
        }

        public static string Create(string path, string preset)
        {
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".shadergraph"))
                throw new ArgumentException($"Path must end with .shadergraph: {path}");

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var content = BuildTemplate(preset);
            File.WriteAllText(path, content, Encoding.UTF8);
            try { _importAsset(path, ImportAssetOptions.Default); }
            catch (Exception ex) { Debug.LogWarning($"ShaderGraph import warning for '{path}': {ex.Message}"); }

            return Get(path);
        }

        static string BuildTemplate(string preset)
        {
            if (preset != "unlit_graph" && preset != "lit_graph")
                throw new ArgumentException($"Unknown preset '{preset}'. Available: unlit_graph, lit_graph");

            bool isLit = preset == "lit_graph";
            var graphId    = Guid.NewGuid().ToString("N");
            var posNodeId  = Guid.NewGuid().ToString("N");
            var normNodeId = Guid.NewGuid().ToString("N");
            var colNodeId  = Guid.NewGuid().ToString("N");
            var targetId   = Guid.NewGuid().ToString("N");
            var subTargetId = Guid.NewGuid().ToString("N");

            // sub-target type differs between unlit and lit
            var subTargetType = isLit
                ? "UnityEditor.Rendering.Universal.ShaderGraph.UniversalLitSubTarget"
                : "UnityEditor.Rendering.Universal.ShaderGraph.UniversalUnlitSubTarget";
            var surfaceBlock = isLit ? "SurfaceDescription.BaseColor" : "SurfaceDescription.BaseColor";
            var path = isLit ? "Lit" : "Unlit";

            return $@"{{
    ""m_SGVersion"": 3,
    ""m_Type"": ""UnityEditor.ShaderGraph.GraphData"",
    ""m_ObjectId"": ""{graphId}"",
    ""m_Properties"": [],
    ""m_Keywords"": [],
    ""m_Dropdowns"": [],
    ""m_CategoryData"": [],
    ""m_Nodes"": [
        {{ ""m_Id"": ""{posNodeId}"" }},
        {{ ""m_Id"": ""{normNodeId}"" }},
        {{ ""m_Id"": ""{colNodeId}"" }}
    ],
    ""m_GroupDatas"": [],
    ""m_StickyNoteDatas"": [],
    ""m_Edges"": [],
    ""m_VertexContext"": {{
        ""m_Position"": {{ ""x"": 0.0, ""y"": 0.0 }},
        ""m_Blocks"": [
            {{ ""m_Id"": ""{posNodeId}"" }},
            {{ ""m_Id"": ""{normNodeId}"" }}
        ]
    }},
    ""m_FragmentContext"": {{
        ""m_Position"": {{ ""x"": 0.0, ""y"": 200.0 }},
        ""m_Blocks"": [
            {{ ""m_Id"": ""{colNodeId}"" }}
        ]
    }},
    ""m_PreviewData"": {{ ""serializedMesh"": {{ ""m_SerializedMesh"": ""{{\""mesh\"":{{\""instanceID\"":0}}}}"", ""m_Guid"": """" }}, ""preventRotation"": false }},
    ""m_Path"": ""{path}"",
    ""m_GraphPrecision"": 1,
    ""m_PreviewMode"": 2,
    ""m_OutputNode"": {{ ""m_Id"": """" }},
    ""m_SubDatas"": [],
    ""m_ActiveTargets"": [{{ ""m_Id"": ""{targetId}"" }}]
}}

{{
    ""m_SGVersion"": 1,
    ""m_Type"": ""UnityEditor.Rendering.Universal.ShaderGraph.UniversalTarget"",
    ""m_ObjectId"": ""{targetId}"",
    ""m_Datas"": [],
    ""m_ActiveSubTarget"": {{ ""m_Id"": ""{subTargetId}"" }},
    ""m_AllowMaterialOverride"": false,
    ""m_SurfaceType"": 0,
    ""m_ZTestMode"": 4,
    ""m_ZWriteControl"": 0,
    ""m_AlphaMode"": 0,
    ""m_RenderFace"": 2,
    ""m_AlphaClip"": false,
    ""m_CastShadows"": true,
    ""m_ReceiveShadows"": true,
    ""m_CustomEditorGUI"": """",
    ""m_SupportVFX"": false
}}

{{
    ""m_SGVersion"": 0,
    ""m_Type"": ""{subTargetType}"",
    ""m_ObjectId"": ""{subTargetId}""
}}

{{
    ""m_SGVersion"": 0,
    ""m_Type"": ""UnityEditor.ShaderGraph.BlockNode"",
    ""m_ObjectId"": ""{posNodeId}"",
    ""m_Group"": {{ ""m_Id"": """" }},
    ""m_Name"": ""VertexDescription.Position"",
    ""m_DrawState"": {{ ""m_Expanded"": true, ""m_Position"": {{ ""serializedVersion"": ""2"", ""x"": 0.0, ""y"": 0.0, ""width"": 0.0, ""height"": 0.0 }} }},
    ""m_Slots"": [],
    ""synonyms"": [],
    ""m_Precision"": 0,
    ""m_PreviewExpanded"": true,
    ""m_DismissedVersion"": 0,
    ""m_PreviewMode"": 0,
    ""m_CustomColors"": {{ ""m_SerializableColors"": [] }},
    ""m_SerializedDescriptor"": ""VertexDescription.Position""
}}

{{
    ""m_SGVersion"": 0,
    ""m_Type"": ""UnityEditor.ShaderGraph.BlockNode"",
    ""m_ObjectId"": ""{normNodeId}"",
    ""m_Group"": {{ ""m_Id"": """" }},
    ""m_Name"": ""VertexDescription.Normal"",
    ""m_DrawState"": {{ ""m_Expanded"": true, ""m_Position"": {{ ""serializedVersion"": ""2"", ""x"": 0.0, ""y"": 0.0, ""width"": 0.0, ""height"": 0.0 }} }},
    ""m_Slots"": [],
    ""synonyms"": [],
    ""m_Precision"": 0,
    ""m_PreviewExpanded"": true,
    ""m_DismissedVersion"": 0,
    ""m_PreviewMode"": 0,
    ""m_CustomColors"": {{ ""m_SerializableColors"": [] }},
    ""m_SerializedDescriptor"": ""VertexDescription.Normal""
}}

{{
    ""m_SGVersion"": 0,
    ""m_Type"": ""UnityEditor.ShaderGraph.BlockNode"",
    ""m_ObjectId"": ""{colNodeId}"",
    ""m_Group"": {{ ""m_Id"": """" }},
    ""m_Name"": ""{surfaceBlock}"",
    ""m_DrawState"": {{ ""m_Expanded"": true, ""m_Position"": {{ ""serializedVersion"": ""2"", ""x"": 0.0, ""y"": 0.0, ""width"": 0.0, ""height"": 0.0 }} }},
    ""m_Slots"": [],
    ""synonyms"": [],
    ""m_Precision"": 0,
    ""m_PreviewExpanded"": true,
    ""m_DismissedVersion"": 0,
    ""m_PreviewMode"": 0,
    ""m_CustomColors"": {{ ""m_SerializableColors"": [] }},
    ""m_SerializedDescriptor"": ""SurfaceDescription.BaseColor""
}}
";
        }

        static void AppendGraphStructure(StringBuilder sb, string root, Dictionary<string, string> byId)
        {
            var nodeRefs = ExtractIdArray(root, "m_Nodes");
            sb.AppendLine($"nodes: {nodeRefs.Count}");
            foreach (var id in nodeRefs)
            {
                if (!byId.TryGetValue(id, out var blk)) { sb.AppendLine($"  [{id}] (missing)"); continue; }
                var type = ShortType(JsonHelper.ExtractString(blk, "m_Type") ?? "Unknown");
                var name = JsonHelper.ExtractString(blk, "m_Name") ?? "";
                var linked = JsonHelper.ExtractString(blk, "m_PropertyGuidSerialized");
                if (linked != null && byId.TryGetValue(linked, out var propBlk))
                    sb.AppendLine($"  [{id}] {type} \"{name}\" -> {JsonHelper.ExtractString(propBlk, "m_Name")}");
                else
                    sb.AppendLine($"  [{id}] {type} \"{name}\"");
            }

            var edges = ExtractEdges(root);
            sb.AppendLine($"edges: {edges.Count}");
            foreach (var e in edges) sb.AppendLine(e);

            var propRefs = ExtractIdArray(root, "m_Properties");
            sb.AppendLine($"properties: {propRefs.Count}");
            foreach (var id in propRefs)
            {
                if (!byId.TryGetValue(id, out var blk)) { sb.AppendLine($"  (missing {id})"); continue; }
                var pname = JsonHelper.ExtractString(blk, "m_Name") ?? id;
                var ptype = ShortType(JsonHelper.ExtractString(blk, "m_Type") ?? "");
                var refname = JsonHelper.ExtractString(blk, "m_DefaultReferenceName") ?? "";
                sb.AppendLine($"  {pname}: {ptype} ({refname})");
            }
        }

        // Splits content into top-level JSON objects {…}, optionally skipping string literals
        static List<string> SplitBlocks(string content, bool skipStrings = true)
        {
            var blocks = new List<string>();
            int depth = 0, start = -1;
            bool inStr = false;
            for (int i = 0; i < content.Length; i++)
            {
                char c = content[i];
                if (skipStrings && c == '"' && (i == 0 || content[i - 1] != '\\')) { inStr = !inStr; continue; }
                if (inStr) continue;
                if (c == '{') { if (depth == 0) start = i; depth++; }
                else if (c == '}') { depth--; if (depth == 0 && start >= 0) { blocks.Add(content.Substring(start, i - start + 1)); start = -1; } }
            }
            return blocks;
        }

        static Dictionary<string, string> BuildIdMap(List<string> blocks)
        {
            var map = new Dictionary<string, string>();
            foreach (var b in blocks) { var id = JsonHelper.ExtractString(b, "m_ObjectId"); if (id != null) map[id] = b; }
            return map;
        }

        static string FindRoot(List<string> blocks)
        {
            foreach (var b in blocks) if ((JsonHelper.ExtractString(b, "m_Type") ?? "").Contains("GraphData")) return b;
            return null;
        }

        static List<string> ExtractIdArray(string json, string key)
        {
            var ids = new List<string>();
            var arr = JsonHelper.ExtractArray(json, key);
            if (arr == "[]") return ids;
            for (int i = 0; i < arr.Length;)
            {
                var s = arr.IndexOf("\"m_Id\"", i); if (s < 0) break;
                var id = JsonHelper.ExtractString(arr.Substring(s), "m_Id");
                if (id != null) ids.Add(id);
                i = s + 6;
            }
            return ids;
        }

        static List<string> ExtractEdges(string root)
        {
            var edges = new List<string>();
            var arr = JsonHelper.ExtractArray(root, "m_Edges");
            if (arr == "[]") return edges;
            for (int i = 0; i < arr.Length;)
            {
                var outS = arr.IndexOf("\"m_OutputSlot\"", i);
                var inS  = arr.IndexOf("\"m_InputSlot\"", i);
                if (outS < 0 || inS < 0) break;
                var next = arr.IndexOf("\"m_OutputSlot\"", outS + 1);
                var o = ExtractSlotRef(arr, outS); var n = ExtractSlotRef(arr, inS);
                if (o != null && n != null) edges.Add($"  [{o.Item1}]:{o.Item2} -> [{n.Item1}]:{n.Item2}");
                i = next > 0 ? next : arr.Length;
            }
            return edges;
        }

        static Tuple<string, string> ExtractSlotRef(string json, int slotStart)
        {
            var brace = json.IndexOf('{', slotStart + 14); if (brace < 0) return null;
            int depth = 0, end = brace;
            for (; end < json.Length; end++) { if (json[end] == '{') depth++; else if (json[end] == '}') { depth--; if (depth == 0) break; } }
            var obj = json.Substring(brace, end - brace + 1);
            var nodeId = JsonHelper.ExtractString(obj, "m_Id");
            var slotId = JsonHelper.ExtractString(obj, "m_SlotId");
            return (nodeId != null && slotId != null) ? Tuple.Create(nodeId, slotId) : null;
        }

        static string ShortType(string t) { var d = t.LastIndexOf('.'); return d >= 0 ? t.Substring(d + 1) : t; }
    }
}
