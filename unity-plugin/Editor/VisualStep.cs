using System;
using UnityEngine;

namespace UnityMCP.Editor
{
    [Serializable]
    internal class VisualStep
    {
        // Single backing field — all data lives here
        [SerializeField] internal PlaytestStep _step;

        // ── Delegating properties (camelCase preserved for test/UI/initializer compat) ──
        public StepType type      { get => _step.Type;         set => _step.Type = value; }
        public string description { get => _step.Label ?? "";  set => _step.Label = value; }
        public string path        { get => _step.Path ?? "";   set => _step.Path = value; }
        public Vector3 position   { get => _step.Position;     set => _step.Position = value; }
        public float delay        { get => _step.Delay;        set => _step.Delay = value; }
        public string query       { get => _step.Query ?? "";  set => _step.Query = value; }
        public string op          { get => _step.Op ?? "==";   set => _step.Op = value; }
        public string value       { get => _step.Value ?? "";  set => _step.Value = value; }
        public float timeout      { get => _step.Timeout;      set => _step.Timeout = value; }
        public string component   { get => _step.Component ?? ""; set => _step.Component = value; }
        public string method      { get => _step.Method ?? "";    set => _step.Method = value; }
        public string args        { get => _step.Args ?? "";      set => _step.Args = value; }
        public string message     { get => _step.Message ?? "";   set => _step.Message = value; }
        public bool abortOnFail   { get => _step.AbortOnFail;     set => _step.AbortOnFail = value; }

        public VisualStep() { _step = new PlaytestStep { Timeout = 5f, Op = "==" }; }

        internal VisualStep(PlaytestStep p) { _step = p; }

        internal PlaytestStep ToStep() => _step;

        internal VisualStep Clone() => new VisualStep(_step.ShallowClone());
    }
}
