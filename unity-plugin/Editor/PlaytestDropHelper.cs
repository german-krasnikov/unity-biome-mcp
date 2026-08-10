using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    internal static class PlaytestDropHelper
    {
        internal static readonly HashSet<Type> _baseTypes = new HashSet<Type>
        {
            typeof(UnityEngine.Object), typeof(Component), typeof(MonoBehaviour),
            typeof(Behaviour), typeof(object)
        };

        internal static IEnumerable<FieldInfo> GetFilteredFields(Type t)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            foreach (var f in t.GetFields(flags))
            {
                if (_baseTypes.Contains(f.DeclaringType)) continue;
                if (!f.IsPublic && f.GetCustomAttribute<SerializeField>() == null) continue;
                yield return f;
            }
        }

        // Public fields + readable non-indexed properties (excludes base Unity types).
        internal static List<string> GetMemberNames(Type t)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            var list = new List<string>();
            foreach (var f in GetFilteredFields(t))
                list.Add(f.Name);
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (_baseTypes.Contains(p.DeclaringType) || !p.CanRead || p.GetIndexParameters().Length > 0)
                    continue;
                list.Add(p.Name);
            }
            return list;
        }

        // Zero-arg public instance methods (excludes special names and base Unity types).
        internal static List<string> GetZeroArgMethodNames(Type t)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            return
            t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
             .Where(m => !_baseTypes.Contains(m.DeclaringType)
                      && !m.IsSpecialName
                      && m.GetParameters().Length == 0)
             .Select(m => m.Name)
             .ToList();
        }

        internal static string BuildQuery(string path, string comp, string member)
            => $"{path}|{comp}|{member}";

        internal static void ShowFieldPicker(Component comp, VisualStep step, StepType context, Action onDone = null, VisualElement anchor = null)
        {
            var path     = ComponentSerializer.GetPath(comp.gameObject);
            var compName = comp.GetType().Name;
            var menu     = new GenericDropdownMenu();

            foreach (var f in GetFilteredFields(comp.GetType()))
            {
                var fn = f.Name;
                menu.AddItem($"Fields/{fn}", false,
                    () => { ApplyMember(step, context, path, compName, fn, comp); onDone?.Invoke(); });
            }

            foreach (var p in comp.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (_baseTypes.Contains(p.DeclaringType) || !p.CanRead || p.GetIndexParameters().Length > 0) continue;
                var pn = p.Name;
                menu.AddItem($"Properties/{pn}", false,
                    () => { ApplyMember(step, context, path, compName, pn, comp); onDone?.Invoke(); });
            }

            foreach (var m in comp.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (_baseTypes.Contains(m.DeclaringType) || m.IsSpecialName || m.GetParameters().Length != 0) continue;
                var mn = m.Name + "()";
                menu.AddItem($"Methods/{mn}", false,
                    () => { ApplyMember(step, context, path, compName, mn, comp); onDone?.Invoke(); });
            }

            ShowDropdown(menu, anchor);
        }

        internal static List<Component> GetUserComponents(GameObject go)
        {
            var result = new List<Component>();
            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp == null) continue;
                var t = comp.GetType();
                if (_baseTypes.Contains(t) || t == typeof(Transform) || t == typeof(RectTransform)) continue;
                result.Add(comp);
            }
            return result;
        }

        internal static void ShowComponentPicker(GameObject go, VisualStep step, StepType context, Action onDone = null, VisualElement anchor = null)
        {
            var menu     = new GenericDropdownMenu();
            var filtered = GetUserComponents(go);
            var source   = filtered.Count > 0 ? filtered : new List<Component>(go.GetComponents<Component>());
            foreach (var comp in source)
            {
                if (comp == null) continue;
                var c = comp;
                menu.AddItem(c.GetType().Name, false, () => ShowFieldPicker(c, step, context, onDone, anchor));
            }
            ShowDropdown(menu, anchor);
        }

        static void ShowDropdown(GenericDropdownMenu menu, VisualElement anchor)
        {
            if (anchor != null)
                menu.DropDown(anchor.worldBound, anchor, false);
            else
                menu.DropDown(new Rect(Event.current?.mousePosition ?? Vector2.zero, Vector2.zero), null, false);
        }

        internal static void AttachDnD(TextField field, Action<Component, GameObject, string> onDrop)
        {
            field.RegisterCallback<DragUpdatedEvent>(_ => {
                DragAndDrop.visualMode = DragAndDropVisualMode.Link;
                field.AddToClassList("step-dnd-highlight");
            });
            field.RegisterCallback<DragPerformEvent>(evt => {
                DragAndDrop.AcceptDrag();
                evt.StopPropagation();
                field.RemoveFromClassList("step-dnd-highlight");
                foreach (var obj in DragAndDrop.objectReferences)
                {
                    if (obj is Component comp) { onDrop(comp, comp.gameObject, ComponentSerializer.GetPath(comp.gameObject)); return; }
                    if (obj is GameObject go)  { onDrop(null, go, ComponentSerializer.GetPath(go)); return; }
                }
            });
            field.RegisterCallback<DragLeaveEvent>(_ =>
                field.RemoveFromClassList("step-dnd-highlight"));
        }

        internal static void AttachMultiDnD(VisualElement target, Action<List<(Component, GameObject, string)>> onDrop)
        {
            target.RegisterCallback<DragUpdatedEvent>(_ =>
                DragAndDrop.visualMode = DragAndDropVisualMode.Link);
            target.RegisterCallback<DragPerformEvent>(_ => {
                DragAndDrop.AcceptDrag();
                var hits = new List<(Component, GameObject, string)>();
                foreach (var obj in DragAndDrop.objectReferences)
                {
                    if (obj is Component c) hits.Add((c, c.gameObject, ComponentSerializer.GetPath(c.gameObject)));
                    else if (obj is GameObject go) hits.Add((null, go, ComponentSerializer.GetPath(go)));
                }
                if (hits.Count > 0) onDrop(hits);
            });
        }

        internal static void ApplyMember(VisualStep step, StepType context, string path, string compName, string member, Component comp)
        {
            if (context == StepType.Invoke)
            {
                step.path      = path;
                step.component = compName;
                step.method    = member.EndsWith("()") ? member.Substring(0, member.Length - 2) : member;
            }
            else if (context == StepType.Set)
            {
                step.path      = path;
                step.component = compName;
                step.method    = member.EndsWith("()") ? member.Substring(0, member.Length - 2) : member;
                try { step.args = RuntimeHelper.ReadFieldInternal(comp, member); }
                catch (Exception e) { Debug.LogWarning($"{BiomeLabel.Tag} Pre-fill failed for {member}: {e.Message}"); }
            }
            else
            {
                step.query = BuildQuery(path, compName, member);
                try { step.value = RuntimeHelper.ReadFieldInternal(comp, member); }
                catch (Exception e) { Debug.LogWarning($"{BiomeLabel.Tag} Pre-fill failed for {member}: {e.Message}"); }
            }
        }
    }
}
