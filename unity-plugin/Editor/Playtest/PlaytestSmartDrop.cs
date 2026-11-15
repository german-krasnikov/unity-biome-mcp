using System;
using UnityEngine;
using UnityEngine.UIElements;
using UnityMCP.Playtest.Core;

namespace UnityMCP.Editor
{
    internal static class PlaytestSmartDrop
    {
        internal static VisualStep CreateStep(GameObject go, StepType type)
        {
            var path = ComponentSerializer.GetPath(go);
            return type is StepType.Move or StepType.Teleport
                ? new VisualStep { type = type, path = path, position = go.transform.position }
                : new VisualStep { type = type, path = path };
        }

        internal static void ShowActionMenu(GameObject go, Action<VisualStep> onCreated, Action onFinished, VisualElement anchor = null)
        {
            var menu = new GenericDropdownMenu();
            var name = go.name;
            menu.AddItem($"Move '{name}'", false,
                () => onCreated(CreateStep(go, StepType.Move)));
            menu.AddItem($"Teleport '{name}'", false,
                () => onCreated(CreateStep(go, StepType.Teleport)));
            menu.AddSeparator("");
            menu.AddItem($"Assert field on '{name}'", false, () => {
                var step = CreateStep(go, StepType.Assert);
                onCreated(step);
                PlaytestDropHelper.ShowComponentPicker(go, step, StepType.Assert, onFinished, anchor);
            });
            menu.AddItem($"WaitUntil field on '{name}'", false, () => {
                var step = CreateStep(go, StepType.WaitUntil);
                step.timeout = 10f;
                onCreated(step);
                PlaytestDropHelper.ShowComponentPicker(go, step, StepType.WaitUntil, onFinished, anchor);
            });
            menu.AddItem($"Invoke method on '{name}'", false, () => {
                var step = CreateStep(go, StepType.Invoke);
                onCreated(step);
                PlaytestDropHelper.ShowComponentPicker(go, step, StepType.Invoke, onFinished, anchor);
            });
            menu.AddItem($"Monitor field on '{name}'", false, () => {
                var step = CreateStep(go, StepType.Monitor);
                onCreated(step);
                PlaytestDropHelper.ShowComponentPicker(go, step, StepType.Monitor, onFinished, anchor);
            });
            menu.AddSeparator("");
            menu.AddItem($"Set field on '{name}'", false, () => {
                var step = CreateStep(go, StepType.Set);
                onCreated(step);
                PlaytestDropHelper.ShowComponentPicker(go, step, StepType.Set, onFinished, anchor);
            });
            menu.AddItem($"Click '{name}'", false, () => onCreated(CreateStep(go, StepType.Click)));
            menu.AddItem($"Capture field on '{name}'", false, () => {
                var step = CreateStep(go, StepType.Capture);
                onCreated(step);
                PlaytestDropHelper.ShowComponentPicker(go, step, StepType.Capture, onFinished, anchor);
            });
            menu.AddItem($"AssertNear '{name}'", false, () => onCreated(CreateStep(go, StepType.AssertNear)));

            if (anchor != null)
                menu.DropDown(anchor.worldBound, anchor, false);
            else
                menu.DropDown(new Rect(Event.current?.mousePosition ?? Vector2.zero, Vector2.zero), null, false);
        }
    }
}
