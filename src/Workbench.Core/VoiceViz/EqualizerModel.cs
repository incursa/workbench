namespace Workbench.VoiceViz;

public sealed class EqualizerModel
{
    private float level01;
    private float[] frontBands;
    private float[] backBands;
    private long samplesSeen;

    public EqualizerModel(int bandCount)
    {
        if (bandCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bandCount));
        }
        this.frontBands = new float[bandCount];
        this.backBands = new float[bandCount];
    }

    public int BandCount => this.frontBands.Length;

    public void UpdateLevel(float value)
    {
        this.level01 = Clamp01(value);
    }

    public void AddSamples(int count)
    {
        if (count > 0)
        {
            Interlocked.Add(ref this.samplesSeen, count);
        }
    }

    public long SampleCount => Interlocked.Read(ref this.samplesSeen);

    public void UpdateBands(ReadOnlySpan<float> bands)
    {
        var target = this.backBands;
        var count = Math.Min(target.Length, bands.Length);
        for (var i = 0; i < count; i++)
        {
            target[i] = Clamp01(bands[i]);
        }
        for (var i = count; i < target.Length; i++)
        {
            target[i] = 0f;
        }

        var previous = Interlocked.Exchange(ref this.frontBands, target);
        this.backBands = previous;
    }

    public float CopySnapshot(Span<float> bandsDest)
    {
        var bands = Volatile.Read(ref this.frontBands);
        var count = Math.Min(bands.Length, bandsDest.Length);
        bands.AsSpan(0, count).CopyTo(bandsDest);
        for (var i = count; i < bandsDest.Length; i++)
        {
            bandsDest[i] = 0f;
        }
        return Clamp01(Volatile.Read(ref this.level01));
    }

    private static float Clamp01(float value)
    {
        if (value < 0f)
        {
            return 0f;
        }
        return value > 1f ? 1f : value;
    }
}
