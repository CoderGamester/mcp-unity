namespace McpUnity.Extensions.Setup
{
    public enum UnityCliPipelineState
    {
        ExactSupported,
        Missing,
        DifferentUntested
    }

    public static class UnityCliPipelineStatus
    {
        public static UnityCliPipelineState Classify(string version)
        {
            if (string.IsNullOrEmpty(version))
            {
                return UnityCliPipelineState.Missing;
            }

            return version == "0.3.1-exp.1"
                ? UnityCliPipelineState.ExactSupported
                : UnityCliPipelineState.DifferentUntested;
        }

        public static string GetDisplayName(UnityCliPipelineState state)
        {
            switch (state)
            {
                case UnityCliPipelineState.ExactSupported:
                    return "exact supported";
                case UnityCliPipelineState.Missing:
                    return "missing";
                default:
                    return "different/untested";
            }
        }
    }
}
