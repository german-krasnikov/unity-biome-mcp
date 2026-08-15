// UIFileHelper — UXML surgical edit methods (partial class).
// Loaded by EditUIFile dispatch: set-attr, add-class, remove-class, add-element, remove-element.
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using UnityEditor;

namespace UnityMCP.Editor
{
    public static partial class UIFileHelper
    {
        internal static string UxmlSetAttr(string abs, string path, string selector, string attr, string value)
        {
            var doc = XDocument.Load(abs);
            var el = FindByName(doc, selector);
            if (el == null) return $"err: no element with name '{selector}' in {path}. Available: {AllNames(doc)}";
            var current = el.Attribute(attr)?.Value;
            if (current == value) return $"no-op: attribute already has value \"{value}\"";
            BackupFile(abs);
            el.SetAttributeValue(attr, value);
            SaveXDocument(doc, abs);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var valErr = ValidateImportedUxml(abs, path);
            return valErr ?? $"ok: set-attr name={selector} attr={attr} value=\"{value}\"\npath: {path}\nvalidated: CloneTree OK";
        }

        internal static string UxmlAddClass(string abs, string path, string selector, string cssClass)
        {
            var doc = XDocument.Load(abs);
            var el = FindByName(doc, selector);
            if (el == null) return $"err: no element with name '{selector}' in {path}";
            var classes = (el.Attribute("class")?.Value ?? "").Split(' ').Where(c => c.Length > 0).ToList();
            if (classes.Contains(cssClass)) return $"no-op: class '{cssClass}' already present on '{selector}'";
            classes.Add(cssClass);
            BackupFile(abs);
            el.SetAttributeValue("class", string.Join(" ", classes));
            SaveXDocument(doc, abs);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return $"ok: add-class name={selector} class={cssClass}\npath: {path}";
        }

        internal static string UxmlRemoveClass(string abs, string path, string selector, string cssClass)
        {
            var doc = XDocument.Load(abs);
            var el = FindByName(doc, selector);
            if (el == null) return $"err: no element with name '{selector}' in {path}";
            var classes = (el.Attribute("class")?.Value ?? "").Split(' ').Where(c => c.Length > 0).ToList();
            if (!classes.Contains(cssClass)) return $"no-op: class '{cssClass}' not present on '{selector}'";
            classes.Remove(cssClass);
            BackupFile(abs);
            el.SetAttributeValue("class", string.Join(" ", classes));
            SaveXDocument(doc, abs);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return $"ok: remove-class name={selector} class={cssClass}\npath: {path}";
        }

        internal static string UxmlAddElement(string abs, string path, string parent, string tag, string attrs)
        {
            var doc = XDocument.Load(abs);
            var parentEl = FindByName(doc, parent) ?? doc.Root;
            if (parentEl == null) return $"err: no element with name '{parent}' in {path}";
            var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;
            var localTag = tag?.Contains(':') == true ? tag.Split(':')[1] : tag ?? "VisualElement";
            var newEl = new XElement(ns + localTag);
            if (!string.IsNullOrEmpty(attrs))
                foreach (var kv in attrs.Split(' '))
                {
                    var parts = kv.Split('=');
                    if (parts.Length == 2) newEl.SetAttributeValue(parts[0], parts[1].Trim('"'));
                }
            BackupFile(abs);
            parentEl.Add(newEl);
            SaveXDocument(doc, abs);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var valErr = ValidateImportedUxml(abs, path);
            return valErr ?? $"ok: add-element tag={tag} under parent={parent}\npath: {path}\nvalidated: CloneTree OK";
        }

        internal static string UxmlRemoveElement(string abs, string path, string selector)
        {
            var doc = XDocument.Load(abs);
            var el = FindByName(doc, selector);
            if (el == null) return $"no-op: no element with name '{selector}' found";
            BackupFile(abs);
            el.Remove();
            SaveXDocument(doc, abs);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return $"ok: remove-element name={selector}\npath: {path}";
        }

        // XDocument helpers

        private static XElement FindByName(XDocument doc, string selector) =>
            doc.Descendants().FirstOrDefault(e => e.Attribute("name")?.Value == selector);

        private static string AllNames(XDocument doc) =>
            string.Join(", ", doc.Descendants()
                .Select(e => e.Attribute("name")?.Value).Where(n => n != null));

        // C4: write XDocument without BOM using XmlWriter.
        private static void SaveXDocument(XDocument doc, string absPath)
        {
            var settings = new XmlWriterSettings { Indent = true, IndentChars = "    ", Encoding = JsonHelper.Utf8NoBom };
            using var writer = XmlWriter.Create(absPath, settings);
            doc.Save(writer);
        }
    }
}
