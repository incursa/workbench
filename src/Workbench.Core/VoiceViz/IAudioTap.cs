namespace Workbench.VoiceViz;

public interface IAudioTap
{
    void PushPcm16(ReadOnlySpan<short> samples);
}
