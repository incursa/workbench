namespace Workbench.VoiceViz;

public sealed class AudioRingBuffer
{
    private readonly short[] buffer;
    private long writePosition;
    private long readPosition;

    public AudioRingBuffer(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }
        this.buffer = new short[capacity];
    }

    public int Capacity => this.buffer.Length;

    public int AvailableToRead
    {
        get
        {
            var localWrite = Volatile.Read(ref this.writePosition);
            var localRead = Volatile.Read(ref this.readPosition);
            var available = localWrite - localRead;
            if (available <= 0)
            {
                return 0;
            }
            return available > int.MaxValue ? int.MaxValue : (int)available;
        }
    }

    public int Write(ReadOnlySpan<short> samples)
    {
        var localWrite = this.writePosition;
        var localRead = Volatile.Read(ref this.readPosition);
        var space = this.buffer.Length - (localWrite - localRead);
        if (space <= 0)
        {
            return 0;
        }

        var toWrite = (int)Math.Min(space, samples.Length);
        if (toWrite <= 0)
        {
            return 0;
        }

        var startIndex = (int)(localWrite % this.buffer.Length);
        var firstPart = Math.Min(toWrite, this.buffer.Length - startIndex);
        samples.Slice(0, firstPart).CopyTo(this.buffer.AsSpan(startIndex, firstPart));

        var remaining = toWrite - firstPart;
        if (remaining > 0)
        {
            samples.Slice(firstPart, remaining).CopyTo(this.buffer.AsSpan(0, remaining));
        }

        Volatile.Write(ref this.writePosition, localWrite + toWrite);
        return toWrite;
    }

    public int Read(Span<short> destination)
    {
        var localRead = this.readPosition;
        var localWrite = Volatile.Read(ref this.writePosition);
        var available = localWrite - localRead;
        if (available <= 0)
        {
            return 0;
        }

        var toRead = (int)Math.Min(available, destination.Length);
        if (toRead <= 0)
        {
            return 0;
        }

        var startIndex = (int)(localRead % this.buffer.Length);
        var firstPart = Math.Min(toRead, this.buffer.Length - startIndex);
        this.buffer.AsSpan(startIndex, firstPart).CopyTo(destination.Slice(0, firstPart));

        var remaining = toRead - firstPart;
        if (remaining > 0)
        {
            this.buffer.AsSpan(0, remaining).CopyTo(destination.Slice(firstPart, remaining));
        }

        Volatile.Write(ref this.readPosition, localRead + toRead);
        return toRead;
    }
}
