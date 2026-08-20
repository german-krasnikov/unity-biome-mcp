using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditorInternal;

namespace UnityMCP.Editor
{
    internal static class ProjectSettingsHelper
    {
        internal static string Execute(string action, string argsJson)
        {
            var target = JsonHelper.ExtractString(argsJson, "target");
            var prop = JsonHelper.ExtractString(argsJson, "prop");
            var value = JsonHelper.ExtractString(argsJson, "value");
            var indexStr = JsonHelper.ExtractString(argsJson, "index");

            return target switch
            {
                "tags"           => action == "get" ? GetTags()           : (prop == "remove" ? RemoveTag(value) : AddTag(value ?? prop)),
                "layers"         => action == "get" ? GetLayers()         : SetLayer(
                    indexStr != null ? int.Parse(indexStr) : throw new System.Exception("'index' is required for layers set"), value),
                "sorting_layers" => action == "get" ? GetSortingLayers() : throw new System.Exception("sorting_layers is read-only"),
                "quality"        => action == "get" ? GetQuality()        : SetQuality(prop, value),
                "physics"        => action == "get" ? GetPhysics()        : SetPhysics(prop, value),
                "time"           => action == "get" ? GetTime()           : SetViaReflection(typeof(Time), prop, value),
                "player"         => action == "get" ? GetPlayer()         : SetPlayer(prop, value, argsJson),
                "graphics"       => action == "get" ? GetGraphics()       : SetGraphics(prop, value),
                "audio"          => action == "get" ? GetAudio()          : throw new System.Exception("audio is read-only via this tool"),
                "input"          => action == "get" ? GetInput()          : throw new System.Exception("input axes are read-only"),
                _ => throw new System.Exception($"Unknown target '{target}'. Valid: tags, layers, sorting_layers, quality, physics, time, player, graphics, audio, input")
            };
        }

        // ── tags ─────────────────────────────────────────────────────────────

        static string GetTags()
        {
            var sb = new StringBuilder();
            foreach (var t in InternalEditorUtility.tags)
                sb.AppendLine(t);
            return sb.ToString().TrimEnd();
        }

        static string AddTag(string tag)
        {
            if (string.IsNullOrEmpty(tag)) throw new System.Exception("value or prop is required for tags set");
            var tm = LoadTagManager();
            var tagsProp = tm.FindProperty("tags");
            tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
            tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tag;
            tm.ApplyModifiedProperties();
            return "ok";
        }

        static string RemoveTag(string tag)
        {
            if (string.IsNullOrEmpty(tag)) throw new System.Exception("value is required for tag remove");
            var tm = LoadTagManager();
            var tagsProp = tm.FindProperty("tags");
            for (int i = 0; i < tagsProp.arraySize; i++)
            {
                if (tagsProp.GetArrayElementAtIndex(i).stringValue == tag)
                {
                    tagsProp.DeleteArrayElementAtIndex(i);
                    tm.ApplyModifiedProperties();
                    return "ok";
                }
            }
            throw new System.Exception($"Tag '{tag}' not found");
        }

        // ── layers ────────────────────────────────────────────────────────────

        static string GetLayers()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < 32; i++)
            {
                var name = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(name))
                    sb.AppendLine($"{i}: {name}");
            }
            return sb.ToString().TrimEnd();
        }

        static string SetLayer(int index, string name)
        {
            if (index < 6) throw new System.Exception("Layers 0-5 are reserved by Unity. Use index >= 6.");
            var tm = LoadTagManager();
            var layersProp = tm.FindProperty("layers");
            layersProp.GetArrayElementAtIndex(index).stringValue = name;
            tm.ApplyModifiedProperties();
            return "ok";
        }

        // ── sorting layers ────────────────────────────────────────────────────

        static string GetSortingLayers()
        {
            var sb = new StringBuilder();
            foreach (var sl in SortingLayer.layers)
                sb.AppendLine($"{sl.name} (id={sl.id})");
            return sb.ToString().TrimEnd();
        }

        // ── quality ───────────────────────────────────────────────────────────

        static string GetQuality()
        {
            int level = QualitySettings.GetQualityLevel();
            return $"shadowDistance: {QualitySettings.shadowDistance}\n" +
                   $"vSyncCount: {QualitySettings.vSyncCount}\n" +
                   $"lodBias: {QualitySettings.lodBias}\n" +
                   $"pixelLightCount: {QualitySettings.pixelLightCount}\n" +
                   $"antiAliasing: {QualitySettings.antiAliasing}\n" +
                   $"currentLevel: {level} ({QualitySettings.names[level]})";
        }

        static string SetQuality(string prop, string value)
        {
            if (prop == "currentLevel")
            {
                QualitySettings.SetQualityLevel(int.Parse(value), applyExpensiveChanges: true);
                return "ok";
            }
            return SetViaReflection(typeof(QualitySettings), prop, value);
        }

        // ── physics ───────────────────────────────────────────────────────────

        static string GetPhysics()
        {
            var sb = new StringBuilder();
            sb.Append($"gravity: {Physics.gravity}\n");
            sb.Append($"defaultSolverIterations: {Physics.defaultSolverIterations}\n");
            sb.Append($"defaultContactOffset: {Physics.defaultContactOffset}\n");
            sb.Append($"bounceThreshold: {Physics.bounceThreshold}\n");
            AppendCollisionMatrix(sb);
            return sb.ToString().TrimEnd();
        }

        static void AppendCollisionMatrix(StringBuilder sb)
        {
            var disabled = new StringBuilder();
            for (int i = 0; i < 32; i++)
            {
                var nameI = LayerMask.LayerToName(i);
                if (string.IsNullOrEmpty(nameI)) continue;
                for (int j = i; j < 32; j++)
                {
                    var nameJ = LayerMask.LayerToName(j);
                    if (string.IsNullOrEmpty(nameJ)) continue;
                    if (Physics.GetIgnoreLayerCollision(i, j))
                        disabled.Append(nameI).Append(" x ").Append(nameJ).AppendLine(": off");
                }
            }
            if (disabled.Length == 0)
                sb.AppendLine("--- Collision Matrix: all enabled ---");
            else
                sb.Append("--- Collision Matrix ---\n").Append(disabled);
        }

        static string SetPhysics(string prop, string value)
        {
            if (prop == "gravity")
            {
                Physics.gravity = ValueParser.ParseVector3(value);
                return "ok";
            }
            return SetViaReflection(typeof(Physics), prop, value);
        }

        // ── time ──────────────────────────────────────────────────────────────

        static string GetTime() =>
            string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "fixedDeltaTime: {0}\nmaximumDeltaTime: {1}\ntimeScale: {2}",
                Time.fixedDeltaTime, Time.maximumDeltaTime, Time.timeScale);

        // ── player ────────────────────────────────────────────────────────────

        static string GetPlayer() =>
            $"companyName: {PlayerSettings.companyName}\n" +
            $"productName: {PlayerSettings.productName}\n" +
            $"bundleVersion: {PlayerSettings.bundleVersion}";

        static string SetPlayer(string prop, string value, string argsJson)
        {
            if (prop == "ScriptingBackend")
            {
                var buildTargetStr = JsonHelper.ExtractString(argsJson, "build_target") ?? "Standalone";
                if (!System.Enum.TryParse(buildTargetStr, ignoreCase: true, out BuildTargetGroup group))
                    throw new System.Exception($"Invalid build_target '{buildTargetStr}'. Valid: {string.Join(", ", System.Enum.GetNames(typeof(BuildTargetGroup)))}");
                if (!System.Enum.TryParse(value, ignoreCase: true, out ScriptingImplementation backend))
                    throw new System.Exception($"Invalid ScriptingBackend '{value}'. Valid: Mono2x, IL2CPP, WinRTDotNET");
                PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(group), backend);
                return "ok";
            }
            return SetViaReflection(typeof(PlayerSettings), prop, value);
        }

        // ── graphics ─────────────────────────────────────────────────────────

        static string GetGraphics()
        {
            var rpa = GraphicsSettings.defaultRenderPipeline;
            return $"renderPipeline:{(rpa != null ? rpa.name : "none")}\n" +
                   $"colorSpace:{PlayerSettings.colorSpace}\n" +
                   $"transparencySortMode:{GraphicsSettings.transparencySortMode}\n" +
                   $"lightsUseLinearIntensity:{GraphicsSettings.lightsUseLinearIntensity}";
        }

        static string SetGraphics(string prop, string value)
        {
            if (prop == "colorSpace")
            {
                PlayerSettings.colorSpace = (ColorSpace)System.Enum.Parse(typeof(ColorSpace), value, ignoreCase: true);
                return "ok";
            }
            return SetViaReflection(typeof(GraphicsSettings), prop, value);
        }

        // ── audio ─────────────────────────────────────────────────────────────

        static string GetAudio()
        {
            var so = LoadProjectSettingsAsset("AudioManager");
            var volume = so.FindProperty("m_Volume")?.floatValue ?? 1f;
            var rolloff = so.FindProperty("m_RolloffScale")?.floatValue ?? 1f;
            var mode = so.FindProperty("m_DefaultSpeakerMode")?.intValue ?? 2;
            return $"masterVolume:{volume}\nrolloffScale:{rolloff}\ndefaultSpeakerMode:{mode}";
        }

        // ── input ─────────────────────────────────────────────────────────────

        static string GetInput()
        {
            var so = LoadProjectSettingsAsset("InputManager");
            var axes = so.FindProperty("m_Axes");
            var sb = new StringBuilder();
            for (int i = 0; i < axes.arraySize; i++)
            {
                var axis = axes.GetArrayElementAtIndex(i);
                var n = axis.FindPropertyRelative("m_Name")?.stringValue ?? "";
                var desc = axis.FindPropertyRelative("descriptiveName")?.stringValue ?? "";
                sb.AppendLine(string.IsNullOrEmpty(desc) ? n : $"{n} ({desc})");
            }
            return sb.ToString().TrimEnd();
        }

        // ── helpers ───────────────────────────────────────────────────────────

        static SerializedObject LoadProjectSettingsAsset(string managerName)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath($"ProjectSettings/{managerName}.asset");
            if (assets.Length == 0) throw new System.Exception($"{managerName}.asset not found");
            return new SerializedObject(assets[0]);
        }

        static SerializedObject LoadTagManager() => LoadProjectSettingsAsset("TagManager");

        static string SetViaReflection(System.Type type, string prop, string value)
        {
            if (string.IsNullOrEmpty(prop)) throw new System.Exception("prop is required");
            if (value == null) throw new System.Exception("value is required");
            var pi = type.GetProperty(prop, BindingFlags.Static | BindingFlags.Public);
            if (pi == null) throw new System.Exception($"Property '{prop}' not found on {type.Name}");
            if (!pi.CanWrite) throw new System.Exception($"Property '{prop}' on {type.Name} is read-only");

            object parsed;
            if      (pi.PropertyType == typeof(float))   parsed = float.Parse(value, CultureInfo.InvariantCulture);
            else if (pi.PropertyType == typeof(int))     parsed = int.Parse(value);
            else if (pi.PropertyType == typeof(bool))    parsed = value == "true";
            else if (pi.PropertyType == typeof(Vector3)) parsed = ValueParser.ParseVector3(value);
            else                                         parsed = value;

            pi.SetValue(null, parsed);
            return "ok";
        }
    }
}
