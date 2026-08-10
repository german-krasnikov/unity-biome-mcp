using System;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class MenuHelper
    {
        private static readonly MethodInfo ExtractSubmenusMethod;
        private static readonly MethodInfo MenuItemExistsMethod;

        static MenuHelper()
        {
            var menuType = typeof(Menu);
            ExtractSubmenusMethod = menuType.GetMethod("ExtractSubmenus",
                BindingFlags.Static | BindingFlags.NonPublic);
            MenuItemExistsMethod = menuType.GetMethod("MenuItemExists",
                BindingFlags.Static | BindingFlags.NonPublic);

            if (ExtractSubmenusMethod == null)
                Debug.LogWarning($"{BiomeLabel.Tag} Menu.ExtractSubmenus not found — menu list unavailable");
            if (MenuItemExistsMethod == null)
                Debug.LogWarning($"{BiomeLabel.Tag} Menu.MenuItemExists not found — using fallback validation");
        }

        public static string Execute(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("path is required");

            // Validate existence
            if (MenuItemExistsMethod != null)
            {
                if (!(bool)MenuItemExistsMethod.Invoke(null, new object[] { path }))
                    throw new ArgumentException($"Menu item not found: {path}");

                if (!Menu.GetEnabled(path))
                    throw new ArgumentException($"Menu item disabled: {path}");
            }

            var success = EditorApplication.ExecuteMenuItem(path);
            if (!success)
                throw new ArgumentException(
                    $"Failed to execute: {path}. Note: Edit/ menu items are not supported by Unity API.");

            return $"Executed: {path}";
        }

        public static string List(string path)
        {
            if (ExtractSubmenusMethod == null)
                throw new InvalidOperationException("Menu.ExtractSubmenus not available in this Unity version");

            if (string.IsNullOrEmpty(path))
                return ListRoots();

            var items = GetSubmenus(path);
            if (items == null || items.Length == 0)
                return $"No menu items under: {path}";

            var sb = new StringBuilder();
            sb.AppendLine($"[{path}] ({items.Length} items)");
            foreach (var item in items)
                sb.AppendLine(item);
            return sb.ToString().TrimEnd();
        }

        private static string ListRoots()
        {
            var sb = new StringBuilder();
            var roots = new[] { "File", "Edit", "Assets", "GameObject", "Component", "Window", "Help", "Tools" };
            foreach (var root in roots)
            {
                var subs = GetSubmenus(root);
                if (subs != null && subs.Length > 0)
                {
                    if (sb.Length > 0) sb.AppendLine();
                    sb.Append($"[{root}] ({subs.Length} items)");
                }
            }
            return sb.Length > 0 ? sb.ToString() : "No root menus found";
        }

        private static string[] GetSubmenus(string menuPath)
        {
            return (string[])ExtractSubmenusMethod.Invoke(null, new object[] { menuPath });
        }
    }
}
