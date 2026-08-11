// Parser for TaskCreate and TaskUpdate tool argsJson.
// Pure C# — no UnityEngine deps (noEngineReferences: true).
// Strategy A: ^Task #(\d+) created successfully: on resultText.
// Strategy B: ^Task (\d+): on subject — primary when relay mutes tool_result.
using System.Text.RegularExpressions;

namespace UnityMCP.Editor.Chat.Parsers
{
    internal enum TaskStatus { Pending, InProgress, Completed }
    internal enum TaskCallKind { Create, Update }

    internal struct TaskCallArgs
    {
        public TaskCallKind Kind;
        public string Subject;
        public string Description;
        public string ActiveForm;
        public string TaskId;
        public TaskStatus Status;
        public bool IsValid;
    }

    internal static class TodoTaskParser
    {
        private static readonly Regex _resultRx =
            new Regex(@"^Task #(\d+) created successfully:", RegexOptions.Compiled);
        private static readonly Regex _subjectRx =
            new Regex(@"^Task (\d+):", RegexOptions.Compiled);

        internal static TaskCallArgs Parse(string toolName, string argsJson)
        {
            if (string.IsNullOrEmpty(argsJson))
                return default;

            var t = argsJson.TrimStart();
            if (t.Length == 0 || t[0] != '{')
                return default;

            if (toolName == "TaskCreate")
                return new TaskCallArgs
                {
                    Kind        = TaskCallKind.Create,
                    Subject     = ReadString(argsJson, "subject"),
                    Description = ReadString(argsJson, "description"),
                    ActiveForm  = ReadString(argsJson, "activeForm"),
                    IsValid     = true,
                };

            if (toolName == "TaskUpdate")
                return new TaskCallArgs
                {
                    Kind    = TaskCallKind.Update,
                    TaskId  = ReadString(argsJson, "taskId"),
                    Status  = ParseStatus(ReadString(argsJson, "status")),
                    IsValid = true,
                };

            return default;
        }

        // Caller logs Debug.LogWarning on null — NOT this class (noEngineReferences: true).
        internal static string TryExtractTaskId(string resultText, string subject)
        {
            if (resultText != null)
            {
                var m = _resultRx.Match(resultText);
                if (m.Success) return m.Groups[1].Value;
            }
            if (subject != null)
            {
                var m = _subjectRx.Match(subject);
                if (m.Success) return m.Groups[1].Value;
            }
            return null;
        }

        private static TaskStatus ParseStatus(string s)
        {
            switch (s)
            {
                case "in_progress": return TaskStatus.InProgress;
                case "completed":   return TaskStatus.Completed;
                default:            return TaskStatus.Pending;
            }
        }

        private static string ReadString(string json, string key) =>
            JsonFieldReader.ReadString(json, key);
    }
}
