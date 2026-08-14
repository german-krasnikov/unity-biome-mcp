// T16: Pure data model for ChangeSet rendering. No Unity API, no VisualElement.
namespace UnityMCP.Editor
{
    internal sealed class ChangeSetViewModel
    {
        public string ChangeSetId { get; }
        public string Status      { get; }  // "open"|"finalized"|"reverted"
        public OperationViewModel[] Operations { get; }

        public int CreateCount { get; }
        public int ModifyCount { get; }
        public int DeleteCount { get; }

        // "abc12345 | finalized | +1 ~3 -1"
        public string Summary =>
            $"{ChangeSetId} | {Status} | +{CreateCount} ~{ModifyCount} -{DeleteCount}";

        public ChangeSetViewModel(string id, string status, OperationViewModel[] ops)
        {
            ChangeSetId  = id     ?? "";
            Status       = status ?? "open";
            Operations   = ops    ?? System.Array.Empty<OperationViewModel>();
            CreateCount  = System.Array.FindAll(Operations, o => o.Kind == "create").Length;
            ModifyCount  = System.Array.FindAll(Operations, o => o.Kind == "modify").Length;
            DeleteCount  = System.Array.FindAll(Operations, o => o.Kind == "delete").Length;
        }
    }

    internal sealed class OperationViewModel
    {
        public string Kind       { get; }
        public string TargetType { get; }
        public string TargetPath { get; }
        public string Prop       { get; }
        public string BeforeHash { get; }
        public string AfterHash  { get; }
        public bool   Reversible { get; }

        public OperationViewModel(string kind, string targetType, string targetPath,
            string prop, string bh, string ah, bool rev)
        {
            Kind = kind; TargetType = targetType; TargetPath = targetPath;
            Prop = prop; BeforeHash = bh; AfterHash = ah; Reversible = rev;
        }
    }
}
