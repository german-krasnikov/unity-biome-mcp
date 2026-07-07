using System;

namespace UnityMCP.Editor
{
    [Serializable]
    internal sealed class SamplingConfig
    {
        public string Model     = "";
        public string Backend   = "";   // "" == "claude" (backward compat)
        public int    MaxTurns  = 2;
        public float  Timeout   = 20f;
        public int    MaxTokens = 0;
    }

    [Serializable]
    internal sealed class LlmConfig
    {
        public SamplingConfig VisualVerify       = new SamplingConfig { Timeout = 15f };
        public SamplingConfig ScreenshotDescribe = new SamplingConfig { Timeout = 20f };
        public SamplingConfig VisualDiff         = new SamplingConfig { Timeout = 25f };
        public SamplingConfig Summarize          = new SamplingConfig { MaxTurns = 1, Timeout = 15f };
        public SamplingConfig DoIntent           = new SamplingConfig { MaxTurns = 1, Timeout = 15f };
        public SamplingConfig Distiller          = new SamplingConfig { MaxTurns = 1, Timeout = 15f, MaxTokens = 500 };
        public SamplingConfig NlComposer         = new SamplingConfig { MaxTurns = 1, Timeout = 10f, MaxTokens = 512, Model = "haiku" };
    }
}
