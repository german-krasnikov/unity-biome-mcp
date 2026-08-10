using System;
using System.Globalization;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class ValueParser
    {
        internal static float[] ParseFloats(string value, int expected)
        {
            var parts = value.Trim('(', ')').Split(',');
            if (parts.Length != expected)
                throw new ArgumentException($"Expected {expected} components but got {parts.Length}: {value}");
            var result = new float[expected];
            for (int i = 0; i < expected; i++)
                if (!float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out result[i]))
                    throw new ArgumentException($"Invalid float at index {i}: {value}");
            return result;
        }

        internal static Vector2 ParseVector2(string v) { var f = ParseFloats(v, 2); return new Vector2(f[0], f[1]); }
        internal static Vector3 ParseVector3(string v) { var f = ParseFloats(v, 3); return new Vector3(f[0], f[1], f[2]); }
        internal static Vector4 ParseVector4(string v) { var f = ParseFloats(v, 4); return new Vector4(f[0], f[1], f[2], f[3]); }

        /// <summary>Flexible 2–4 component vector. Missing Z/W default to 0. Used for shader/material vector properties.</summary>
        internal static Vector4 ParseVector4Lenient(string s)
        {
            s = s.Trim().Trim('(', ')');
            var p = s.Split(',');
            if (p.Length < 2) throw new ArgumentException($"Invalid vector '{s}'. Use (x,y,z,w)");
            return new Vector4(
                float.Parse(p[0].Trim(), CultureInfo.InvariantCulture),
                float.Parse(p[1].Trim(), CultureInfo.InvariantCulture),
                p.Length > 2 ? float.Parse(p[2].Trim(), CultureInfo.InvariantCulture) : 0f,
                p.Length > 3 ? float.Parse(p[3].Trim(), CultureInfo.InvariantCulture) : 0f);
        }

        /// <summary>Parse "true"/"false"/"1"/"0" case-insensitively. Throws ArgumentException on invalid input.</summary>
        internal static bool ParseBool(string value)
        {
            if (string.IsNullOrEmpty(value)) throw new ArgumentException($"Invalid bool: empty");
            if (value.Equals("true",  StringComparison.OrdinalIgnoreCase) || value == "1") return true;
            if (value.Equals("false", StringComparison.OrdinalIgnoreCase) || value == "0") return false;
            throw new ArgumentException($"Invalid bool: '{value}'");
        }
        internal static Quaternion ParseQuaternion(string v)
        {
            // ComponentSerializer emits 3-component Euler "(x, y, z)" — detect by count.
            // 4-component strings are raw xyzw (pass-through from LLM supplying explicit quaternion).
            var parts = v.Trim('(', ')').Split(',');
            if (parts.Length == 3) { var f = ParseFloats(v, 3); return Quaternion.Euler(f[0], f[1], f[2]); }
            if (parts.Length == 4) { var f = ParseFloats(v, 4); return new Quaternion(f[0], f[1], f[2], f[3]); }
            throw new ArgumentException($"Expected 3 or 4 components for Quaternion but got {parts.Length}: {v}");
        }

        internal static Color ParseColor(string value)
        {
            value = value.Trim();
            if (value.StartsWith("#"))
            {
                if (!ColorUtility.TryParseHtmlString(value, out var c))
                    throw new ArgumentException($"Invalid color: {value}");
                return c;
            }
            if (value.Length == 6 || value.Length == 8)
                if (ColorUtility.TryParseHtmlString("#" + value, out var c))
                    return c;
            var s = value.Trim('(', ')');
            var p = s.Split(',');
            if (p.Length < 3)
                throw new ArgumentException($"Invalid color: {value}");
            try
            {
                return new Color(
                    float.Parse(p[0].Trim(), CultureInfo.InvariantCulture),
                    float.Parse(p[1].Trim(), CultureInfo.InvariantCulture),
                    float.Parse(p[2].Trim(), CultureInfo.InvariantCulture),
                    p.Length > 3 ? float.Parse(p[3].Trim(), CultureInfo.InvariantCulture) : 1f);
            }
            catch (FormatException)
            {
                throw new ArgumentException($"Invalid color: {value}");
            }
        }

        internal static void SetPropertyValue(SerializedProperty property, string value)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    switch (property.numericType)
                    {
                        case SerializedPropertyNumericType.Int64:
                            if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lv))
                                throw new ArgumentException($"Invalid long: '{value}'");
                            property.longValue = lv; break;
                        case SerializedPropertyNumericType.UInt64:
                            if (!ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ulv))
                                throw new ArgumentException($"Invalid ulong: '{value}'");
                            property.ulongValue = ulv; break;
                        default:
                            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intVal))
                                throw new ArgumentException($"Invalid int: '{value}'");
                            property.intValue = intVal; break;
                    }
                    break;
                case SerializedPropertyType.LayerMask:
                    if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lmVal))
                        throw new ArgumentException($"Invalid LayerMask: '{value}'");
                    property.intValue = lmVal; break;
                case SerializedPropertyType.Float:
                    if (property.numericType == SerializedPropertyNumericType.Double)
                    {
                        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var dv))
                            throw new ArgumentException($"Invalid double: '{value}'");
                        property.doubleValue = dv;
                    }
                    else
                    {
                        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var fVal))
                            throw new ArgumentException($"Invalid float: '{value}'");
                        property.floatValue = fVal;
                    }
                    break;
                case SerializedPropertyType.Boolean: property.boolValue = ParseBool(value); break;
                case SerializedPropertyType.String: property.stringValue = value; break;
                case SerializedPropertyType.Vector2: property.vector2Value = ParseVector2(value); break;
                case SerializedPropertyType.Vector3: property.vector3Value = ParseVector3(value); break;
                case SerializedPropertyType.Vector4: property.vector4Value = ParseVector4(value); break;
                case SerializedPropertyType.Quaternion: property.quaternionValue = ParseQuaternion(value); break;
                case SerializedPropertyType.Color: property.colorValue = ParseColor(value); break;
                case SerializedPropertyType.Vector2Int:
                    var v2i = ParseFloats(value, 2);
                    property.vector2IntValue = new Vector2Int((int)v2i[0], (int)v2i[1]); break;
                case SerializedPropertyType.Vector3Int:
                    var v3i = ParseFloats(value, 3);
                    property.vector3IntValue = new Vector3Int((int)v3i[0], (int)v3i[1], (int)v3i[2]); break;
                case SerializedPropertyType.Enum:
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var enumVal))
                    {
                        if (enumVal < 0)
                            throw new ArgumentException($"Invalid enum value '{value}': negative integers are not valid enum values: {property.propertyPath}");
                        property.enumValueFlag = enumVal;  // underlying C# int; handles gap-valued + [Flags] enums
                    }
                    else
                    {
                        var idx = Array.IndexOf(property.enumNames, value);
                        if (idx < 0)
                            idx = Array.FindIndex(property.enumNames, n => string.Equals(n, value, StringComparison.OrdinalIgnoreCase));
                        if (idx >= 0) property.enumValueIndex = idx;
                        else throw new ArgumentException(
                            $"Invalid enum value '{value}'. Valid: {string.Join(", ", property.enumNames)}: {property.propertyPath}");
                    }
                    break;
                case SerializedPropertyType.ArraySize:
                    if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var arrSize))
                        throw new ArgumentException($"Invalid int: '{value}'");
                    property.arraySize = arrSize; break;
                case SerializedPropertyType.ObjectReference: SetObjectReference(property, value); break;
                case SerializedPropertyType.Rect:
                {
                    var f = ParseFloats(value, 4);
                    property.rectValue = new Rect(f[0], f[1], f[2], f[3]); break;
                }
                case SerializedPropertyType.Bounds:
                {
                    var f = ParseFloats(value, 6);
                    property.boundsValue = new Bounds(
                        new Vector3(f[0], f[1], f[2]), new Vector3(f[3], f[4], f[5])); break;
                }
                case SerializedPropertyType.RectInt:
                {
                    var f = ParseFloats(value, 4);
                    property.rectIntValue = new RectInt((int)f[0], (int)f[1], (int)f[2], (int)f[3]); break;
                }
                case SerializedPropertyType.BoundsInt:
                {
                    var f = ParseFloats(value, 6);
                    property.boundsIntValue = new BoundsInt(
                        new Vector3Int((int)f[0], (int)f[1], (int)f[2]),
                        new Vector3Int((int)f[3], (int)f[4], (int)f[5])); break;
                }
                default: throw new ArgumentException($"Unsupported property type: {property.propertyType}");
            }
        }

        private static void SetObjectReference(SerializedProperty property, string value)
        {
            if (string.IsNullOrEmpty(value) || value == "null") { property.objectReferenceValue = null; return; }
            // Defensive: strip one surrounding quote pair if present
            if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
                value = value.Substring(1, value.Length - 2);
            // P-258: component wire format "path::TypeName #componentId" — resolve typed component directly.
            // The "::" suffix signals this is a component ref; the "#id" encodes the component instanceID.
            if (value.Contains("::") && value.Contains("#"))
            {
                var comp = ComponentSerializer.FindComponentByRef(value);
                if (comp != null) { property.objectReferenceValue = comp; return; }
                // Fall through to path-based lookup if resolution fails (graceful degradation).
            }
            // P-258b: "path::TypeName" (no #id) — LLM-friendly bare type resolution.
            // e.g. "/Root/Player::AudioSource" → first AudioSource on the Player GO.
            // Sub-asset paths ("Assets/Foo.anim::Clip") fall through when FindObject returns null.
            if (value.Contains("::") && !value.Contains("#"))
            {
                var colonIdx = value.IndexOf("::");
                var goPath   = value.Substring(0, colonIdx).Trim();
                var typeName = value.Substring(colonIdx + 2).Trim();
                if (!string.IsNullOrEmpty(goPath) && !string.IsNullOrEmpty(typeName))
                {
                    var go = ComponentSerializer.FindObject(goPath);
                    if (go != null)
                    {
                        var comp = ComponentSerializer.FindComponent(go, typeName);
                        if (comp == null)
                        {
                            var available = string.Join(", ",
                                System.Array.ConvertAll(go.GetComponents<Component>(), c => c.GetType().Name));
                            throw new ArgumentException(
                                $"No '{typeName}' component on '{goPath}'. Available: {available}. " +
                                "For multiple components of the same type, use 'path::TypeName #componentId'.");
                        }
                        property.objectReferenceValue = comp;
                        return;
                    }
                    // go == null: fall through to sub-asset check below.
                }
            }
            if (value.StartsWith("#"))
            {
                if (!TransientObjectId.TryParse(value, out var objectId))
                    throw new ArgumentException($"Invalid entity ID: {value}");
                var resolved = objectId.Resolve();
                if (resolved == null)
                    throw new ArgumentException($"No object found for entity ID: {value}");
                property.objectReferenceValue = resolved;
                return;
            }
            var fieldType = GetSerializedFieldType(property);
            if (fieldType == null)
                Debug.LogWarning($"[MCP] Cannot resolve C# type for '{property.propertyPath}'. " +
                    "ObjectReference assignment may fail if field expects a specific Component.");
            var refGo = ComponentSerializer.FindObject(value);
            if (refGo != null)
            {
                if (fieldType != null && typeof(Component).IsAssignableFrom(fieldType) && fieldType != typeof(GameObject))
                {
                    var comp = refGo.GetComponent(fieldType);
                    if (comp == null)
                        throw new ArgumentException(
                            $"Type mismatch: field '{property.propertyPath}' expects {fieldType.Name}, " +
                            $"but '{value}' has no such component.");
                    property.objectReferenceValue = comp;
                }
                else
                {
                    property.objectReferenceValue = refGo;
                    // G3: when fieldType is unknown (C++ backed), Unity may coerce GameObject to
                    // Transform or reject it (null). Try each component to find one the field accepts.
                    var stored = property.objectReferenceValue;
                    if ((stored == null || stored is Transform) && refGo != null)
                    {
                        foreach (var comp in refGo.GetComponents<Component>())
                        {
                            if (comp is Transform) continue;
                            property.objectReferenceValue = comp;
                            var candidate = property.objectReferenceValue;
                            if (candidate != null && !(candidate is Transform)) return;
                        }
                        property.objectReferenceValue = refGo; // restore if no component matched
                    }
                    if (property.objectReferenceValue == null && refGo != null)
                        throw new ArgumentException(
                            $"Type mismatch: field '{property.propertyPath}' rejected GameObject " +
                            "(field expects a specific Component).");
                }
                return;
            }
            var sepIdx = value.IndexOf("::");
            if (sepIdx > 0 && sepIdx < value.Length - 2)
            {
                var assetPath = value.Substring(0, sepIdx);
                var subName = value.Substring(sepIdx + 2);
                var allAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                foreach (var a in allAssets)
                {
                    if (a == null) continue; // P-403b: LoadAllAssetsAtPath can return nulls
                    if (a.name == subName && (fieldType == null || fieldType.IsInstanceOfType(a)))
                    {
                        property.objectReferenceValue = a;
                        if (property.objectReferenceValue != null)
                            return;
                        // Type rejected by serialized field — try next candidate
                    }
                }
                throw new ArgumentException($"Sub-asset '{subName}' not found in: {assetPath}");
            }
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(value);
            if (asset == null) throw new ArgumentException($"Not found in scene or assets: {value}");
            if (fieldType != null && typeof(Component).IsAssignableFrom(fieldType) && asset is GameObject assetGo)
            {
                var comp = assetGo.GetComponent(fieldType);
                if (comp == null)
                    throw new ArgumentException($"Component {fieldType.Name} not found on asset: {value}");
                property.objectReferenceValue = comp;
            }
            else
            {
                property.objectReferenceValue = asset;
                if (property.objectReferenceValue == null && asset != null)
                    throw new ArgumentException(
                        $"Type mismatch: field '{property.propertyPath}' rejected asset " +
                        "(field expects a specific Component or incompatible type).");
            }
        }

        /// <summary>Split comma-separated array values, respecting parens/brackets.</summary>
        internal static string[] SplitArrayValues(string value)
        {
            value = value?.Trim() ?? "";
            if (value == "" || value == "[]") return Array.Empty<string>();
            if (value.StartsWith("[") && value.EndsWith("]")) value = value.Substring(1, value.Length - 2).Trim();

            var result = new System.Collections.Generic.List<string>();
            int depth = 0;
            int start = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '(' || c == '[') depth++;
                else if (c == ')' || c == ']') depth--;
                else if (c == ',' && depth == 0)
                {
                    result.Add(value.Substring(start, i - start).Trim());
                    start = i + 1;
                }
            }
            result.Add(value.Substring(start).Trim());
            return result.ToArray();
        }

        // Fix C: iterative nested path traversal + Fix A dependency (internal visibility)
        internal static Type GetSerializedFieldType(SerializedProperty property)
        {
            try
            {
                var targetObj = property.serializedObject.targetObject;
                if (targetObj == null) return null;
                var type = targetObj.GetType();
                var segments = property.propertyPath.Split('.');
                FieldInfo lastField = null;

                for (int i = 0; i < segments.Length; i++)
                {
                    var seg = segments[i];
                    if (seg == "Array" && i + 1 < segments.Length && segments[i + 1].StartsWith("data["))
                    {
                        if (lastField != null)
                        {
                            var ft = lastField.FieldType;
                            type = ft.IsArray ? ft.GetElementType() :
                                   ft.IsGenericType ? ft.GetGenericArguments()[0] : ft;
                            lastField = null;  // element has no FieldInfo; force final return to use `type`
                        }
                        i++; // skip data[N]
                        continue;
                    }
                    lastField = null;
                    var search = type;
                    while (search != null)
                    {
                        lastField = search.GetField(seg, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                        if (lastField != null) break;
                        search = search.BaseType;
                    }
                    if (lastField == null) return null;
                    type = lastField.FieldType;
                }
                if (lastField == null) return type;  // type holds element type from Array branch
                var resultType = lastField.FieldType;
                if (resultType.IsArray) return resultType.GetElementType();
                if (resultType.IsGenericType) return resultType.GetGenericArguments()[0];
                return resultType;
            }
            catch (Exception e) { Debug.LogWarning($"[MCP] GetSerializedFieldType: {e.Message}"); }
            return null;
        }
    }
}
