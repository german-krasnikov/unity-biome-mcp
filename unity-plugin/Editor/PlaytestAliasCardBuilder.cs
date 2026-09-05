// Static card builder for Alias Manager — extracts BuildCard from PlaytestAliasWindow.
// Static-only class — exempt from 300-line limit.
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityMCP.Playtest.Core;

namespace UnityMCP.Editor
{
    internal static class PlaytestAliasCardBuilder
    {
        // Entry point called by PlaytestAliasWindow.Refresh()
        internal static VisualElement BuildCard(
            int idx,
            PlaytestConfig config,
            SerializedObject so,
            Action onChanged,
            Action<int> onDelete)
        {
            var card     = new VisualElement();
            var aliasRef = config.aliases[idx];
            var arrProp  = so.FindProperty("aliases").GetArrayElementAtIndex(idx);

            card.AddToClassList("alias-card");
            ApplyTypeClass(card, aliasRef.type);

            card.Add(BuildRow1(idx, aliasRef, arrProp, so, card, onChanged, onDelete));
            var row2Container = new VisualElement();
            row2Container.AddToClassList("alias-card-row2-wrap");
            RebuildRow2(row2Container, idx, aliasRef, arrProp, so, onChanged);
            card.Add(row2Container);
            return card;
        }

        // Row1: [TypeDropdown] [nameField] [statusDot] [Copy] [X]
        static VisualElement BuildRow1(int idx, QueryAlias aliasRef, SerializedProperty arrProp,
            SerializedObject so, VisualElement card, Action onChanged, Action<int> onDelete)
        {
            var row1     = new VisualElement(); row1.AddToClassList("alias-card-row1");
            var typeProp = arrProp.FindPropertyRelative("type");
            var typeDd   = new DropdownField(new List<string> { "VAL Path", "Constant", "VAR" }, Math.Clamp((int)aliasRef.type, 0, 2));
            typeDd.AddToClassList("alias-type-field");
            typeDd.RegisterValueChangedCallback(e => {
                typeProp.enumValueIndex = typeDd.index;
                so.ApplyModifiedProperties();
                ApplyTypeClass(card, (AliasType)typeDd.index);
                var wrap = card.Q<VisualElement>(className: "alias-card-row2-wrap");
                RebuildRow2(wrap, idx, aliasRef, arrProp, so, onChanged);
                onChanged?.Invoke();
            });
            var nameProp  = arrProp.FindPropertyRelative("alias");
            var nameField = MakeBoundTf(nameProp, so, "Alias Name", onChanged);
            nameField.AddToClassList("alias-name-field");
            var dot       = BuildStatusDot(aliasRef);
            var copyBtn   = new Button(() => GUIUtility.systemCopyBuffer = PlaytestAliasHelpers.FormatLine(aliasRef)) { text = "Copy" };
            copyBtn.AddToClassList("alias-copy-btn");
            var delBtn    = new Button(() => onDelete?.Invoke(idx)) { text = "X" };
            delBtn.AddToClassList("alias-delete-btn");
            var actions   = new VisualElement(); actions.AddToClassList("alias-card-actions");
            actions.Add(copyBtn); actions.Add(delBtn);
            row1.Add(typeDd); row1.Add(nameField); row1.Add(dot); row1.Add(actions);
            return row1;
        }

        static void RebuildRow2(VisualElement wrap, int idx, QueryAlias aliasRef,
            SerializedProperty arrProp, SerializedObject so, Action onChanged)
        {
            wrap.Clear();
            var row2 = aliasRef.type == AliasType.ValConst
                ? BuildRow2ValConst(arrProp, so, onChanged)
                : BuildRow2PathBased(aliasRef, arrProp, so, onChanged,
                    aliasRef.type == AliasType.VarRuntime ? "/path (runtime resolve)" : "/path/to/object");
            row2.AddToClassList("alias-card-row2");
            wrap.Add(row2);
        }

        // Row2 for ValPath and VarRuntime — path + comp dropdown + field dropdown + Pick
        static VisualElement BuildRow2PathBased(QueryAlias aliasRef, SerializedProperty arrProp,
            SerializedObject so, Action onChanged, string pathPlaceholder)
        {
            var row2      = new VisualElement();
            var pathProp  = arrProp.FindPropertyRelative("path");
            var compProp  = arrProp.FindPropertyRelative("component");
            var fieldProp = arrProp.FindPropertyRelative("field");

            var pathField = new TextField { value = pathProp.stringValue };
            pathField.AddToClassList("alias-path-field");
            if (string.IsNullOrEmpty(pathProp.stringValue))
                pathField.textEdition.placeholder = pathPlaceholder;

            var compDd  = new DropdownField { choices = new List<string>() };
            var fieldDd = new DropdownField { choices = new List<string>() };
            compDd.AddToClassList("alias-comp-field");
            fieldDd.AddToClassList("alias-field-field");
            compDd.SetEnabled(false); fieldDd.SetEnabled(false);

            PlaytestDropHelper.AttachDnD(pathField, (comp, go, path) => {
                pathProp.stringValue = path; so.ApplyModifiedProperties();
                RefreshCompDropdown(path, aliasRef.component, compDd, fieldDd, compProp, fieldProp, so);
                if (comp != null)
                    ShowAliasFieldPicker(comp, aliasRef, compProp, fieldProp, compDd, fieldDd, so, onChanged);
                else
                    ShowAliasCompPicker(go, aliasRef, compProp, fieldProp, compDd, fieldDd, so, onChanged, pathField);
                onChanged?.Invoke();
            });

            pathField.RegisterValueChangedCallback(e => {
                pathProp.stringValue = e.newValue; so.ApplyModifiedProperties();
                RefreshCompDropdown(e.newValue, "", compDd, fieldDd, compProp, fieldProp, so);
                onChanged?.Invoke();
            });

            compDd.RegisterValueChangedCallback(e => {
                compProp.stringValue = e.newValue ?? ""; so.ApplyModifiedProperties();
                RefreshFieldDropdown(aliasRef.path, e.newValue, aliasRef.field, fieldDd, fieldProp, so);
                onChanged?.Invoke();
            });

            fieldDd.RegisterValueChangedCallback(e => {
                fieldProp.stringValue = e.newValue ?? ""; so.ApplyModifiedProperties();
                onChanged?.Invoke();
            });

            RefreshCompDropdown(aliasRef.path, aliasRef.component, compDd, fieldDd, compProp, fieldProp, so);

            row2.Add(pathField); row2.Add(compDd); row2.Add(fieldDd);
            return row2;
        }

        // Row2 for ValConst — single wide constValue field
        static VisualElement BuildRow2ValConst(SerializedProperty arrProp, SerializedObject so, Action onChanged)
        {
            var row2       = new VisualElement();
            var constProp  = arrProp.FindPropertyRelative("constValue");
            var constField = MakeBoundTf(constProp, so, null, onChanged);
            constField.AddToClassList("alias-const-value-field");
            constField.textEdition.placeholder = "literal value";
            row2.Add(constField);
            return row2;
        }

        // 8px validation dot
        internal static VisualElement BuildStatusDot(QueryAlias a)
        {
            var dot = new VisualElement(); dot.AddToClassList("alias-status-dot");
            bool hasAlias   = !string.IsNullOrEmpty(a.alias);
            bool hasContent = a.type == AliasType.ValConst
                ? !string.IsNullOrEmpty(a.constValue)
                : !string.IsNullOrEmpty(a.path) && !string.IsNullOrEmpty(a.component) && !string.IsNullOrEmpty(a.field);
            string cls = !hasAlias    ? "alias-status-dot--empty"
                       : !hasContent ? "alias-status-dot--partial"
                       : "alias-status-dot--valid";
            dot.AddToClassList(cls);
            return dot;
        }

        static void RefreshCompDropdown(string path, string currentComp,
            DropdownField compDd, DropdownField fieldDd,
            SerializedProperty compProp, SerializedProperty fieldProp, SerializedObject so)
        {
            var go      = ResolveGo(path);
            var choices = go != null
                ? PlaytestDropHelper.GetUserComponents(go).ConvertAll(c => c.GetType().Name)
                : new List<string>();
            compDd.choices = choices; compDd.SetEnabled(choices.Count > 0);
            var val = choices.Contains(currentComp) ? currentComp : "";
            compDd.SetValueWithoutNotify(val);
            RefreshFieldDropdown(path, val, "", fieldDd, fieldProp, so);
        }

        static void RefreshFieldDropdown(string path, string compName, string currentField,
            DropdownField fieldDd, SerializedProperty fieldProp, SerializedObject so)
        {
            var go      = ResolveGo(path);
            var choices = new List<string>();
            if (go != null && !string.IsNullOrEmpty(compName))
            {
                var comp = go.GetComponent(compName);
                if (comp != null) choices = PlaytestDropHelper.GetMemberNames(comp.GetType());
            }
            fieldDd.choices = choices; fieldDd.SetEnabled(choices.Count > 0);
            var val = choices.Contains(currentField) ? currentField : "";
            fieldDd.SetValueWithoutNotify(val);
            if (val != currentField) { fieldProp.stringValue = val; so.ApplyModifiedProperties(); }
        }

        static void ShowAliasCompPicker(GameObject go, QueryAlias alias,
            SerializedProperty compProp, SerializedProperty fieldProp,
            DropdownField compDd, DropdownField fieldDd, SerializedObject so, Action onChanged,
            VisualElement anchor)
        {
            if (go == null) return;
            var tmp = new VisualStep();
            PlaytestDropHelper.ShowComponentPicker(go, tmp, StepType.Assert, () => {
                var parts = tmp.query?.Split('|') ?? System.Array.Empty<string>();
                alias.component = parts.Length > 1 ? parts[1] : "";
                alias.field     = parts.Length > 2 ? parts[2] : "";
                compProp.stringValue  = alias.component;
                fieldProp.stringValue = alias.field;
                so.ApplyModifiedProperties();
                RefreshCompDropdown(alias.path, alias.component, compDd, fieldDd, compProp, fieldProp, so);
                onChanged?.Invoke();
            }, anchor);
        }

        static void ShowAliasFieldPicker(Component comp, QueryAlias alias,
            SerializedProperty compProp, SerializedProperty fieldProp,
            DropdownField compDd, DropdownField fieldDd, SerializedObject so, Action onChanged)
        {
            alias.component  = comp.GetType().Name;
            compProp.stringValue = alias.component; so.ApplyModifiedProperties();
            var tmp = new VisualStep();
            PlaytestDropHelper.ShowFieldPicker(comp, tmp, StepType.Assert, () => {
                var parts = tmp.query?.Split('|') ?? System.Array.Empty<string>();
                alias.field = parts.Length > 2 ? parts[2] : "";
                fieldProp.stringValue = alias.field; so.ApplyModifiedProperties();
                RefreshCompDropdown(alias.path, alias.component, compDd, fieldDd, compProp, fieldProp, so);
                onChanged?.Invoke();
            });
        }

        static GameObject ResolveGo(string path) =>
            string.IsNullOrEmpty(path) ? null : ComponentSerializer.FindObject(path);

        static void ApplyTypeClass(VisualElement card, AliasType t)
        {
            card.RemoveFromClassList("alias-card--val-path");
            card.RemoveFromClassList("alias-card--val-const");
            card.RemoveFromClassList("alias-card--var");
            card.AddToClassList(CardTypeClass(t));
        }

        internal static string CardTypeClass(AliasType t) => t switch {
            AliasType.ValConst   => "alias-card--val-const",
            AliasType.VarRuntime => "alias-card--var",
            _                    => "alias-card--val-path",
        };

        static TextField MakeBoundTf(SerializedProperty prop, SerializedObject so, string label, Action onChanged)
        {
            var tf = label != null ? new TextField(label) { value = prop.stringValue }
                                   : new TextField { value = prop.stringValue };
            tf.RegisterValueChangedCallback(e => {
                prop.stringValue = e.newValue; so.ApplyModifiedProperties();
                onChanged?.Invoke();
            });
            return tf;
        }
    }
}
