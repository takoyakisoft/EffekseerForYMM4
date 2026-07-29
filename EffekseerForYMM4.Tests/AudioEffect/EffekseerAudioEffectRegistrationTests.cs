using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using YukkuriMovieMaker.Player.Audio.Effects;
using YukkuriMovieMaker.Plugin.Effects;

namespace EffekseerForYMM4.Tests.AudioEffect;

public sealed class EffekseerAudioEffectRegistrationTests
{
    [Fact]
    public void EffectIsDiscoverableAsAnAudioEffect()
    {
        var type = typeof(EffekseerAudioEffect);

        Assert.True(typeof(AudioEffectBase).IsAssignableFrom(type));
        Assert.Contains(
            type.GetCustomAttributesData(),
            static attribute => attribute.AttributeType.Name == "AudioEffectAttribute");
        Assert.True(type.IsPublic);
    }

    [Fact]
    public void NativeEngineSupportsHeadlessAudioInitialization()
    {
        using var renderer = new EffekseerForNative.EffekseerRenderer();

        Assert.True(renderer.Initialize(IntPtr.Zero, IntPtr.Zero, 1, 1));
        renderer.SetSoundCallbacks(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        renderer.Update(1);
    }

    [Fact]
    public void EffekseerFileProducesAudioSamples()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Resources",
            "Laser01.efkefc");
        Assert.True(File.Exists(path));

        var effect = new EffekseerAudioEffect
        {
            FilePath = path,
            IsLoop = false,
        };
        using var input = new SilentAudioStream(44100, TimeSpan.FromSeconds(5));
        using var processor = effect.CreateAudioEffect(TimeSpan.FromSeconds(5));
        processor.Input = input;

        var buffer = new float[4096];
        var peak = 0f;
        while (processor.Position < processor.Duration)
        {
            var read = processor.Read(
                buffer,
                0,
                (int)Math.Min(buffer.Length, processor.Duration - processor.Position));
            if (read <= 0)
            {
                break;
            }

            for (var index = 0; index < read; index++)
            {
                peak = Math.Max(peak, Math.Abs(buffer[index]));
            }
        }

        Assert.True(peak > 0.001f, $"Expected Effekseer audio, but peak was {peak}.");
    }

    [Fact]
    public void ZeroEffectVolumePreservesInputAudio()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Resources",
            "Laser01.efkefc");
        Assert.True(File.Exists(path));

        var effect = new EffekseerAudioEffect
        {
            FilePath = path,
            IsLoop = false,
        };
        effect.Volume.Values[0].Value = 0;

        const float inputSample = 0.25f;
        using var input = new ConstantAudioStream(44100, TimeSpan.FromSeconds(1), inputSample);
        using var processor = effect.CreateAudioEffect(TimeSpan.FromSeconds(1));
        processor.Input = input;

        var buffer = new float[4096];
        var read = processor.Read(buffer, 0, buffer.Length);

        Assert.Equal(buffer.Length, read);
        Assert.All(buffer, sample => Assert.Equal(inputSample, sample));
    }

    private sealed class ConstantAudioStream(
        int hz,
        TimeSpan duration,
        float sample) : IAudioStream
    {
        private readonly long sampleCount = (long)(duration.TotalSeconds * hz) * 2;

        public int Hz { get; } = hz;
        public int Channel => 2;
        public long Duration => sampleCount / 2;
        public long Position { get; set; }

        public int Read(float[] buffer, int offset, int count)
        {
            var readable = (int)Math.Min(count, sampleCount - Position);
            if (readable <= 0)
            {
                return 0;
            }

            Array.Fill(buffer, sample, offset, readable);
            Position += readable;
            return readable;
        }

        public void Seek(long position) => Position = position * 2;

        public void Seek(TimeSpan time) =>
            Position = (long)(time.TotalSeconds * Hz) * 2;

        public void Dispose()
        {
        }
    }

    private sealed class SilentAudioStream(int hz, TimeSpan duration) : IAudioStream
    {
        private readonly long frameCount = (long)(duration.TotalSeconds * hz);

        public int Hz { get; } = hz;
        [SuppressMessage(
            "Performance",
            "CA1822:Mark members as static",
            Justification = "IAudioStream requires an instance Channel property.")]
        public int Channel => 2;
        public long Duration => frameCount;
        public long Position { get; set; }

        public int Read(float[] buffer, int offset, int count)
        {
            var readable = (int)Math.Min(count, frameCount * 2 - Position);
            if (readable <= 0)
            {
                return 0;
            }

            Array.Clear(buffer, offset, readable);
            Position += readable;
            return readable;
        }

        public void Seek(long position) => Position = position * 2;

        public void Seek(TimeSpan time) =>
            Position = (long)(time.TotalSeconds * Hz) * 2;

        public void Dispose()
        {
        }
    }
}
