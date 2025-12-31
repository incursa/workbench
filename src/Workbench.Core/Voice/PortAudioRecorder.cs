using System.Runtime.InteropServices;
using PortAudioSharp;
using Workbench.VoiceViz;
using Stream = PortAudioSharp.Stream;

namespace Workbench.Core.Voice;

public sealed class PortAudioRecorder : IAudioRecorder
{
    private static int activeSessions;

    public async Task<IAudioRecordingSession> StartAsync(AudioRecordingOptions options, CancellationToken ct)
    {
        if (options.Format.Channels != 1)
        {
            throw new InvalidOperationException("Only mono recording is supported.");
        }

        EnsureInitialized();

        try
        {
            var deviceIndex = PortAudio.DefaultInputDevice;
            if (deviceIndex == PortAudio.NoDevice)
            {
                throw new InvalidOperationException("No default input device found.");
            }

            var deviceInfo = PortAudio.GetDeviceInfo(deviceIndex);
            var param = new StreamParameters
            {
                device = deviceIndex,
                channelCount = options.Format.Channels,
                sampleFormat = SampleFormat.Int16,
                suggestedLatency = deviceInfo.defaultLowInputLatency,
                hostApiSpecificStreamInfo = IntPtr.Zero
            };

            var limit = AudioLimiter.Calculate(options.Format, options.MaxDuration, options.MaxBytes);
            var maxFrames = limit.MaxFrames;

            var outputDir = options.OutputDirectory;
            Directory.CreateDirectory(outputDir);
            var fileName = $"{options.FilePrefix}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}.wav";
            var outputPath = Path.Combine(outputDir, fileName);

            var session = new RecordingSession(options.Format, outputPath, maxFrames, options.Tap);

            Stream.Callback callback = (IntPtr input, IntPtr output, uint frameCount, ref StreamCallbackTimeInfo timeInfo,
                StreamCallbackFlags statusFlags, IntPtr userData) =>
            {
                return session.OnAudio(input, frameCount);
            };

            session.AttachStream(new Stream(
                inParams: param,
                outParams: null,
                sampleRate: options.Format.SampleRateHz,
                framesPerBuffer: options.FramesPerBuffer,
                streamFlags: StreamFlags.ClipOff,
                callback: callback,
                userData: IntPtr.Zero));

            session.Start();

            if (ct.CanBeCanceled)
            {
                ct.Register(() => _ = session.CancelAsync(CancellationToken.None));
            }

            await Task.Yield();
            return session;
        }
        catch
        {
            ReleaseInitialization();
            throw;
        }
    }

    private static void EnsureInitialized()
    {
        if (Interlocked.Increment(ref activeSessions) == 1)
        {
            PortAudio.Initialize();
        }
    }

    private static void ReleaseInitialization()
    {
        if (Interlocked.Decrement(ref activeSessions) == 0)
        {
            try
            {
                PortAudio.Terminate();
            }
            catch
            {
                // Ignore termination failures during teardown.
#pragma warning disable ERP022
            }
#pragma warning restore ERP022
        }
    }

    private sealed class RecordingSession : IAudioRecordingSession
    {
        private readonly AudioFormat format;
        private readonly long maxFrames;
        private readonly string outputPath;
        private readonly IAudioTap? tap;
        private readonly Lock gate = new();
        private readonly TaskCompletionSource<AudioRecordingResult> completion;
        private readonly WaveFileWriter writer;
        private PortAudioSharp.Stream? stream;
        private short[] sampleBuffer = Array.Empty<short>();
        private long framesWritten;
        private bool stopRequested;
        private bool cancelRequested;
        private bool finalized;
        private int stopped;

        public RecordingSession(AudioFormat format, string outputPath, long maxFrames, IAudioTap? tap)
        {
            this.format = format;
            this.outputPath = outputPath;
            this.maxFrames = maxFrames;
            this.tap = tap;
            this.completion = new TaskCompletionSource<AudioRecordingResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            this.writer = new WaveFileWriter(outputPath, format);
        }

        public Task<AudioRecordingResult> Completion => this.completion.Task;

        public void AttachStream(PortAudioSharp.Stream streamInstance)
        {
            this.stream = streamInstance;
        }

        public void Start()
        {
            this.stream?.Start();
        }

        public StreamCallbackResult OnAudio(IntPtr input, uint frameCount)
        {
            if (input == IntPtr.Zero || frameCount == 0)
            {
                return StreamCallbackResult.Continue;
            }

            var framesToWrite = (long)frameCount;
            if (this.maxFrames > 0)
            {
                var remaining = this.maxFrames - this.framesWritten;
                if (remaining <= 0)
                {
                    this.stopRequested = true;
                    return StreamCallbackResult.Complete;
                }
                if (framesToWrite > remaining)
                {
                    framesToWrite = remaining;
                    this.stopRequested = true;
                }
            }

            var sampleCount = (int)(framesToWrite * this.format.Channels);
            if (this.sampleBuffer.Length < sampleCount)
            {
                this.sampleBuffer = new short[sampleCount];
            }
            Marshal.Copy(input, this.sampleBuffer, 0, sampleCount);

            lock (this.gate)
            {
                this.writer.WriteSamples(this.sampleBuffer.AsSpan(0, sampleCount));
                this.framesWritten += framesToWrite;
            }

            this.tap?.PushPcm16(this.sampleBuffer.AsSpan(0, sampleCount));

            if (this.cancelRequested)
            {
                return StreamCallbackResult.Abort;
            }
            if (this.stopRequested)
            {
                return StreamCallbackResult.Complete;
            }

            return StreamCallbackResult.Continue;
        }

        public Task StopAsync(CancellationToken ct)
        {
            this.stopRequested = true;
            this.StopStream();
            this.FinalizeRecording(canceled: false);
#pragma warning disable VSTHRD003
            return this.Completion;
#pragma warning restore VSTHRD003
        }

        public Task CancelAsync(CancellationToken ct)
        {
            this.cancelRequested = true;
            this.StopStream();
            this.FinalizeRecording(canceled: true);
#pragma warning disable VSTHRD003
            return this.Completion;
#pragma warning restore VSTHRD003
        }

        public ValueTask DisposeAsync()
        {
            this.StopStream();
            this.FinalizeRecording(canceled: this.cancelRequested);
            return ValueTask.CompletedTask;
        }

        private void StopStream()
        {
            if (Interlocked.Exchange(ref this.stopped, 1) == 1)
            {
                return;
            }

            try
            {
                this.stream?.Stop();
            }
            catch
            {
                // Ignore stream stop failures during teardown.
#pragma warning disable ERP022
            }
#pragma warning restore ERP022
            finally
            {
                this.stream?.Dispose();
                this.stream = null;
                ReleaseInitialization();
            }
        }

        private void FinalizeRecording(bool canceled)
        {
            lock (this.gate)
            {
                if (this.finalized)
                {
                    return;
                }
                this.finalized = true;
            }

            this.writer.Dispose();
            var duration = this.format.SampleRateHz > 0
                ? TimeSpan.FromSeconds(this.framesWritten / (double)this.format.SampleRateHz)
                : TimeSpan.Zero;
            var bytes = this.framesWritten * this.format.BlockAlign;

            if (canceled)
            {
                TryDelete(this.outputPath);
            }

            this.completion.TrySetResult(new AudioRecordingResult(
                canceled ? Array.Empty<string>() : new[] { this.outputPath },
                duration,
                this.format,
                bytes,
                canceled));
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Ignore cleanup failures.
#pragma warning disable ERP022
            }
#pragma warning restore ERP022
        }
    }
}
