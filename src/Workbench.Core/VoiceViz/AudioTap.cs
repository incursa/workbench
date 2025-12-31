namespace Workbench.VoiceViz;

public sealed class AudioTap : IAudioTap
{
    private const float ShortScale = 1f / 32768f;
    private readonly EqualizerModel model;
    private readonly AudioRingBuffer? ringBuffer;
    private readonly float levelBoost;

    public AudioTap(EqualizerModel model, EqualizerOptions options, int ringBufferSize)
    {
        this.model = model;
        this.levelBoost = options.LevelBoost;
        this.ringBuffer = options.EnableSpectrum ? new AudioRingBuffer(ringBufferSize) : null;
    }

    public AudioRingBuffer? RingBuffer => this.ringBuffer;

    public void PushPcm16(ReadOnlySpan<short> samples)
    {
        if (samples.IsEmpty)
        {
            this.model.UpdateLevel(0f);
            return;
        }

        this.model.AddSamples(samples.Length);

        double sumSquares = 0;
        for (var i = 0; i < samples.Length; i++)
        {
            var value = samples[i] * ShortScale;
            sumSquares += value * value;
        }

        var rms = (float)Math.Sqrt(sumSquares / samples.Length);
        var level = Math.Clamp(rms * this.levelBoost, 0f, 1f);
        this.model.UpdateLevel(level);

        this.ringBuffer?.Write(samples);
    }
}
