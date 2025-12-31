namespace Workbench.VoiceViz;

public sealed record EqualizerOptions(
    int BandCount,
    int UpdateHz,
    int FftSize,
    float LevelBoost,
    float Attack,
    float Release,
    bool EnableSpectrum)
{
    public static EqualizerOptions Default => new(
        BandCount: 12,
        UpdateHz: 20,
        FftSize: 1024,
        LevelBoost: 4.0f,
        Attack: 0.6f,
        Release: 0.9f,
        EnableSpectrum: true);

    public static EqualizerOptions Load()
    {
        var defaults = Default;
        var fftSize = ReadInt("WORKBENCH_VOICE_VIZ_FFT_SIZE", defaults.FftSize);
        if (!IsPowerOfTwo(fftSize))
        {
            fftSize = defaults.FftSize;
        }
        return new EqualizerOptions(
            BandCount: ReadInt("WORKBENCH_VOICE_VIZ_BANDS", defaults.BandCount),
            UpdateHz: ReadInt("WORKBENCH_VOICE_VIZ_UPDATE_HZ", defaults.UpdateHz),
            FftSize: fftSize,
            LevelBoost: ReadFloat("WORKBENCH_VOICE_VIZ_LEVEL_BOOST", defaults.LevelBoost),
            Attack: ReadFloat("WORKBENCH_VOICE_VIZ_ATTACK", defaults.Attack),
            Release: ReadFloat("WORKBENCH_VOICE_VIZ_RELEASE", defaults.Release),
            EnableSpectrum: ReadBool("WORKBENCH_VOICE_VIZ_SPECTRUM", defaults.EnableSpectrum));
    }

    private static int ReadInt(string key, int fallback)
    {
        var raw = Environment.GetEnvironmentVariable(key);
        return int.TryParse(raw, CultureInfo.InvariantCulture, out var parsed) && parsed > 0 ? parsed : fallback;
    }

    private static float ReadFloat(string key, float fallback)
    {
        var raw = Environment.GetEnvironmentVariable(key);
        return float.TryParse(raw, CultureInfo.InvariantCulture, out var parsed) && parsed > 0 ? parsed : fallback;
    }

    private static bool ReadBool(string key, bool fallback)
    {
        var raw = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }
        return raw.Equals("1", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("true", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPowerOfTwo(int value)
    {
        return value > 0 && (value & (value - 1)) == 0;
    }
}
