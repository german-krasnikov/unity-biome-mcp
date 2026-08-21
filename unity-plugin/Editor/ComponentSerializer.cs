using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEditor;

namespace UnityMCP.Editor
{
    public static partial class ComponentSerializer
    {
        private static readonly System.Collections.Generic.HashSet<string> SkipProperties = new System.Collections.Generic.HashSet<string>
        {
            "m_Script", "m_ObjectHideFlags", "m_GameObject",
            "m_Enabled", "m_EditorHideFlags", "m_EditorClassIdentifier"
        };

        private static readonly System.Collections.Generic.HashSet<string> SkipTransformProperties = new System.Collections.Generic.HashSet<string>
        {
            "m_Father", "m_Children", "m_RootOrder", "m_ConstrainProportionsScale"
        };

        public static string Serialize(string path, string typeName)
        {
            var go = FindObject(path);
            return go == null ? null : Serialize(go, typeName);
        }

        public static string Serialize(GameObject go, string typeName)
        {
            if (go == null) return null;
            var component = FindComponent(go, typeName);
            if (component == null) return null;

            var sb = new StringBuilder();
            SerializeComponent(sb, component);
            return sb.ToString().TrimEnd();
        }

        public static string SerializeAll(string objectId)
        {
            var go = FindObjectById(objectId);
            if (go == null) return null;

            var sb = new StringBuilder();
            sb.Append("name: ").AppendLine(go.name);
            sb.Append("active: ").AppendLine(go.activeSelf ? "true" : "false");
            sb.Append("tag: ").AppendLine(go.tag);
            var layerName = LayerMask.LayerToName(go.layer);
            sb.Append("layer: ").AppendLine(string.IsNullOrEmpty(layerName) ? go.layer.ToString() : layerName);
            if (go.isStatic) sb.AppendLine("static: true");

            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp == null) continue;
                sb.AppendLine("---");
                sb.Append('[').Append(comp.GetType().Name).AppendLine("]");
                SerializeComponent(sb, comp);
            }

            return sb.ToString().TrimEnd();
        }

        public static string SerializeAll(int legacyId)
            => SerializeAll(legacyId.ToString(CultureInfo.InvariantCulture));

        private static void SerializeComponent(StringBuilder sb, Component component)
        {
            var isTransform = component is Transform;
            var so = new SerializedObject(component);
            var prop = so.GetIterator();
            int written = 0;
            if (prop.NextVisible(true))
            {
                do
                {
                    if (SkipProperties.Contains(prop.name)) continue;
                    if (isTransform && SkipTransformProperties.Contains(prop.name)) continue;
                    sb.Append(prop.name).Append(": ");
                    AppendPropertyValue(sb, prop);
                    sb.AppendLine();
                    written++;
                } while (prop.NextVisible(false));
            }
            if (written == 0) sb.AppendLine("(no serialized fields)");

            // RectTransform: also expose friendly names (anchoredPosition, sizeDelta, anchorMin, anchorMax, pivot)
            if (component is RectTransform rt)
            {
                var ap = rt.anchoredPosition;
                var sd = rt.sizeDelta;
                var amin = rt.anchorMin;
                var amax = rt.anchorMax;
                var piv = rt.pivot;
                sb.AppendLine($"anchoredPosition: ({ap.x:G},{ap.y:G})");
                sb.AppendLine($"sizeDelta: ({sd.x:G},{sd.y:G})");
                sb.AppendLine($"anchorMin: ({amin.x:G},{amin.y:G})");
                sb.AppendLine($"anchorMax: ({amax.x:G},{amax.y:G})");
                sb.AppendLine($"pivot: ({piv.x:G},{piv.y:G})");
            }
        }

        public static string ListComponents(string path)
        {
            var go = FindObject(path);
            if (go == null) return null;
            return ListComponentsInternal(go);
        }

        public static string ListComponentsById(string objectId)
        {
            var go = FindObjectById(objectId);
            if (go == null) return null;
            return ListComponentsInternal(go);
        }

        public static string ListComponents(int legacyId)
            => ListComponentsById(legacyId.ToString(CultureInfo.InvariantCulture));

        private static string ListComponentsInternal(GameObject go)
        {
            var components = go.GetComponents<Component>();
            var sb = new StringBuilder();
            foreach (var comp in components)
            {
                if (comp == null) continue;
                var name = comp.GetType().Name;
                if (name == "Transform") continue;
                if (sb.Length > 0) sb.AppendLine();
                sb.Append(name);
            }
            return sb.ToString();
        }

        private static void AppendPropertyValue(StringBuilder sb, SerializedProperty prop)
            => sb.Append(GetPropertyValueString(prop));

        internal static string GetPropertyValueString(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:   return prop.intValue.ToString();
                case SerializedPropertyType.Float:     return prop.floatValue.ToString("G4", CultureInfo.InvariantCulture);
                case SerializedPropertyType.Boolean:   return prop.boolValue ? "true" : "false";
                case SerializedPropertyType.String:    return prop.stringValue;
                case SerializedPropertyType.Enum:
                    return (prop.enumValueIndex >= 0 && prop.enumValueIndex < prop.enumNames.Length)
                        ? prop.enumNames[prop.enumValueIndex]
                        : prop.intValue.ToString();
                case SerializedPropertyType.Vector2:
                    var v2 = prop.vector2Value;
                    return $"({v2.x.ToString("G4", CultureInfo.InvariantCulture)}, {v2.y.ToString("G4", CultureInfo.InvariantCulture)})";
                case SerializedPropertyType.Vector3:
                    var v3 = prop.vector3Value;
                    return $"({v3.x.ToString("G4", CultureInfo.InvariantCulture)}, {v3.y.ToString("G4", CultureInfo.InvariantCulture)}, {v3.z.ToString("G4", CultureInfo.InvariantCulture)})";
                case SerializedPropertyType.Vector4:
                    var v4 = prop.vector4Value;
                    return $"({v4.x.ToString("G4", CultureInfo.InvariantCulture)}, {v4.y.ToString("G4", CultureInfo.InvariantCulture)}, {v4.z.ToString("G4", CultureInfo.InvariantCulture)}, {v4.w.ToString("G4", CultureInfo.InvariantCulture)})";
                case SerializedPropertyType.Vector2Int:
                    var v2i = prop.vector2IntValue;
                    return $"({v2i.x}, {v2i.y})";
                case SerializedPropertyType.Vector3Int:
                    var v3i = prop.vector3IntValue;
                    return $"({v3i.x}, {v3i.y}, {v3i.z})";
                case SerializedPropertyType.Color:
                    return $"#{ColorUtility.ToHtmlStringRGBA(prop.colorValue)}";
                case SerializedPropertyType.Quaternion:
                    var euler = prop.quaternionValue.eulerAngles;
                    return $"({euler.x.ToString("G4", CultureInfo.InvariantCulture)}, {euler.y.ToString("G4", CultureInfo.InvariantCulture)}, {euler.z.ToString("G4", CultureInfo.InvariantCulture)})";
                case SerializedPropertyType.ObjectReference:
                    if (prop.objectReferenceValue == null) return "null";
                    if (prop.objectReferenceValue is GameObject refGo)
                        return $"{GetPath(refGo)} {RefManager.AssignAny(refGo)}";
                    if (prop.objectReferenceValue is Component refComp)
                        return $"{GetPath(refComp.gameObject)}::{refComp.GetType().Name} {RefManager.AssignAny(refComp)}";
                    return $"{prop.objectReferenceValue.name} {RefManager.AssignAny(prop.objectReferenceValue)}";
                case SerializedPropertyType.LayerMask:
                    var lsb = new StringBuilder();
                    AppendLayerMask(lsb, prop.intValue);
                    return lsb.ToString();
                case SerializedPropertyType.ArraySize:
                    return prop.intValue.ToString();
                case SerializedPropertyType.Generic:
                    if (prop.isArray)
                    {
                        int count = System.Math.Min(prop.arraySize, 10);
                        if (count == 0) return "[]";
                        var asb = new StringBuilder("[");
                        for (int j = 0; j < count; j++)
                        {
                            if (j > 0) asb.Append(", ");
                            var elem = prop.GetArrayElementAtIndex(j);
                            if (elem.propertyType == SerializedPropertyType.Generic)
                            {
                                var inlined = TryInlineStruct(elem, 1);
                                asb.Append(inlined ?? "{...}");
                            }
                            else
                                asb.Append(GetPropertyValueString(elem));
                        }
                        if (prop.arraySize > 10) asb.Append($", ...+{prop.arraySize - 10}");
                        return asb.Append("]").ToString();
                    }
                    else
                    {
                        var calls = prop.FindPropertyRelative("m_PersistentCalls.m_Calls");
                        if (calls != null && calls.isArray)
                        {
                            var esb = new StringBuilder();
                            AppendUnityEvent(esb, calls);
                            return esb.ToString();
                        }
                        return TryInlineStruct(prop, 0) ?? $"<{prop.type}>";
                    }
                case SerializedPropertyType.Rect:
                {
                    var r = prop.rectValue;
                    return $"({r.x.ToString("G4", CultureInfo.InvariantCulture)}, {r.y.ToString("G4", CultureInfo.InvariantCulture)}, {r.width.ToString("G4", CultureInfo.InvariantCulture)}, {r.height.ToString("G4", CultureInfo.InvariantCulture)})";
                }
                case SerializedPropertyType.Bounds:
                {
                    var b = prop.boundsValue;
                    return $"({b.center.x.ToString("G4", CultureInfo.InvariantCulture)}, {b.center.y.ToString("G4", CultureInfo.InvariantCulture)}, {b.center.z.ToString("G4", CultureInfo.InvariantCulture)}, {b.size.x.ToString("G4", CultureInfo.InvariantCulture)}, {b.size.y.ToString("G4", CultureInfo.InvariantCulture)}, {b.size.z.ToString("G4", CultureInfo.InvariantCulture)})";
                }
                case SerializedPropertyType.RectInt:
                {
                    var r = prop.rectIntValue;
                    return $"({r.x}, {r.y}, {r.width}, {r.height})";
                }
                case SerializedPropertyType.BoundsInt:
                {
                    var b = prop.boundsIntValue;
                    return $"({b.position.x}, {b.position.y}, {b.position.z}, {b.size.x}, {b.size.y}, {b.size.z})";
                }
                default:
                    return $"<{prop.propertyType}>";
            }
        }

        private static void AppendUnityEvent(StringBuilder sb, SerializedProperty calls)
        {
            sb.Append($"UnityEvent[{calls.arraySize}]");
            if (calls.arraySize == 0) return;
            sb.Append(" -> ");
            for (int i = 0; i < calls.arraySize; i++)
            {
                if (i > 0) sb.Append(", ");
                var call = calls.GetArrayElementAtIndex(i);
                var target = call.FindPropertyRelative("m_Target")?.objectReferenceValue;
                var method = call.FindPropertyRelative("m_MethodName")?.stringValue ?? "?";
                var mode = call.FindPropertyRelative("m_Mode")?.enumValueIndex ?? 0;
                string tPath = target switch {
                    UnityEngine.Component tc   => GetPath(tc.gameObject),
                    UnityEngine.GameObject tgo => GetPath(tgo),
                    _                          => target != null ? target.name : "null"
                };
                sb.Append(tPath);
                sb.Append('.').Append(method).Append('(');
                var args = call.FindPropertyRelative("m_Arguments");
                switch (mode)
                {
                    case 1: sb.Append("void"); break;
                    case 3: sb.Append("int=").Append(args?.FindPropertyRelative("m_IntArgument")?.intValue ?? 0); break;
                    case 4: sb.Append("float=").Append((args?.FindPropertyRelative("m_FloatArgument")?.floatValue ?? 0f).ToString("G4", CultureInfo.InvariantCulture)); break;
                    case 5: sb.Append("string=").Append(args?.FindPropertyRelative("m_StringArgument")?.stringValue ?? ""); break;
                    case 6: sb.Append("bool=").Append(args?.FindPropertyRelative("m_BoolArgument")?.boolValue == true ? "True" : "False"); break;
                    case 2:
                    {
                        var objArg = args?.FindPropertyRelative("m_ObjectArgument")?.objectReferenceValue;
                        sb.Append("obj=").Append(objArg != null ? objArg.name : "null");
                        break;
                    }
                    default: sb.Append("void"); break;
                }
                sb.Append(')');
            }
        }

        private static void AppendLayerMask(StringBuilder sb, int mask)
        {
            bool first = true;
            for (int i = 0; i < 32; i++)
            {
                if ((mask & (1 << i)) != 0)
                {
                    var name = LayerMask.LayerToName(i);
                    if (string.IsNullOrEmpty(name)) continue;
                    if (!first) sb.Append(", ");
                    sb.Append(name);
                    first = false;
                }
            }
            if (first) sb.Append("None");
        }

        private const int MaxInlineFields = 8;

        private static string TryInlineStruct(SerializedProperty prop, int depth)
        {
            if (depth > 2) return null;

            var copy = prop.Copy();
            var end = prop.GetEndProperty();
            if (!copy.NextVisible(true) || SerializedProperty.EqualContents(copy, end))
                return null;

            var fields = new System.Collections.Generic.List<(string name, string val)>();
            do
            {
                if (SerializedProperty.EqualContents(copy, end)) break;
                fields.Add((copy.name, GetPropertyValueString(copy)));
                if (fields.Count >= MaxInlineFields) break;
            } while (copy.NextVisible(false));

            if (fields.Count == 0) return null;

            // Pretty HashId: exactly 2 fields, one string + one int
            if (fields.Count == 2)
            {
                var f0 = prop.FindPropertyRelative(fields[0].name);
                var f1 = prop.FindPropertyRelative(fields[1].name);
                if (f0 != null && f1 != null)
                {
                    SerializedProperty strProp = null, intProp = null;
                    if (f0.propertyType == SerializedPropertyType.String && f1.propertyType == SerializedPropertyType.Integer)
                    { strProp = f0; intProp = f1; }
                    else if (f1.propertyType == SerializedPropertyType.String && f0.propertyType == SerializedPropertyType.Integer)
                    { strProp = f1; intProp = f0; }
                    if (strProp != null)
                        return $"{strProp.stringValue} ({intProp.intValue})";
                }
            }

            var sb = new StringBuilder("{");
            for (int i = 0; i < fields.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(fields[i].name).Append('=').Append(fields[i].val);
            }
            return sb.Append("}").ToString();
        }
    }
}
