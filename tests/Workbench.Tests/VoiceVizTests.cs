using Microsoft.VisualStudio.TestTools.UnitTesting;
using Workbench.VoiceViz;

namespace Workbench.Tests;

[TestClass]
public sealed class VoiceVizTests
{
    [TestMethod]
    public void AudioTapComputesRmsLevel()
    {
        var model = new EqualizerModel(8);
        var options = EqualizerOptions.Default with { EnableSpectrum = false, LevelBoost = 1f };
        var tap = new AudioTap(model, options, ringBufferSize: 0);

        var samples = new short[1000];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = short.MaxValue;
        }

        tap.PushPcm16(samples);

        var snapshot = new float[model.BandCount];
        var level = model.CopySnapshot(snapshot);
        Assert.IsGreaterThan(0.9f, level);
        Assert.IsLessThanOrEqualTo(1f, level);
    }

    [TestMethod]
    public void RingBufferWrapsCorrectly()
    {
        var buffer = new AudioRingBuffer(8);
        var written = buffer.Write(new short[] { 1, 2, 3, 4, 5, 6 });
        Assert.AreEqual(6, written);

        var readTarget = new short[4];
        var read = buffer.Read(readTarget);
        Assert.AreEqual(4, read);
        CollectionAssert.AreEqual(new short[] { 1, 2, 3, 4 }, readTarget);

        written = buffer.Write(new short[] { 7, 8, 9, 10, 11 });
        Assert.AreEqual(5, written);

        var readTarget2 = new short[6];
        read = buffer.Read(readTarget2);
        Assert.AreEqual(6, read);
        CollectionAssert.AreEqual(new short[] { 5, 6, 7, 8, 9, 10 }, readTarget2);
    }

    [TestMethod]
    public void SpectrumAnalyzerBandsStayInRange()
    {
        var model = new EqualizerModel(8);
        var options = EqualizerOptions.Default with { BandCount = 8, FftSize = 1024 };
        var ring = new AudioRingBuffer(4096);
        var analyzer = new SpectrumAnalyzer(model, ring, options, sampleRate: 16000);

        var samples = new short[options.FftSize];
        var frequency = 440f;
        var sampleRate = 16000f;
        for (var i = 0; i < samples.Length; i++)
        {
            var t = i / sampleRate;
            samples[i] = (short)(MathF.Sin(2f * MathF.PI * frequency * t) * short.MaxValue);
        }

        var bands = new float[options.BandCount];
        analyzer.Analyze(samples, bands);

        foreach (var band in bands)
        {
            Assert.IsGreaterThanOrEqualTo(0f, band);
            Assert.IsLessThanOrEqualTo(1f, band);
        }
    }
}
