using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace UnityMCP.Editor
{
    internal static partial class ObjectManager
    {
        public static string SetProperty(string path, string component, string prop, string value, bool dryRun = false)
        {
            // Intercept "active" / "m_IsActive" BEFORE ResolveComponent
            var normProp = prop.ToLowerInvariant();
            if (normProp is "active" or "m_isactive" or "isactive")
            {
                if (dryRun)
                {
                    var go = ComponentSerializer.FindObjectOrThrow(path);
                    return $"DRY-RUN: active would change {go.activeSelf} → {value}";
                }
                bool active = value is "true" or "True" or "1";
                return SetActive(path, active);
            }

            // Intercept "layer" / "m_layer" — maps to GameObject.layer (int)
            if (normProp is "layer" or "m_layer")
            {
                var go = ComponentSerializer.FindObjectOrThrow(path);
                if (dryRun)
                    return $"DRY-RUN: layer would change {go.layer} → {value}";
                int layerIdx;
                if (!int.TryParse(value, out layerIdx))
                {
                    layerIdx = LayerMask.NameToLayer(value);
                    if (layerIdx < 0)
                        throw new ArgumentException($"Invalid layer: '{value}'. Use layer index (int) or layer name.");
                }
                Undo.RecordObject(go, "Set Layer");
                go.layer = layerIdx;
                if (!EditorApplication.isPlaying && go.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(go.scene);
                return go.layer.ToString();
            }

            var (_, comp) = ResolveComponent(path, component);

            var so = new SerializedObject(comp);
            prop = InputNormalizer.NormalizeProperty(prop, so);
            value = InputNormalizer.NormalizeValue(value);
            var property = so.FindProperty(prop);

            if (property == null)
                throw new ArgumentException(ErrorHelper.PropertyNotFound(prop, component, path));

            if (dryRun)
            {
                var current = ComponentSerializer.GetPropertyValueString(property);
                return $"DRY-RUN: {prop} would change {current} → {value}";
            }

            Undo.RecordObject(comp, $"Set {prop}");
            // Handle arrays: comma-separated paths/values
            if (property.isArray && property.propertyType == SerializedPropertyType.Generic)
            {
                var items = ValueParser.SplitArrayValues(value);
                if (items.Length == 0)
                {
                    property.arraySize = 0;
                }
                else
                {
                    property.arraySize = items.Length;
                    for (int i = 0; i < items.Length; i++)
                    {
                        var elem = property.GetArrayElementAtIndex(i);
                        ValueParser.SetPropertyValue(elem, items[i]);
                    }
                }
            }
            else
            {
                ValueParser.SetPropertyValue(property, value);
            }
            so.ApplyModifiedProperties();
            if (!EditorApplication.isPlaying && comp.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(comp.gameObject.scene);
            ApplyNativePhysicsProperty(comp, prop, value);
            if (comp is Transform && !EditorApplication.isPlaying && !BatchHelper.InBatch)
            {
                Physics.SyncTransforms();
                Physics2D.SyncTransforms();
            }
            var readProp = so.FindProperty(prop);
            return readProp != null ? ComponentSerializer.GetPropertyValueString(readProp) : value;
        }

        // Unity 6: Rigidbody damping properties don't auto-sync to the native PhysX body
        // via SerializedObject alone — must also set via C# API.
        private static void ApplyNativePhysicsProperty(Component comp, string prop, string value)
        {
            if (comp is not Rigidbody rb) return;
            if (!float.TryParse(value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var f)) return;
            switch (prop)
            {
                case "m_Drag":           rb.linearDamping  = f; break;
                case "m_AngularDrag":    rb.angularDamping = f; break;
                case "m_Mass":           rb.mass           = f; break;
            }
        }

        public static string SetPropertyDelta(string path, string component, string prop, string delta)
        {
            var (_, comp) = ResolveComponent(path, component);
            Undo.RecordObject(comp, $"Delta {prop}");
            var so = new SerializedObject(comp);
            prop = InputNormalizer.NormalizeProperty(prop, so);
            var property = so.FindProperty(prop);
            if (property == null)
                throw new ArgumentException(ErrorHelper.PropertyNotFound(prop, component, path));

            var oldStr = ComponentSerializer.GetPropertyValueString(property);

            switch (property.propertyType)
            {
                case SerializedPropertyType.Float:
                {
                    var d = float.Parse(delta.TrimStart('+'), System.Globalization.CultureInfo.InvariantCulture);
                    property.floatValue += d;
                    break;
                }
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.LayerMask:
                {
                    var d = int.Parse(delta.TrimStart('+'));
                    property.intValue += d;
                    break;
                }
                case SerializedPropertyType.Vector3:
                {
                    var parts = delta.Trim('(', ')').Split(',');
                    var dx = float.Parse(parts[0].Trim().TrimStart('+'), System.Globalization.CultureInfo.InvariantCulture);
                    var dy = float.Parse(parts[1].Trim().TrimStart('+'), System.Globalization.CultureInfo.InvariantCulture);
                    var dz = float.Parse(parts[2].Trim().TrimStart('+'), System.Globalization.CultureInfo.InvariantCulture);
                    property.vector3Value += new UnityEngine.Vector3(dx, dy, dz);
                    break;
                }
                default:
                    throw new ArgumentException($"set_property_delta: unsupported type {property.propertyType}");
            }

            so.ApplyModifiedProperties();
            if (!EditorApplication.isPlaying && comp.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(comp.gameObject.scene);
            if (comp is Transform && !EditorApplication.isPlaying && !BatchHelper.InBatch)
            {
                Physics.SyncTransforms();
                Physics2D.SyncTransforms();
            }
            var newStr = ComponentSerializer.GetPropertyValueString(so.FindProperty(prop));
            return $"{oldStr} → {newStr}";
        }
    }
}
