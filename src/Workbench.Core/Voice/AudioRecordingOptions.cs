using Workbench.VoiceViz;

namespace Workbench.Core.Voice;

public sealed record AudioRecordingOptions(
    AudioFormat Format,
    TimeSpan MaxDuration,
    long MaxBytes,
    string OutputDirectory,
    string FilePrefix,
    uint FramesPerBuffer,
    IAudioTap? Tap = null);
