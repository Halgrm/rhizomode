#nullable enable

namespace Rhizomode.Audio.Analysis
{
    internal enum AudioAnalyzerUpdateAction
    {
        None,
        WaitForNextFrame,
        InitializePending,
        ShutdownBeforePending,
        ClearInconsistent
    }
}
