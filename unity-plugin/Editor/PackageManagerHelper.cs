// PackageManagerHelper: async UPM operations via Client.* + EditorApplication.update pump.
// Must run on main thread — caller enqueues via MainThreadDispatcher.
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;

namespace UnityMCP.Editor
{
    internal static class PackageManagerHelper
    {
        internal static void Execute(string action, string name, string version,
                                     string query, TaskCompletionSource<string> inner)
        {
            switch (action)
            {
                case "list":
                    var listReq = Client.List();
                    PollUntilComplete(listReq, () => FormatPackageList(listReq), inner);
                    break;
                case "search":
                    if (string.IsNullOrEmpty(query)) { inner.TrySetResult("err:query required"); return; }
                    var searchReq = Client.Search(query);
                    PollUntilComplete(searchReq, () => FormatPackageSearch(searchReq), inner);
                    break;
                case "add":
                    if (string.IsNullOrEmpty(name)) { inner.TrySetResult("err:name required"); return; }
                    var nameVer = string.IsNullOrEmpty(version) ? name : $"{name}@{version}";
                    var addReq = Client.Add(nameVer);
                    PollUntilComplete(addReq, () => FormatPackageAdd(addReq), inner);
                    break;
                case "remove":
                    if (string.IsNullOrEmpty(name)) { inner.TrySetResult("err:name required"); return; }
                    var removeReq = Client.Remove(name);
                    PollUntilComplete(removeReq, () => "ok", inner);
                    break;
                default:
                    inner.TrySetResult($"err:invalid action '{action}'");
                    break;
            }
        }

        // Polls every editor frame until req completes; failure path uses req.Error.
        private static void PollUntilComplete(Request req,
            System.Func<string> getResult, TaskCompletionSource<string> inner)
        {
            EditorApplication.CallbackFunction pump = null;
            pump = () =>
            {
                if (!req.IsCompleted) return;
                EditorApplication.update -= pump;
                if (req.Status == StatusCode.Failure)
                    inner.TrySetResult($"err:{req.Error?.message ?? "unknown"}");
                else
                    inner.TrySetResult(getResult());
            };
            EditorApplication.update += pump;
        }

        internal static string FormatPackageList(ListRequest req)
        {
            var sb = new StringBuilder();
            foreach (var pkg in req.Result)
                sb.AppendLine($"name:{pkg.name} ver:{pkg.version} src:{pkg.source.ToString().ToLowerInvariant()}");
            return sb.ToString().TrimEnd();
        }

        internal static string FormatPackageAdd(AddRequest req)
        {
            var pkg = req.Result;
            return $"ok\nname:{pkg.name} ver:{pkg.version}";
        }

        internal static string FormatPackageSearch(SearchRequest req)
        {
            var sb = new StringBuilder();
            foreach (var pkg in req.Result)
                sb.AppendLine($"id:{pkg.name} ver:{pkg.version}");
            return sb.ToString().TrimEnd();
        }
    }
}
