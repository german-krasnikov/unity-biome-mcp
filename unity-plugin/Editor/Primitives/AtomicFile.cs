using System.IO;

namespace UnityMCP.Editor
{
    // Shared tmp+swap primitive: write content to a sibling ".tmp" path, then call
    // Swap to move it into place. File.Replace has no intermediate state where the
    // path is missing, so a sharing violation on the original (AV scan, sync
    // client, another process) between delete and move can never permanently lose
    // it — the same fix WizardConfigWriter.WriteAtomic proved for config files
    // (C1 r5 #2), reused here so PortResolver's port-file writers get it too
    // (C1 r6 #1). tmp is always cleaned up, even on failure, without masking the
    // original Replace/Move exception.
    internal static class AtomicFile
    {
        internal static void Swap(string tmp, string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Replace(tmp, path, null);
                else
                    File.Move(tmp, path);
            }
            finally
            {
                try
                {
                    if (File.Exists(tmp)) File.Delete(tmp);
                }
                catch
                {
                    // tmp cleanup must not mask the original Replace/Move error.
                }
            }
        }
    }
}
