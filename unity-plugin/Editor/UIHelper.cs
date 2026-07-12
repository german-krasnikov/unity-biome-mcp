using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnityMCP.Editor
{
    public static class UIHelper
    {
        private static readonly Dictionary<string, (Vector2 min, Vector2 max, Vector2 pivot)> Presets =
            new Dictionary<string, (Vector2, Vector2, Vector2)>(StringComparer.OrdinalIgnoreCase)
            {
                ["stretch"]        = (new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f)),
                ["center"]         = (new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)),
                ["top-left"]       = (new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1)),
                ["top-center"]     = (new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1)),
                ["top-right"]      = (new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1)),
                ["middle-left"]    = (new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f)),
                ["middle-right"]   = (new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f)),
                ["bottom-left"]    = (new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0)),
                ["bottom-center"]  = (new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0)),
                ["bottom-right"]   = (new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0)),
                ["top-stretch"]    = (new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1)),
                ["bottom-stretch"] = (new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0)),
                ["left-stretch"]   = (new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f)),
                ["right-stretch"]  = (new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f)),
            };

        public static string CreateUI(string type, string name, string parent,
            string anchor, string pos, string size, string pivot,
            string color, string text, string font_size)
        {
            if (string.IsNullOrEmpty(type))
                throw new ArgumentException("type is required");

            if (string.IsNullOrEmpty(name)) name = type;

            switch (type.ToLower())
            {
                case "canvas":  return CreateCanvas(name);
                case "panel":   return CreateElement(name, parent, anchor ?? "stretch", pos, size, pivot, color, "Image");
                case "button":  return CreateButton(name, parent, anchor ?? "center", pos, size ?? "(160,30)", pivot, color, text, font_size);
                case "text":    return CreateText(name, parent, anchor ?? "center", pos, size ?? "(200,50)", pivot, color, text, font_size);
                case "image":   return CreateImage(name, parent, anchor ?? "center", pos, size ?? "(100,100)", pivot, color);
                default:
                    throw new ArgumentException($"Unknown UI type '{type}'. Valid: Canvas, Panel, Button, Text, Image.");
            }
        }

        public static string SetRect(string path, string anchor, string pos, string size,
            string pivot, string offset_min, string offset_max)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("path is required");

            var go = ComponentSerializer.FindObject(path);
            if (go == null)
                throw new ArgumentException(ErrorHelper.ObjectNotFound(path));

            var rt = go.GetComponent<RectTransform>();
            if (rt == null)
                throw new ArgumentException($"No RectTransform on '{path}'. Use create_ui to create UI elements.");

            Undo.RecordObject(rt, "SetRect");
            ApplyRect(rt, anchor, pos, size, pivot, offset_min, offset_max);
            return $"rect:{path} updated";
        }

        // --- Private helpers ---

        private static string CreateCanvas(string name)
        {
            var go = MakeCanvas(name);
            Undo.RegisterCreatedObjectUndo(go, $"Create UI {name}");
            var path = ComponentSerializer.GetPath(go);
            return $"Created {path}\n{HierarchySerializer.SerializeSubtree(go)}";
        }

        private static string CreateElement(string name, string parent, string anchor,
            string pos, string size, string pivot, string color, string componentType)
        {
            var parentGo = ResolveParent(parent);
            var go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, $"Create UI {name}");
            go.transform.SetParent(parentGo.transform, false);

            if (componentType == "Image")
                Undo.AddComponent<Image>(go);

            var rt = go.GetComponent<RectTransform>();
            ApplyRect(rt, anchor, pos, size, pivot, null, null);

            if (!string.IsNullOrEmpty(color))
            {
                var img = go.GetComponent<Image>();
                if (img != null) img.color = ValueParser.ParseColor(color);
            }

            return FormatCreated(go);
        }

        private static string CreateButton(string name, string parent, string anchor,
            string pos, string size, string pivot, string color, string text, string font_size)
        {
            var parentGo = ResolveParent(parent);
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            Undo.RegisterCreatedObjectUndo(go, $"Create UI {name}");
            go.transform.SetParent(parentGo.transform, false);

            var rt = go.GetComponent<RectTransform>();
            ApplyRect(rt, anchor, pos, size, pivot, null, null);

            if (!string.IsNullOrEmpty(color))
                go.GetComponent<Image>().color = ValueParser.ParseColor(color);

            // Child text
            var textGo = new GameObject("Text", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(textGo, $"Create UI {name}/Text");
            textGo.transform.SetParent(go.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            ApplyRect(textRt, "stretch", null, null, null, null, null);

            AddTextComponent(textGo, text ?? name, font_size, null);

            return FormatCreated(go);
        }

        private static string CreateText(string name, string parent, string anchor,
            string pos, string size, string pivot, string color, string text, string font_size)
        {
            var parentGo = ResolveParent(parent);
            var go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, $"Create UI {name}");
            go.transform.SetParent(parentGo.transform, false);

            var rt = go.GetComponent<RectTransform>();
            ApplyRect(rt, anchor, pos, size, pivot, null, null);

            AddTextComponent(go, text ?? name, font_size, color);

            return FormatCreated(go);
        }

        private static string CreateImage(string name, string parent, string anchor,
            string pos, string size, string pivot, string color)
        {
            return CreateElement(name, parent, anchor, pos, size, pivot, color, "Image");
        }

        private static GameObject ResolveParent(string parent)
        {
            if (!string.IsNullOrEmpty(parent))
            {
                var go = ComponentSerializer.FindObject(parent);
                if (go == null)
                    throw new ArgumentException(ErrorHelper.ObjectNotFound(parent));
                return go;
            }
            // Auto-Canvas: find or create
            return FindOrCreateCanvas();
        }

        private static GameObject FindOrCreateCanvas()
        {
            var existing = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            if (existing != null) return existing.gameObject;

            var go = MakeCanvas("Canvas");
            Undo.RegisterCreatedObjectUndo(go, "Auto-create Canvas");
            return go;
        }

        private static GameObject MakeCanvas(string name)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            EnsureEventSystem();
            return go;
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
        }

        private static void AddTextComponent(GameObject go, string text, string font_size, string color)
        {
            // Try TMPro first
            var tmpType = FindTMPTextType();
            if (tmpType != null)
            {
                var comp = Undo.AddComponent(go, tmpType);
                SetTMPText(comp, text, font_size, color);
                return;
            }
            // Fallback to legacy Text
            var t = Undo.AddComponent<Text>(go);
            t.text = text ?? "";
            t.alignment = TextAnchor.MiddleCenter;
            if (!string.IsNullOrEmpty(font_size))
                t.fontSize = int.Parse(font_size);
            if (!string.IsNullOrEmpty(color))
                t.color = ValueParser.ParseColor(color);
        }

        private static Type FindTMPTextType()
        {
            // Search for TextMeshProUGUI across all loaded assemblies
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = assembly.GetType("TMPro.TextMeshProUGUI");
                if (t != null) return t;
            }
            return null;
        }

        private static void SetTMPText(Component comp, string text, string font_size, string color)
        {
            var type = comp.GetType();
            EnsureTMPFont(comp, type);
            TrySetProp(comp, type, "text", text ?? "");
            TrySetProp(comp, type, "alignment", 514); // TextAlignmentOptions.Center
            TrySetProp(comp, type, "isOrthographic", true);
            if (!string.IsNullOrEmpty(font_size) && int.TryParse(font_size, out var fs))
                TrySetProp(comp, type, "fontSize", (float)fs);
            if (!string.IsNullOrEmpty(color))
                TrySetProp(comp, type, "color", ValueParser.ParseColor(color));
        }

        private static void TrySetProp(Component comp, System.Type type, string name, object value)
        {
            try { type.GetProperty(name)?.SetValue(comp, value); }
            catch (System.Reflection.TargetInvocationException) { /* TMP styling is cosmetic */ }
        }

        private static void EnsureTMPFont(Component comp, Type textType)
        {
            var fontProp = textType.GetProperty("font");
            if (fontProp == null || fontProp.GetValue(comp) != null) return;

            // Try TMP_Settings.defaultFontAsset
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var st = asm.GetType("TMPro.TMP_Settings");
                if (st == null) continue;
                var defFont = st.GetProperty("defaultFontAsset", BindingFlags.Static | BindingFlags.Public)?.GetValue(null);
                if (defFont != null) { fontProp.SetValue(comp, defFont); return; }
                break;
            }

            // Fallback: search for any TMP_FontAsset in project
            var guids = AssetDatabase.FindAssets($"t:{fontProp.PropertyType.Name}");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                var font = AssetDatabase.LoadAssetAtPath(path, fontProp.PropertyType);
                if (font != null) fontProp.SetValue(comp, font);
            }
        }

        private static void ApplyRect(RectTransform rt, string anchor, string pos, string size,
            string pivot, string offset_min, string offset_max)
        {
            if (!string.IsNullOrEmpty(anchor))
            {
                if (!Presets.TryGetValue(anchor, out var preset))
                    throw new ArgumentException($"Unknown anchor '{anchor}'. Valid: {string.Join(", ", Presets.Keys)}.");
                rt.anchorMin = preset.min;
                rt.anchorMax = preset.max;
                rt.pivot = preset.pivot;
            }

            if (!string.IsNullOrEmpty(pivot))
                rt.pivot = ValueParser.ParseVector2(pivot);
            if (!string.IsNullOrEmpty(pos))
                rt.anchoredPosition = ValueParser.ParseVector2(pos);
            if (!string.IsNullOrEmpty(size))
                rt.sizeDelta = ValueParser.ParseVector2(size);
            if (!string.IsNullOrEmpty(offset_min))
                rt.offsetMin = ValueParser.ParseVector2(offset_min);
            if (!string.IsNullOrEmpty(offset_max))
                rt.offsetMax = ValueParser.ParseVector2(offset_max);
        }

        private static string FormatCreated(GameObject go)
        {
            var path = ComponentSerializer.GetPath(go);
            var parent = go.transform.parent?.gameObject;
            if (parent != null)
                return $"Created {path}\n--- parent ---\n{HierarchySerializer.SerializeSubtree(parent)}";
            return $"Created {path}\n{HierarchySerializer.SerializeSubtree(go)}";
        }
    }
}
