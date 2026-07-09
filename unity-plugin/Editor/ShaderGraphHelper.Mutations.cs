using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static partial class ShaderGraphHelper
    {
        public static string ManageNode(string path, string nodeType, string nodeId, string action)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                throw new FileNotFoundException($"ShaderGraph not found: {path}");
            var content = File.ReadAllText(path);
            var blocks = SplitBlocks(content);
            var root = FindRoot(blocks);
            if (root == null) throw new InvalidOperationException("No GraphData block found");
            content = action == "remove"
                ? RemoveNode(content, blocks, root, nodeId)
                : AddNode(content, root, nodeType);
            File.WriteAllText(path, content);
            try { AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate); }
            catch (Exception ex) { Debug.LogWarning($"ShaderGraph import failed for '{path}': {ex.Message}"); }
            return Get(path);
        }

        public static string ManageEdge(string path, string outputNode, int outputSlot, string inputNode, int inputSlot, string action)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                throw new FileNotFoundException($"ShaderGraph not found: {path}");
            var content = File.ReadAllText(path);
            var blocks = SplitBlocks(content);
            var root = FindRoot(blocks);
            if (root == null) throw new InvalidOperationException("No GraphData block found");
            content = action == "remove"
                ? RemoveEdge(content, root, outputNode, outputSlot, inputNode, inputSlot)
                : AddEdge(content, root, outputNode, outputSlot, inputNode, inputSlot);
            File.WriteAllText(path, content);
            try { AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate); }
            catch (Exception ex) { Debug.LogWarning($"ShaderGraph import failed for '{path}': {ex.Message}"); }
            return Get(path);
        }

        static string AddNode(string content, string root, string nodeType)
        {
            var newId = Guid.NewGuid().ToString("N");
            var nodeBlock = $"{{\n  \"m_SGVersion\": 0,\n  \"m_Type\": \"UnityEditor.ShaderGraph.{nodeType}\",\n  \"m_ObjectId\": \"{newId}\",\n  \"m_Group\": {{\"m_Id\": \"\"}},\n  \"m_Name\": \"{nodeType}\",\n  \"m_DrawState\": {{\"m_Expanded\": true, \"m_Position\": {{\"serializedVersion\": \"2\", \"x\": 0, \"y\": 0, \"width\": 200, \"height\": 100}}}},\n  \"m_Slots\": [],\n  \"synonyms\": [],\n  \"m_Precision\": 0,\n  \"m_PreviewExpanded\": false,\n  \"m_DismissedVersion\": 0,\n  \"m_PreviewMode\": 0,\n  \"m_CustomColors\": {{\"m_SerializableColors\": []}}\n}}";
            var nodeRef = $"{{\"m_Id\": \"{newId}\"}}";
            content = InsertIntoArray(content, root, "m_Nodes", nodeRef);
            content = content.TrimEnd() + "\n\n" + nodeBlock + "\n";
            return content;
        }

        static string RemoveNode(string content, List<string> blocks, string root, string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) throw new ArgumentException("nodeId required for remove");

            // 1. Remove the node block from file
            foreach (var b in blocks)
                if (JsonHelper.ExtractString(b, "m_ObjectId") == nodeId) { content = content.Replace(b, ""); break; }

            // 2. Remove node ref from m_Nodes + edges via line-based filtering
            // More reliable than SplitBlocks+Replace chains which break on whitespace drift
            var lines = content.Split('\n');
            var result = new List<string>();
            bool skipBlock = false;
            int skipDepth = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].Trim();

                if (!skipBlock && trimmed.Contains(nodeId))
                {
                    int braceCount = 0;
                    foreach (char c in trimmed) { if (c == '{') braceCount++; if (c == '}') braceCount--; }

                    if (braceCount == 0)
                    {
                        if (result.Count > 0 && result[result.Count - 1].TrimEnd().EndsWith(","))
                            result[result.Count - 1] = result[result.Count - 1].TrimEnd().TrimEnd(',');
                        continue;
                    }
                    else if (braceCount > 0)
                    {
                        skipBlock = true;
                        skipDepth = braceCount;
                        if (result.Count > 0 && result[result.Count - 1].TrimEnd().EndsWith(","))
                            result[result.Count - 1] = result[result.Count - 1].TrimEnd().TrimEnd(',');
                        continue;
                    }
                }

                if (skipBlock)
                {
                    foreach (char c in trimmed) { if (c == '{') skipDepth++; if (c == '}') skipDepth--; }
                    if (skipDepth <= 0) skipBlock = false;
                    continue;
                }

                result.Add(lines[i]);
            }

            content = string.Join("\n", result);
            while (content.Contains("\n\n\n")) content = content.Replace("\n\n\n", "\n\n");
            return content;
        }

        static string AddEdge(string content, string root, string outputNode, int outputSlot, string inputNode, int inputSlot)
        {
            var edgeJson = $"{{\"m_OutputSlot\": {{\"m_Node\": {{\"m_Id\": \"{outputNode}\"}}, \"m_SlotId\": {outputSlot}}}, \"m_InputSlot\": {{\"m_Node\": {{\"m_Id\": \"{inputNode}\"}}, \"m_SlotId\": {inputSlot}}}}}";
            return InsertIntoArray(content, root, "m_Edges", edgeJson);
        }

        static string RemoveEdge(string content, string root, string outputNode, int outputSlot, string inputNode, int inputSlot)
        {
            var edgesArr = JsonHelper.ExtractArray(root, "m_Edges");
            if (edgesArr == "[]") return content;
            var edgeBlocks = SplitBlocks(edgesArr);
            var filtered = new List<string>();
            foreach (var eb in edgeBlocks)
            {
                var outSlot = ExtractSlotRef(eb, eb.IndexOf("\"m_OutputSlot\""));
                var inSlot  = ExtractSlotRef(eb, eb.IndexOf("\"m_InputSlot\""));
                if (outSlot != null && inSlot != null
                    && outSlot.Item1 == outputNode && outSlot.Item2 == outputSlot.ToString()
                    && inSlot.Item1  == inputNode  && inSlot.Item2  == inputSlot.ToString()) continue;
                filtered.Add(eb);
            }
            var newArr = filtered.Count > 0 ? "[\n    " + string.Join(",\n    ", filtered) + "\n  ]" : "[]";
            return content.Replace(edgesArr, newArr);
        }

        static string InsertIntoArray(string content, string root, string arrayKey, string item)
        {
            var arr = JsonHelper.ExtractArray(root, arrayKey);
            var last = arr.LastIndexOf(']');
            var inner = arr.Substring(0, last).TrimEnd();
            var updated = (inner.EndsWith("[") ? inner : inner + ",\n    ") + item + "\n  ]";
            var newRoot = root.Replace(arr, updated);
            return content.Replace(root, newRoot);
        }

        public static string AddProperty(string path, string name, string type, string defaultValue, string referenceName)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                throw new FileNotFoundException($"ShaderGraph not found: {path}");
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("name is required");

            var content = File.ReadAllText(path);
            var blocks = SplitBlocks(content);
            var root = FindRoot(blocks);
            if (root == null) throw new InvalidOperationException("No GraphData block found");

            // Guard: duplicate name check among existing properties
            var byId = BuildIdMap(blocks);
            foreach (var pid in ExtractIdArray(root, "m_Properties"))
                if (byId.TryGetValue(pid, out var pb) && JsonHelper.ExtractString(pb, "m_Name") == name)
                    throw new ArgumentException($"Property already exists: {name}");

            var typeStr = type ?? "Float";
            var newId = Guid.NewGuid().ToString("N");
            var guidId = Guid.NewGuid().ToString("N");
            var refName = string.IsNullOrEmpty(referenceName) ? "_" + name : referenceName;
            name = name.Replace("\"", "\\\"");
            refName = refName.Replace("\"", "\\\"");
            var valueField = BuildValueField(typeStr, defaultValue);
            var propBlock = $"{{\n  \"m_SGVersion\": 1,\n  \"m_Type\": \"{MapPropertyType(typeStr)}\",\n  \"m_ObjectId\": \"{newId}\",\n  \"m_Guid\": {{\"m_GuidSerialized\": \"{guidId}\"}},\n  \"m_Name\": \"{name}\",\n  \"m_DefaultRefNameVersion\": 0,\n  \"m_RefNameGeneratedByDisplayName\": \"\",\n  \"m_DefaultReferenceName\": \"{refName}\",\n  \"m_OverrideReferenceName\": \"\",\n  \"m_GeneratePropertyBlock\": true,\n  \"m_UseCustomSlotLabel\": false,\n  \"m_CustomSlotLabel\": \"\",\n  \"m_DismissedVersion\": 0,\n  \"m_Precision\": 0,\n  \"overrideHLSLDeclaration\": false,\n  \"hlslDeclarationOverride\": 0,\n  \"m_Hidden\": false{valueField}\n}}";

            content = InsertIntoArray(content, root, "m_Properties", $"{{\"m_Id\": \"{newId}\"}}");
            content = content.TrimEnd() + "\n\n" + propBlock + "\n";
            File.WriteAllText(path, content);
            try { AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate); }
            catch (Exception ex) { Debug.LogWarning($"ShaderGraph import failed for '{path}': {ex.Message}"); }
            return Get(path);
        }

        public static string RemoveProperty(string path, string name)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                throw new FileNotFoundException($"ShaderGraph not found: {path}");

            var content = File.ReadAllText(path);
            var blocks = SplitBlocks(content);
            var root = FindRoot(blocks);
            if (root == null) throw new InvalidOperationException("No GraphData block found");

            var byId = BuildIdMap(blocks);
            string propId = null;
            foreach (var pid in ExtractIdArray(root, "m_Properties"))
                if (byId.TryGetValue(pid, out var pb) && JsonHelper.ExtractString(pb, "m_Name") == name)
                { propId = pid; break; }

            if (propId == null) throw new InvalidOperationException($"Property not found: {name}");

            content = RemoveNode(content, blocks, root, propId);
            File.WriteAllText(path, content);
            try { AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate); }
            catch (Exception ex) { Debug.LogWarning($"ShaderGraph import failed for '{path}': {ex.Message}"); }
            return $"property removed: {name}";
        }

        public static string RenameProperty(string path, string oldName, string newName)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                throw new FileNotFoundException($"ShaderGraph not found: {path}");
            if (string.IsNullOrEmpty(oldName) || string.IsNullOrEmpty(newName))
                throw new ArgumentException("name and new_name are required");

            var content = File.ReadAllText(path);
            var blocks = SplitBlocks(content);
            var root = FindRoot(blocks);
            if (root == null) throw new InvalidOperationException("No GraphData block found");

            var byId = BuildIdMap(blocks);
            string propBlock = null;
            foreach (var pid in ExtractIdArray(root, "m_Properties"))
                if (byId.TryGetValue(pid, out var pb) && JsonHelper.ExtractString(pb, "m_Name") == oldName)
                { propBlock = pb; break; }

            if (propBlock == null) throw new InvalidOperationException($"Property not found: {oldName}");

            oldName = oldName.Replace("\"", "\\\"");
            newName = newName.Replace("\"", "\\\"");
            var updatedBlock = propBlock.Replace($"\"m_Name\": \"{oldName}\"", $"\"m_Name\": \"{newName}\"");
            content = content.Replace(propBlock, updatedBlock);
            File.WriteAllText(path, content);
            try { AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate); }
            catch (Exception ex) { Debug.LogWarning($"ShaderGraph import failed for '{path}': {ex.Message}"); }
            return $"property renamed: {oldName} → {newName}";
        }

        static string MapPropertyType(string type) => type switch
        {
            "Float"     => "UnityEditor.ShaderGraph.Internal.Vector1ShaderProperty",
            "Color"     => "UnityEditor.ShaderGraph.Internal.ColorShaderProperty",
            "Vector"    => "UnityEditor.ShaderGraph.Internal.Vector4ShaderProperty",
            "Texture2D" => "UnityEditor.ShaderGraph.Internal.Texture2DShaderProperty",
            "Boolean"   => "UnityEditor.ShaderGraph.Internal.BooleanShaderProperty",
            _ => throw new ArgumentException($"Unknown property type '{type}'. Use: Float, Color, Vector, Texture2D, Boolean")
        };

        static string BuildValueField(string type, string defaultValue)
        {
            if (defaultValue == null) return "";
            return type switch
            {
                "Float"   => $",\n  \"m_Value\": {defaultValue}",
                "Boolean" => $",\n  \"m_Value\": {(defaultValue == "true" ? "true" : "false")}",
                _ => ""
            };
        }
    }
}
