// Partial MCPChatWindow — clipboard paste interception for image data.
// Intercepts ExecuteCommandEvent("Paste") on the chip input field.
// If clipboard has image bytes, inserts an image chip instead of text.
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    public partial class MCPChatWindow
    {
        // Overridable in tests via reflection or subclassing.
        internal System.Func<byte[]> ClipboardReader = ClipboardImageReader.TryRead;

        /// <summary>Wire paste interception on _chipField. Called from CreateGUI.</summary>
        internal void WireClipboardPaste()
        {
            if (_chipField == null) return;
            _chipField.RegisterCallback<ExecuteCommandEvent>(OnExecuteCommand, TrickleDown.TrickleDown);
        }

        private void OnExecuteCommand(ExecuteCommandEvent e)
        {
            if (e.commandName != "Paste") return;
            var bytes = ClipboardReader?.Invoke();
            if (bytes == null || bytes.Length == 0) return; // no image → let default text paste proceed

            e.StopImmediatePropagation();
            var dest = ImageAttachmentStore.ImportBytes(bytes, baseName: "paste");
            InsertInlineChip(null, dest, "paste.png");
        }
    }
}
