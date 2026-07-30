// search_context TCP command implementation, registered in the Chat.CLI assembly so it can
// directly use SceneMentionIndex + AssetMentionIndex (Editor assembly can't reference Chat.CLI).
// Injected into SearchHelper.SearchContextProvider via [InitializeOnLoad].
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Chat
{
    [InitializeOnLoad]
    internal static class SearchContextPlugin
    {
        private static readonly MentionCoordinator _coordinator =
            new MentionCoordinator(new SceneMentionIndex(), new AssetMentionIndex());

        static SearchContextPlugin()
        {
            SearchHelper.SearchContextProvider = Search;
        }

        internal static string Search(string query, int limit = 30, string types = null)
        {
            bool goOnly = types == "go";
            int fetchLimit = goOnly ? limit * 2 : limit;
            var candidates = new List<MentionCandidate>();
            _coordinator.Search(query, fetchLimit, candidates);

            var sb = new StringBuilder();
            int count = 0;
            foreach (var c in candidates)
            {
                if (count >= limit) break;
                bool isGo = c.Chip.KindKey == ChipKindKeys.Hierarchy;
                if (goOnly && !isGo) continue;
                var typeCode = isGo ? "go" : ExtToCode(Path.GetExtension(c.Chip.Path));
                sb.Append(typeCode).Append('\t')
                  .Append(c.Chip.Path).Append('\t')
                  .AppendLine(c.Chip.DisplayName);
                count++;
            }
            return sb.ToString().TrimEnd('\n');
        }

        internal static string ExtToCode(string ext)
        {
            switch (ext?.ToLowerInvariant() ?? "")
            {
                case ".cs":                              return "cs";
                case ".prefab":                          return "pfb";
                case ".mat":                             return "mat";
                case ".unity":                           return "scene";
                case ".png": case ".jpg":
                case ".tga": case ".jpeg":               return "tex";
                case ".fbx": case ".obj":
                case ".blend": case ".dae":              return "model";
                case ".wav": case ".mp3":
                case ".ogg": case ".aiff":               return "audio";
                case ".asset":                           return "so";
                case ".anim":                            return "anim";
                case ".shader": case ".shadergraph":     return "shader";
                case "":                                 return "folder";
                default:                                 return "asset";
            }
        }
    }
}
