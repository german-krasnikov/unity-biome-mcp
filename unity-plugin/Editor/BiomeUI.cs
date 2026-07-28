using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Shared UI Toolkit building blocks for Biome editor surfaces.
    /// Static presentation belongs in USS; this class only standardizes structure and behavior.
    /// </summary>
    internal static class BiomeUI
    {
        internal const int MotionFastMs = 120;
        internal const int MotionNormalMs = 220;
        internal const int PageMotionMs = 280;

        internal static void LoadCoreStyles(VisualElement root, bool includeWizard = false)
        {
            AddStyle(root, "MCPHub.uss");
            AddStyle(root, "MCPSettings.uss");
            AddStyle(root, "ArcadeAnim.uss");
            if (includeWizard)
                AddStyle(root, "Wizard/SetupWizard.uss");
        }

        internal static Button PrimaryButton(string text, Action clicked, string tooltip = null) =>
            Button(text, clicked, "biome-button--primary", tooltip);

        internal static Button SecondaryButton(string text, Action clicked, string tooltip = null) =>
            Button(text, clicked, "biome-button--secondary", tooltip);

        internal static Button QuietButton(string text, Action clicked, string tooltip = null) =>
            Button(text, clicked, "biome-button--quiet", tooltip);

        internal static Button Button(string text, Action clicked, string modifierClass, string tooltip = null)
        {
            var button = new Button
            {
                text = text,
                tooltip = tooltip ?? string.Empty
            };
            if (clicked != null)
                button.clicked += clicked;
            button.AddToClassList("biome-button");
            if (!string.IsNullOrEmpty(modifierClass))
                button.AddToClassList(modifierClass);
            return button;
        }

        internal static VisualElement Section(string title, out VisualElement body)
        {
            var section = new VisualElement();
            section.AddToClassList("biome-section");

            if (!string.IsNullOrEmpty(title))
            {
                var heading = new Label(title);
                heading.AddToClassList("biome-section__title");
                section.Add(heading);
            }

            body = new VisualElement();
            body.AddToClassList("biome-section__body");
            section.Add(body);
            return section;
        }

        internal static Label StatusLabel(string text = null)
        {
            var label = new Label(text ?? string.Empty);
            label.AddToClassList("biome-status");
            return label;
        }

        internal static void SetStatus(Label label, string text, string state)
        {
            label.text = text;
            SetExclusiveClass(
                label,
                $"biome-status--{state}",
                "biome-status--neutral",
                "biome-status--success",
                "biome-status--warning",
                "biome-status--error");
        }

        internal static void SetExclusiveClass(
            VisualElement element,
            string activeClass,
            params string[] classes)
        {
            foreach (string className in classes)
                element.EnableInClassList(className, className == activeClass);
        }

        internal static void ShakeX(VisualElement element)
        {
            if (element.panel == null)
                element.usageHints |= UsageHints.DynamicTransform;

            float[] offsets = { -6f, 5f, -4f, 3f, 0f };
            for (int i = 0; i < offsets.Length; i++)
            {
                float offset = offsets[i];
                element.schedule.Execute(() =>
                    element.style.translate = new Translate(offset, 0f))
                    .StartingIn(i * 45);
            }
        }

        private static void AddStyle(VisualElement root, string path)
        {
            var styleSheet = MCPEditorUtils.LoadStyleSheet(path);
            if (styleSheet != null && !root.styleSheets.Contains(styleSheet))
                root.styleSheets.Add(styleSheet);
        }
    }
}
