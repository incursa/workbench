using Terminal.Gui;
using Workbench.VoiceViz;
using Attribute = Terminal.Gui.Attribute;

namespace Workbench.Tui.VoiceViz;

public sealed class EqualizerView : View
{
    private readonly EqualizerModel model;
    private readonly float[] bandBuffer;
    private float[] columnBuffer = Array.Empty<float>();

    public EqualizerView(EqualizerModel model)
    {
        this.model = model;
        bandBuffer = new float[model.BandCount];
        CanFocus = false;
    }

    public bool ShowLabel { get; set; } = true;

    public override void Redraw(Rect bounds)
    {
        Driver.SetAttribute(ColorScheme?.Normal ?? Attribute.Make(Color.Black, Color.Gray));
        Clear();
        base.Redraw(bounds);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var labelHeight = ShowLabel ? 1 : 0;
        var barHeight = Math.Max(1, bounds.Height - labelHeight);
        var level = model.CopySnapshot(bandBuffer);
        var displayLevel = MathF.Min(1f, MathF.Pow(level, 0.35f) * 1.1f);
        var maxBand = 0f;
        for (var i = 0; i < bandBuffer.Length; i++)
        {
            if (bandBuffer[i] > maxBand)
            {
                maxBand = bandBuffer[i];
            }
        }

        if (maxBand < 0.01f)
        {
            for (var i = 0; i < bandBuffer.Length; i++)
            {
                bandBuffer[i] = displayLevel;
            }
        }

        if (ShowLabel)
        {
            var samples = model.SampleCount;
            var label = $"REC {Math.Round(displayLevel * 100):0}% ({samples / 1000}k)";
            DrawText(0, 0, label, bounds.Width);
        }

        var columns = Math.Min(bandBuffer.Length, bounds.Width);
        if (columns <= 0)
        {
            return;
        }

        var columnValues = EnsureColumnBuffer(columns);
        var scale = Math.Clamp(displayLevel / 0.3f, 0f, 1.6f);

        for (var column = 0; column < columns; column++)
        {
            var start = (int)MathF.Floor(column * bandBuffer.Length / (float)columns);
            var end = (int)MathF.Floor((column + 1) * bandBuffer.Length / (float)columns);
            if (end <= start)
            {
                end = Math.Min(start + 1, bandBuffer.Length);
            }

            float sum = 0f;
            var count = 0;
            for (var i = start; i < end && i < bandBuffer.Length; i++)
            {
                sum += bandBuffer[i];
                count++;
            }
            var value = count > 0 ? sum / count : 0f;
            value = MathF.Pow(value, 0.7f);
            value = Math.Max(value, displayLevel * 0.35f);
            columnValues[column] = Math.Clamp(value * scale, 0f, 1f);
        }

        if (columns > 2)
        {
            for (var i = 1; i < columns - 1; i++)
            {
                var smoothed = (columnValues[i - 1] + columnValues[i] + columnValues[i + 1]) / 3f;
                columnValues[i] = MathF.Max(columnValues[i], smoothed * 0.85f);
            }

            var max = 0f;
            var second = 0f;
            var maxIndex = 0;
            for (var i = 0; i < columns; i++)
            {
                var value = columnValues[i];
                if (value > max)
                {
                    second = max;
                    max = value;
                    maxIndex = i;
                }
                else if (value > second)
                {
                    second = value;
                }
            }

            if (maxIndex == columns - 1 && max > 0.9f && second < 0.3f)
            {
                columnValues[maxIndex] = Math.Clamp(second + 0.2f, 0f, 1f);
            }
        }

        for (var column = 0; column < columns; column++)
        {
            var filled = (int)MathF.Round(columnValues[column] * (barHeight - 1));
            for (var row = 0; row < barHeight; row++)
            {
                var y = labelHeight + (barHeight - 1 - row);
                AddRune(column, y, row <= filled ? '█' : ' ');
            }
        }

        for (var column = columns; column < bounds.Width; column++)
        {
            for (var row = 0; row < barHeight; row++)
            {
                var y = labelHeight + (barHeight - 1 - row);
                AddRune(column, y, ' ');
            }
        }
    }

    private void DrawText(int x, int y, string text, int width)
    {
        var length = Math.Min(text.Length, width);
        for (var i = 0; i < length; i++)
        {
            AddRune(x + i, y, text[i]);
        }
        for (var i = length; i < width; i++)
        {
            AddRune(x + i, y, ' ');
        }
    }

    private float[] EnsureColumnBuffer(int count)
    {
        if (columnBuffer.Length < count)
        {
            columnBuffer = new float[count];
        }
        return columnBuffer;
    }
}
