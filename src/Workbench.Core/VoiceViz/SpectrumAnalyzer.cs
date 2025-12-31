namespace Workbench.VoiceViz;

public sealed class SpectrumAnalyzer : IAsyncDisposable
{
    private readonly EqualizerModel model;
    private readonly AudioRingBuffer ringBuffer;
    private readonly EqualizerOptions options;
    private readonly float[] window;
    private readonly float[] real;
    private readonly float[] imag;
    private readonly float[] bandLevels;
    private readonly float[] smoothedBands;
    private readonly int[] bandEdges;
    private Task? worker;
    private CancellationTokenSource? cts;

    public SpectrumAnalyzer(EqualizerModel model, AudioRingBuffer ringBuffer, EqualizerOptions options, int sampleRate)
    {
        if (options.FftSize <= 0 || (options.FftSize & (options.FftSize - 1)) != 0)
        {
#pragma warning disable S3928
#pragma warning disable MA0015
            throw new ArgumentOutOfRangeException(nameof(options.FftSize));
#pragma warning restore MA0015
#pragma warning restore S3928
        }
        this.model = model;
        this.ringBuffer = ringBuffer;
        this.options = options;
        this.window = new float[options.FftSize];
        this.real = new float[options.FftSize];
        this.imag = new float[options.FftSize];
        this.bandLevels = new float[options.BandCount];
        this.smoothedBands = new float[options.BandCount];
        this.bandEdges = BuildLogBands(options.BandCount, options.FftSize, sampleRate, 80f, sampleRate / 2f);
        BuildHannWindow(this.window);
    }

    public void Start()
    {
        if (this.worker is not null)
        {
            return;
        }
        this.cts = new CancellationTokenSource();
        this.worker = Task.Run(() => this.RunAsync(this.cts.Token), this.cts.Token);
    }

    public async ValueTask DisposeAsync()
    {
        if (this.cts is null)
        {
            return;
        }
        await this.cts.CancelAsync().ConfigureAwait(false);
        try
        {
            if (this.worker is not null)
            {
                Task task = this.worker;
                if (task != null)
                {
                    await task.ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Noop
        }
        finally
        {
            this.cts.Dispose();
            this.cts = null;
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var sampleBuffer = new short[this.options.FftSize];
        var updateDelay = TimeSpan.FromMilliseconds(1000d / this.options.UpdateHz);
        while (!ct.IsCancellationRequested)
        {
            if (this.ringBuffer.AvailableToRead >= this.options.FftSize)
            {
                var read = this.ringBuffer.Read(sampleBuffer);
                if (read == this.options.FftSize)
                {
                    this.Analyze(sampleBuffer, this.bandLevels);
                    ApplySmoothing(this.smoothedBands, this.bandLevels, this.options.Attack, this.options.Release);
                    this.model.UpdateBands(this.smoothedBands);
                }
            }

            await Task.Delay(updateDelay, ct).ConfigureAwait(false);
        }
    }

    public void Analyze(ReadOnlySpan<short> samples, Span<float> destination)
    {
        var scale = 1f / 32768f;
        for (var i = 0; i < samples.Length; i++)
        {
            this.real[i] = samples[i] * scale * this.window[i];
            this.imag[i] = 0f;
        }

        FftInPlace(this.real, this.imag);
        ComputeBandLevels(this.real, this.imag, this.bandEdges, destination);
    }

    internal static void ComputeBandLevels(ReadOnlySpan<float> real, ReadOnlySpan<float> imag, int[] edges, Span<float> destination)
    {
        var binCount = real.Length / 2;
        var offset = 0;
        for (var band = 0; band < destination.Length; band++)
        {
            var start = edges[offset++];
            var end = edges[offset++];
            if (start < 1)
            {
                start = 1;
            }
            if (end > binCount)
            {
                end = binCount;
            }
            if (end <= start)
            {
                destination[band] = 0f;
                continue;
            }

            float sum = 0f;
            var count = 0;
            for (var i = start; i < end; i++)
            {
                var mag = MathF.Sqrt(real[i] * real[i] + imag[i] * imag[i]);
                sum += mag;
                count++;
            }

            var avg = count > 0 ? sum / count : 0f;
            destination[band] = avg;
        }

        var minDb = -60f;
        for (var i = 0; i < destination.Length; i++)
        {
            var db = 20f * MathF.Log10(destination[i] + 1e-6f);
            var normalized = (db - minDb) / -minDb;
            destination[i] = Math.Clamp(normalized, 0f, 1f);
        }
    }

    internal static int[] BuildLogBands(int bandCount, int fftSize, int sampleRate, float minFreq, float maxFreq)
    {
        var edges = new int[bandCount * 2];
        var min = MathF.Max(1f, minFreq);
        var max = MathF.Max(min + 1f, maxFreq);
        var logMin = MathF.Log10(min);
        var logMax = MathF.Log10(max);
        var binWidth = sampleRate / (float)fftSize;

        for (var band = 0; band < bandCount; band++)
        {
            var t0 = band / (float)bandCount;
            var t1 = (band + 1) / (float)bandCount;
            var f0 = MathF.Pow(10f, logMin + (logMax - logMin) * t0);
            var f1 = MathF.Pow(10f, logMin + (logMax - logMin) * t1);
            var b0 = (int)MathF.Round(f0 / binWidth);
            var b1 = (int)MathF.Round(f1 / binWidth);
            edges[band * 2] = b0;
            edges[band * 2 + 1] = Math.Max(b1, b0 + 1);
        }

        return edges;
    }

    private static void BuildHannWindow(Span<float> window)
    {
        var length = window.Length;
        for (var i = 0; i < length; i++)
        {
            window[i] = 0.5f * (1f - MathF.Cos(2f * MathF.PI * i / (length - 1)));
        }
    }

    private static void ApplySmoothing(Span<float> current, ReadOnlySpan<float> target, float attack, float release)
    {
        for (var i = 0; i < current.Length; i++)
        {
            var value = target[i];
            var factor = value >= current[i] ? attack : release;
            current[i] += (value - current[i]) * factor;
        }
    }

    private static void FftInPlace(Span<float> real, Span<float> imag)
    {
        var n = real.Length;
        var j = 0;
        for (var i = 1; i < n - 1; i++)
        {
            var bit = n >> 1;
            while (j >= bit)
            {
                j -= bit;
                bit >>= 1;
            }
            j += bit;
            if (i < j)
            {
                (real[i], real[j]) = (real[j], real[i]);
                (imag[i], imag[j]) = (imag[j], imag[i]);
            }
        }

        for (var len = 2; len <= n; len <<= 1)
        {
            var ang = -2f * MathF.PI / len;
            var wlenR = MathF.Cos(ang);
            var wlenI = MathF.Sin(ang);
            for (var i = 0; i < n; i += len)
            {
                var wR = 1f;
                var wI = 0f;
                for (var k = 0; k < len / 2; k++)
                {
                    var uR = real[i + k];
                    var uI = imag[i + k];
                    var vR = real[i + k + len / 2] * wR - imag[i + k + len / 2] * wI;
                    var vI = real[i + k + len / 2] * wI + imag[i + k + len / 2] * wR;

                    real[i + k] = uR + vR;
                    imag[i + k] = uI + vI;
                    real[i + k + len / 2] = uR - vR;
                    imag[i + k + len / 2] = uI - vI;

                    var nextWR = wR * wlenR - wI * wlenI;
                    var nextWI = wR * wlenI + wI * wlenR;
                    wR = nextWR;
                    wI = nextWI;
                }
            }
        }
    }
}
