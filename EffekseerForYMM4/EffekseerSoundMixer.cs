using System.IO;

namespace EffekseerForYMM4;

internal sealed class EffekseerSoundMixer : IDisposable
{
    private sealed class Sound(float[] samples, int channels, int sampleRate)
    {
        public float[] Samples { get; } = samples;
        public int Channels { get; } = channels;
        public int SampleRate { get; } = sampleRate;
        public int FrameCount => Samples.Length / Channels;
    }

    private sealed class Voice
    {
        public Sound Sound { get; private set; } = null!;
        public float Volume { get; private set; }
        public float Pan { get; private set; }
        public bool Mode3D { get; private set; }
        public float X { get; private set; }
        public float Y { get; private set; }
        public float Z { get; private set; }
        public float Distance { get; private set; }
        public double Position { get; set; }
        public double PitchScale { get; private set; }

        public void Initialize(
            Sound sound,
            float volume,
            float pan,
            float pitch,
            bool mode3d,
            float x,
            float y,
            float z,
            float distance)
        {
            Sound = sound;
            Volume = volume;
            Pan = pan;
            Mode3D = mode3d;
            X = x;
            Y = y;
            Z = z;
            Distance = distance;
            Position = 0;
            PitchScale = Math.Pow(2.0, pitch);
        }

        public void Reset()
        {
            Sound = null!;
        }
    }

    private const int MaximumConcurrentVoices = 64;

    private readonly Lock sync = new();
    private readonly Dictionary<int, Sound> sounds = [];
    private readonly List<Voice> voices = new(MaximumConcurrentVoices);
    private readonly Stack<Voice> voicePool = new(MaximumConcurrentVoices);
    private int outputSampleRate;
    private int nextSoundId = 1;
    private float listenerX;
    private float listenerY;
    private float listenerZ = 20;
    private bool disposed;

    public EffekseerSoundMixer(int outputSampleRate)
    {
        for (var index = 0; index < MaximumConcurrentVoices; index++)
        {
            voicePool.Push(new Voice());
        }
        SetOutputSampleRate(outputSampleRate);
    }

    public void SetOutputSampleRate(int sampleRate)
    {
        var normalizedSampleRate = Math.Max(1, sampleRate);
        if (Volatile.Read(ref outputSampleRate) == normalizedSampleRate)
        {
            return;
        }

        lock (sync)
        {
            outputSampleRate = normalizedSampleRate;
        }
    }

    public void SetListenerPosition(float x, float y, float z)
    {
        lock (sync)
        {
            listenerX = x;
            listenerY = y;
            listenerZ = z;
        }
    }

    public int LoadSound(string path)
    {
        var wave = WaveReader.Load(path);
        if (wave is null)
        {
            return -1;
        }

        lock (sync)
        {
            if (disposed)
            {
                return -1;
            }

            var id = nextSoundId++;
            sounds.Add(id, new Sound(wave.Value.Samples, wave.Value.Channels, wave.Value.SampleRate));
            return id;
        }
    }

    public void UnloadSound(int id)
    {
        lock (sync)
        {
            sounds.Remove(id);
        }
    }

    public void PlaySound(
        int id,
        float volume,
        float pan,
        float pitch,
        bool mode3d,
        float x,
        float y,
        float z,
        float distance)
    {
        lock (sync)
        {
            if (!disposed && sounds.TryGetValue(id, out var sound))
            {
                if (!voicePool.TryPop(out var voice))
                {
                    return;
                }
                voice.Initialize(
                    sound,
                    volume,
                    pan,
                    pitch,
                    mode3d,
                    x,
                    y,
                    z,
                    distance);
                voices.Add(voice);
            }
        }
    }

    public void Mix(float[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (offset > buffer.Length - count)
        {
            throw new ArgumentException("The buffer range is outside the destination array.");
        }

        lock (sync)
        {
            var stereoFrameCount = count / 2;
            foreach (var voice in voices)
            {
                CalculateChannelVolumes(voice, out var leftVolume, out var rightVolume);
                var step = GetStep(voice);

                for (var frame = 0; frame < stereoFrameCount; frame++)
                {
                    if (voice.Position >= voice.Sound.FrameCount)
                    {
                        break;
                    }

                    ReadInterpolated(voice.Sound, voice.Position, out var left, out var right);
                    var target = offset + frame * 2;
                    buffer[target] += left * leftVolume;
                    buffer[target + 1] += right * rightVolume;
                    voice.Position += step;
                }
            }
            RecycleFinishedVoices();
        }
    }

    public void Advance(long sampleFrames)
    {
        if (sampleFrames <= 0)
        {
            return;
        }

        lock (sync)
        {
            foreach (var voice in voices)
            {
                voice.Position += sampleFrames * GetStep(voice);
            }
            RecycleFinishedVoices();
        }
    }

    public void StopAll()
    {
        lock (sync)
        {
            for (var index = 0; index < voices.Count; index++)
            {
                ReturnVoice(voices[index]);
            }
            voices.Clear();
        }
    }

    private void RecycleFinishedVoices()
    {
        for (var index = voices.Count - 1; index >= 0; index--)
        {
            var voice = voices[index];
            if (voice.Position < voice.Sound.FrameCount)
            {
                continue;
            }

            voices.RemoveAt(index);
            ReturnVoice(voice);
        }
    }

    private void ReturnVoice(Voice voice)
    {
        voice.Reset();
        voicePool.Push(voice);
    }

    private double GetStep(Voice voice) =>
        voice.PitchScale * voice.Sound.SampleRate / outputSampleRate;

    private void CalculateChannelVolumes(
        Voice voice,
        out float leftVolume,
        out float rightVolume)
    {
        leftVolume = voice.Volume;
        rightVolume = voice.Volume;

        float pan;
        if (voice.Mode3D)
        {
            var dx = voice.X - listenerX;
            var dy = voice.Y - listenerY;
            var dz = voice.Z - listenerZ;
            var actualDistance = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
            var referenceDistance = Math.Max(0.1f, voice.Distance);
            if (actualDistance > referenceDistance)
            {
                var attenuation = referenceDistance / actualDistance;
                leftVolume *= attenuation;
                rightVolume *= attenuation;
            }
            pan = actualDistance > 0.001f
                ? Math.Clamp(dx / actualDistance, -1f, 1f)
                : 0f;
        }
        else
        {
            pan = Math.Clamp(voice.Pan, -1f, 1f);
        }

        if (pan < 0)
        {
            rightVolume *= 1 + pan;
        }
        else
        {
            leftVolume *= 1 - pan;
        }
    }

    private static void ReadInterpolated(
        Sound sound,
        double position,
        out float left,
        out float right)
    {
        var firstFrame = Math.Min((int)position, sound.FrameCount - 1);
        var secondFrame = Math.Min(firstFrame + 1, sound.FrameCount - 1);
        var fraction = (float)(position - firstFrame);
        ReadFrame(sound, firstFrame, out var firstLeft, out var firstRight);
        ReadFrame(sound, secondFrame, out var secondLeft, out var secondRight);
        left = firstLeft + (secondLeft - firstLeft) * fraction;
        right = firstRight + (secondRight - firstRight) * fraction;
    }

    private static void ReadFrame(Sound sound, int frame, out float left, out float right)
    {
        var index = frame * sound.Channels;
        left = sound.Samples[index];
        right = sound.Channels == 1 ? left : sound.Samples[index + 1];
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            for (var index = 0; index < voices.Count; index++)
            {
                voices[index].Reset();
            }
            voices.Clear();
            voicePool.Clear();
            sounds.Clear();
        }
    }

    private static class WaveReader
    {
        internal readonly record struct WaveData(float[] Samples, int Channels, int SampleRate);

        public static WaveData? Load(string path)
        {
            try
            {
                using var stream = File.OpenRead(path);
                using var reader = new BinaryReader(stream);
                if (stream.Length < 12 ||
                    ReadFourCc(reader) != "RIFF" ||
                    reader.ReadUInt32() > stream.Length ||
                    ReadFourCc(reader) != "WAVE")
                {
                    return null;
                }

                ushort format = 0;
                ushort channels = 0;
                int sampleRate = 0;
                ushort bitsPerSample = 0;
                byte[]? audioBytes = null;

                while (stream.Position <= stream.Length - 8)
                {
                    var chunkId = ReadFourCc(reader);
                    var chunkSize = reader.ReadUInt32();
                    var chunkStart = stream.Position;
                    var chunkEnd = chunkStart + chunkSize;
                    if (chunkEnd > stream.Length)
                    {
                        return null;
                    }

                    if (chunkId == "fmt " && chunkSize >= 16)
                    {
                        format = reader.ReadUInt16();
                        channels = reader.ReadUInt16();
                        sampleRate = reader.ReadInt32();
                        _ = reader.ReadUInt32();
                        _ = reader.ReadUInt16();
                        bitsPerSample = reader.ReadUInt16();
                        if (format == 0xfffe && chunkSize >= 40)
                        {
                            _ = reader.ReadUInt16();
                            _ = reader.ReadUInt16();
                            _ = reader.ReadUInt32();
                            format = reader.ReadUInt16();
                        }
                    }
                    else if (chunkId == "data" && chunkSize <= int.MaxValue)
                    {
                        audioBytes = reader.ReadBytes((int)chunkSize);
                        if (audioBytes.Length != (int)chunkSize)
                        {
                            return null;
                        }
                    }

                    stream.Position = chunkEnd + (chunkSize & 1);
                }

                if (audioBytes is null ||
                    channels is < 1 or > 2 ||
                    sampleRate <= 0 ||
                    !IsSupported(format, bitsPerSample))
                {
                    return null;
                }

                var bytesPerSample = bitsPerSample / 8;
                var sampleCount = audioBytes.Length / bytesPerSample;
                var samples = new float[sampleCount];
                for (var index = 0; index < sampleCount; index++)
                {
                    samples[index] = ReadSample(
                        audioBytes.AsSpan(index * bytesPerSample, bytesPerSample),
                        format,
                        bitsPerSample);
                }
                return new WaveData(samples, channels, sampleRate);
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static bool IsSupported(ushort format, ushort bitsPerSample) =>
            format == 1 && bitsPerSample is 8 or 16 or 24 or 32 ||
            format == 3 && bitsPerSample == 32;

        private static float ReadSample(
            ReadOnlySpan<byte> bytes,
            ushort format,
            ushort bitsPerSample)
        {
            if (format == 3)
            {
                return BitConverter.Int32BitsToSingle(
                    System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(bytes));
            }

            return bitsPerSample switch
            {
                8 => (bytes[0] - 128) / 128f,
                16 => System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(bytes) / 32768f,
                24 => ReadInt24(bytes) / 8388608f,
                32 => System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(bytes) / 2147483648f,
                _ => 0,
            };
        }

        private static int ReadInt24(ReadOnlySpan<byte> bytes)
        {
            var value = bytes[0] | bytes[1] << 8 | bytes[2] << 16;
            return (value & 0x800000) == 0 ? value : value | unchecked((int)0xff000000);
        }

        private static string ReadFourCc(BinaryReader reader) =>
            new(reader.ReadChars(4));
    }
}
