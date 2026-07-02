using System;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityMCP.Editor
{
    internal static class EnvironmentHelper
    {
        private static readonly string[] ValidActions = { "get", "set" };

        // Known writable properties and their types.
        private static readonly string[] WritableProps =
        {
            "ambientMode", "ambientLight", "ambientIntensity",
            "ambientSkyColor", "ambientEquatorColor", "ambientGroundColor",
            "fog", "fogColor", "fogMode", "fogDensity", "fogStartDistance", "fogEndDistance",
            "reflectionIntensity", "reflectionBounces",
            "subtractiveShadowColor", "defaultReflectionResolution"
        };

        internal static string Execute(string action, string argsJson)
        {
            return action switch
            {
                "get" => Get(),
                "set" => Set(argsJson),
                _ => throw new ArgumentException(ErrorHelper.InvalidAction(action, ValidActions))
            };
        }

        private static string Fmt(float v) => v.ToString("G4", CultureInfo.InvariantCulture);

        private static string Get()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"ambientMode: {RenderSettings.ambientMode}");
            sb.AppendLine($"ambientLight: {RenderSettings.ambientLight}");
            sb.AppendLine($"ambientIntensity: {Fmt(RenderSettings.ambientIntensity)}");
            sb.AppendLine($"ambientSkyColor: {RenderSettings.ambientSkyColor}");
            sb.AppendLine($"ambientEquatorColor: {RenderSettings.ambientEquatorColor}");
            sb.AppendLine($"ambientGroundColor: {RenderSettings.ambientGroundColor}");
            sb.AppendLine($"fog: {(RenderSettings.fog ? "true" : "false")}");
            sb.AppendLine($"fogColor: {RenderSettings.fogColor}");
            sb.AppendLine($"fogMode: {RenderSettings.fogMode}");
            sb.AppendLine($"fogDensity: {Fmt(RenderSettings.fogDensity)}");
            sb.AppendLine($"fogStartDistance: {Fmt(RenderSettings.fogStartDistance)}");
            sb.AppendLine($"fogEndDistance: {Fmt(RenderSettings.fogEndDistance)}");
            var skybox = RenderSettings.skybox;
            sb.AppendLine($"skybox: {(skybox != null ? skybox.name : "none")}");
            var sun = RenderSettings.sun;
            sb.AppendLine($"sun: {(sun != null ? sun.name : "none")}");
            sb.AppendLine($"reflectionIntensity: {Fmt(RenderSettings.reflectionIntensity)}");
            sb.AppendLine($"reflectionBounces: {RenderSettings.reflectionBounces}");
            sb.AppendLine($"subtractiveShadowColor: {RenderSettings.subtractiveShadowColor}");
            sb.AppendLine($"defaultReflectionResolution: {RenderSettings.defaultReflectionResolution}");
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Returns the RenderSettings singleton UnityEngine.Object for Undo recording.
        /// Unity stores RenderSettings as a hidden asset per-scene; we retrieve it via
        /// the internal GetRenderSettings() method (available since Unity 2019).
        /// Fallback: search Resources for the type.
        /// </summary>
        private static UnityEngine.Object GetRenderSettingsObject()
        {
            var mi = typeof(RenderSettings).GetMethod("GetRenderSettings",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (mi != null)
            {
                var obj = mi.Invoke(null, null) as UnityEngine.Object;
                if (obj != null) return obj;
            }
            // Fallback: find by type in loaded objects (always works).
            var all = Resources.FindObjectsOfTypeAll(typeof(RenderSettings));
            if (all.Length > 0) return all[0];
            throw new InvalidOperationException("RenderSettings singleton not found");
        }

        private static string Set(string argsJson)
        {
            var prop = JsonHelper.ExtractString(argsJson, "prop");
            var value = JsonHelper.ExtractString(argsJson, "value");
            if (string.IsNullOrEmpty(prop))
                throw new ArgumentException("prop is required");
            if (value == null)
                throw new ArgumentException("value is required");

            var rsObj = GetRenderSettingsObject();
            Undo.RecordObject(rsObj, $"MCP Set Environment {prop}");

            switch (prop)
            {
                case "ambientMode":
                    RenderSettings.ambientMode = ParseEnum<AmbientMode>(value);
                    break;
                case "ambientLight":
                    RenderSettings.ambientLight = ValueParser.ParseColor(value);
                    break;
                case "ambientIntensity":
                    RenderSettings.ambientIntensity = float.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "ambientSkyColor":
                    RenderSettings.ambientSkyColor = ValueParser.ParseColor(value);
                    break;
                case "ambientEquatorColor":
                    RenderSettings.ambientEquatorColor = ValueParser.ParseColor(value);
                    break;
                case "ambientGroundColor":
                    RenderSettings.ambientGroundColor = ValueParser.ParseColor(value);
                    break;
                case "fog":
                    RenderSettings.fog = ValueParser.ParseBool(value);
                    break;
                case "fogColor":
                    RenderSettings.fogColor = ValueParser.ParseColor(value);
                    break;
                case "fogMode":
                    RenderSettings.fogMode = ParseEnum<FogMode>(value);
                    break;
                case "fogDensity":
                    RenderSettings.fogDensity = float.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "fogStartDistance":
                    RenderSettings.fogStartDistance = float.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "fogEndDistance":
                    RenderSettings.fogEndDistance = float.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "reflectionIntensity":
                    RenderSettings.reflectionIntensity = float.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "reflectionBounces":
                    RenderSettings.reflectionBounces = int.Parse(value);
                    break;
                case "subtractiveShadowColor":
                    RenderSettings.subtractiveShadowColor = ValueParser.ParseColor(value);
                    break;
                case "defaultReflectionResolution":
                    RenderSettings.defaultReflectionResolution = int.Parse(value);
                    break;
                default:
                    throw new ArgumentException(
                        $"Unknown property '{prop}'. Valid: {string.Join(", ", WritableProps)}");
            }

            EditorUtility.SetDirty(rsObj);
            return "ok";
        }

        private static T ParseEnum<T>(string value) where T : struct
        {
            if (Enum.TryParse<T>(value, true, out var result))
                return result;
            throw new ArgumentException(
                $"Invalid {typeof(T).Name} value '{value}'. Valid: {string.Join("|", Enum.GetNames(typeof(T)))}");
        }
    }
}
