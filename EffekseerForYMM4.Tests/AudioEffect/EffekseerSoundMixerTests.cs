using System.IO;

namespace EffekseerForYMM4.Tests.AudioEffect;

public sealed class EffekseerSoundMixerTests
{
    [Fact]
    public void LoadsAndMixesPcm16MonoWave()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.wav");
        try
        {
            WriteMonoWave(path, 22050, [0, short.MaxValue, 0, short.MinValue]);
            using var mixer = new EffekseerSoundMixer(44100);

            var soundId = mixer.LoadSound(path);
            Assert.True(soundId > 0);

            mixer.PlaySound(soundId, 1, 0, 0, false, 0, 0, 0, 1);
            var buffer = new float[16];
            mixer.Mix(buffer, 0, buffer.Length);

            Assert.Contains(buffer, static sample => Math.Abs(sample) > 0.25f);
            Assert.Equal(buffer[2], buffer[3]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void StopAllRemovesActiveVoices()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.wav");
        try
        {
            WriteMonoWave(path, 44100, [short.MaxValue, short.MaxValue]);
            using var mixer = new EffekseerSoundMixer(44100);
            var soundId = mixer.LoadSound(path);
            mixer.PlaySound(soundId, 1, 0, 0, false, 0, 0, 0, 1);

            mixer.StopAll();
            var buffer = new float[4];
            mixer.Mix(buffer, 0, buffer.Length);

            Assert.All(buffer, static sample => Assert.Equal(0, sample));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReusedVoicesDoNotAllocateOnTheMixingThread()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.wav");
        try
        {
            WriteMonoWave(
                path,
                44100,
                [short.MaxValue, short.MaxValue, short.MaxValue, short.MaxValue]);
            using var mixer = new EffekseerSoundMixer(44100);
            var soundId = mixer.LoadSound(path);
            var buffer = new float[8];

            mixer.PlaySound(soundId, 1, 0, 0, false, 0, 0, 0, 1);
            mixer.Mix(buffer, 0, buffer.Length);

            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            for (var iteration = 0; iteration < 100; iteration++)
            {
                Array.Clear(buffer);
                mixer.PlaySound(soundId, 1, 0, 0, false, 0, 0, 0, 1);
                mixer.Mix(buffer, 0, buffer.Length);
            }
            var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

            Assert.Equal(0, allocatedBytes);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void WriteMonoWave(string path, int sampleRate, short[] samples)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(36 + samples.Length * sizeof(short));
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(sampleRate);
        writer.Write(sampleRate * sizeof(short));
        writer.Write((short)sizeof(short));
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(samples.Length * sizeof(short));
        foreach (var sample in samples)
        {
            writer.Write(sample);
        }
    }
}
