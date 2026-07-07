using System;
using UnityEngine;

namespace UnityMCP.Editor
{
    [Serializable]
    internal class VisualStep
    {
        public StepType type;
        public string description = "";  // DESC label shown above the step
        public string path      = "";    // Move/Teleport/Invoke target
        public Vector3 position;         // Move/Teleport coords
        public float   delay;            // Wait/TimeScale duration
        public string  query    = "";    // Assert/WaitUntil primary query
        public string  op       = "==";  // comparison operator
        public string  value    = "";    // expected value
        public float   timeout  = 5f;    // WaitUntil timeout seconds
        public string  component = "";   // Invoke component name
        public string  method    = "";   // Invoke method name
        public string  args      = "";   // Invoke arguments
        public string  message   = "";   // Section/Log/raw text
        public bool    abortOnFail;      // WaitUntil abort on timeout

        internal VisualStep Clone() => new VisualStep {
            type = type, description = description, path = path, position = position,
            delay = delay, query = query, op = op, value = value, timeout = timeout,
            component = component, method = method, args = args,
            message = message, abortOnFail = abortOnFail
        };
    }
}
