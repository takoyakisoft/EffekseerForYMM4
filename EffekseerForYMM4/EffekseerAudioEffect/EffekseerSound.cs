using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Linq;

namespace EffekseerForYMM4
{
    public class EffekseerSound
    {
        public float[] Data { get; private set; }
        public int Channels { get; private set; }
        public int SampleRate { get; private set; }

        public EffekseerSound(float[] data, int channels, int sampleRate)
        {
            Data = data;
            Channels = channels;
            SampleRate = sampleRate;
        }
    }

    public class EffekseerVoice
    {
        public EffekseerSound Sound;
        public float Volume;
        public float Pan;
        public float Pitch;
        public bool Mode3D;
        public float X, Y, Z;
        public float Distance;
        
        public double Position; // In samples
        public bool IsPlaying = true;

        public EffekseerVoice(EffekseerSound sound, float volume, float pan, float pitch, bool mode3d, float x, float y, float z, float distance)
        {
            Sound = sound;
            Volume = volume;
            Pan = pan; // -1.0 to 1.0?
            Pitch = pitch;
            Mode3D = mode3d;
            X = x; Y = y; Z = z;
            Distance = distance;
            Position = 0;
        }
    }

    public class EffekseerSoundMixer
    {
        private Dictionary<int, EffekseerSound> sounds = new Dictionary<int, EffekseerSound>();
        private List<EffekseerVoice> voices = new List<EffekseerVoice>();
        private int nextId = 1;
        private object lockObj = new object();

        private int outputSampleRate = 44100; // Default
        
        // Listener Position
        private float listenerX = 0;
        private float listenerY = 0;
        private float listenerZ = 20;

        public EffekseerSoundMixer(int sampleRate)
        {
            outputSampleRate = sampleRate;
        }

        public void SetListenerPosition(float x, float y, float z)
        {
            lock (lockObj)
            {
                listenerX = x;
                listenerY = y;
                listenerZ = z;
            }
        }

        public int LoadSound(string path)
        {
            try
            {
                if (!File.Exists(path)) return -1;
                // Simple WAV loader
                var sound = WaveReader.Load(path);
                if (sound == null) return -1;

                lock (lockObj)
                {
                    int id = nextId++;
                    sounds[id] = sound;
                    return id;
                }
            }
            catch
            {
                return -1;
            }
        }

        public void UnloadSound(int id)
        {
            lock (lockObj)
            {
                if (sounds.ContainsKey(id))
                {
                    sounds.Remove(id);
                }
            }
        }

        public void PlaySound(int id, float volume, float pan, float pitch, bool mode3d, float x, float y, float z, float distance)
        {
            lock (lockObj)
            {
                if (sounds.TryGetValue(id, out var sound))
                {
                    // Effekseer volume is 0.0-1.0
                    // Pan is -1.0 to 1.0 usually?
                    // Pitch is usually 1.0 base?
                    voices.Add(new EffekseerVoice(sound, volume, pan, pitch, mode3d, x, y, z, distance));
                }
            }
        }

        public void Mix(float[] buffer, int offset, int count)
        {
            lock (lockObj)
            {
                for (int i = 0; i < voices.Count; i++)
                {
                    var voice = voices[i];
                    if (!voice.IsPlaying) continue;

                    // Pitch is octave shift (0.0 = original, 1.0 = +1 octave, -1.0 = -1 octave)
                    double speed = Math.Pow(2.0, voice.Pitch);
                    // If pitch is 1.0, and sample rates differ, we need to adjust speed
                    double rateRatio = (double)voice.Sound.SampleRate / outputSampleRate;
                    double step = speed * rateRatio;

                    // Simple nearest neighbor or linear interpolation?
                    // Let's do nearest for speed for now, or linear if easy.
                    // Stereo mixing.
                    
                    // Pan logic:
                    float leftVol = voice.Volume;
                    float rightVol = voice.Volume;

                    if (voice.Mode3D)
                    {
                        // 3D positioning
                        // voice.Distance is the "Distance" parameter in Effekseer (Attenuation distance)
                        // voice.X, Y, Z are relative position from listener? No, PlaySound passes relative position 
                        // if listener is at (0,0,0) in Effekseer's internal calculation.
                        // However, PlaySound in ManagerImplemented passes:
                        // position = transform.GetTranslation() + ...
                        // So X,Y,Z are World Coordinates.
                        // We set camera at (0,0,20).
                        
                        float ex = voice.X;
                        float ey = voice.Y;
                        float ez = voice.Z;

                        // Listener (Camera) position
                        float lx = listenerX;
                        float ly = listenerY;
                        float lz = listenerZ;

                        // Calculate distance from listener
                        float dx = ex - lx;
                        float dy = ey - ly;
                        float dz = ez - lz;
                        float dist = (float)Math.Sqrt(dx*dx + dy*dy + dz*dz);
                        
                        // Attenuation logic based on Effekseer's standard (Inverse distance clamped by param)
                        // Effekseer "Distance" param usually means "Reference Distance" where volume is 1.0 (or attenuation starts).
                        // Standard model: Volume = RefDist / (RefDist + (CurrentDist - RefDist) * Rolloff) ?
                        // Or simple: Volume = RefDist / CurrentDist (clamped when Current < Ref)
                        
                        // Let's assume voice.Distance is Reference Distance (min distance).
                        float refDist = Math.Max(0.1f, voice.Distance);
                        
                        if (dist < refDist)
                        {
                            // Within reference distance, no attenuation
                        }
                        else
                        {
                            // Simple inverse distance model
                            float attenuation = refDist / dist;
                            leftVol *= attenuation;
                            rightVol *= attenuation;
                        }

                        // 3D Panning (Left/Right balance)
                        // Check relative X from listener
                        // If dx > 0, sound is to the right.
                        if (dist > 0.001f)
                        {
                            float pan3d = dx / dist; // -1.0 to 1.0
                            pan3d = Math.Max(-1.0f, Math.Min(1.0f, pan3d));
                            
                            if (pan3d < 0) rightVol *= (1.0f + pan3d);
                            else if (pan3d > 0) leftVol *= (1.0f - pan3d);
                        }
                    }
                    else
                    {
                        // 2D Pan
                        if (voice.Pan < 0) rightVol *= (1.0f + voice.Pan);
                        else if (voice.Pan > 0) leftVol *= (1.0f - voice.Pan);
                    }

                    for (int j = 0; j < count; j += 2)
                    {
                        if (voice.Position >= voice.Sound.Data.Length / voice.Sound.Channels)
                        {
                            voice.IsPlaying = false;
                            break;
                        }

                        int sampleIndex = (int)voice.Position;
                        
                        float sampleL = 0;
                        float sampleR = 0;

                        if (voice.Sound.Channels == 1)
                        {
                            sampleL = voice.Sound.Data[sampleIndex];
                            sampleR = sampleL;
                        }
                        else if (voice.Sound.Channels == 2)
                        {
                            sampleL = voice.Sound.Data[sampleIndex * 2];
                            sampleR = voice.Sound.Data[sampleIndex * 2 + 1];
                        }

                        buffer[offset + j] += sampleL * leftVol;
                        buffer[offset + j + 1] += sampleR * rightVol;

                        voice.Position += step;
                    }
                }

                // Remove finished voices
                voices.RemoveAll(v => !v.IsPlaying);
            }
        }
    }

    public static class WaveReader
    {
        public static EffekseerSound Load(string path)
        {
            try
            {
                using (var fs = File.OpenRead(path))
                using (var reader = new BinaryReader(fs))
                {
                    // RIFF header
                    if (fs.Length < 12) return null;
                    if (new string(reader.ReadChars(4)) != "RIFF") return null;
                    reader.ReadInt32(); // File size
                    if (new string(reader.ReadChars(4)) != "WAVE") return null;

                    int channels = 0;
                    int sampleRate = 0;
                    int bitsPerSample = 0;
                    int audioFormat = 0;
                    
                    long dataChunkStart = -1;
                    int dataChunkSize = 0;

                    // チャンク走査（fmt探索 & data位置特定）
                    while (fs.Position < fs.Length)
                    {
                        if (fs.Length - fs.Position < 8) break;
                        
                        var chunkId = new string(reader.ReadChars(4));
                        var chunkSize = reader.ReadInt32();
                        
                        long chunkEnd = fs.Position + chunkSize;
                        
                        // パディング考慮
                        if (chunkSize % 2 != 0) chunkEnd++;

                        if (chunkId == "fmt ")
                        {
                            if (chunkSize >= 16)
                            {
                                audioFormat = reader.ReadInt16();
                                channels = reader.ReadInt16();
                                sampleRate = reader.ReadInt32();
                                reader.ReadInt32(); // Byte rate
                                reader.ReadInt16(); // Block align
                                bitsPerSample = reader.ReadInt16();
                            }
                        }
                        else if (chunkId == "data")
                        {
                            if (dataChunkStart == -1)
                            {
                                dataChunkStart = fs.Position;
                                dataChunkSize = chunkSize;
                            }
                        }

                        // 次のチャンクへ
                        if (chunkEnd > fs.Length) break;
                        fs.Seek(chunkEnd, SeekOrigin.Begin);
                    }

                    // 読み込み実行
                    if (channels > 0 && bitsPerSample > 0 && dataChunkStart != -1 && dataChunkSize > 0)
                    {
                        fs.Seek(dataChunkStart, SeekOrigin.Begin);
                        
                        int bytesPerSample = bitsPerSample / 8;
                        if (bytesPerSample == 0) return null;

                        int numSamples = dataChunkSize / bytesPerSample;
                        var data = new float[numSamples];

                        if (audioFormat == 1) // PCM
                        {
                            if (bitsPerSample == 16)
                            {
                                for (int i = 0; i < numSamples; i++)
                                {
                                    if (fs.Position >= dataChunkStart + dataChunkSize) break;
                                    data[i] = reader.ReadInt16() / 32768f;
                                }
                            }
                            else if (bitsPerSample == 8)
                            {
                                for (int i = 0; i < numSamples; i++)
                                {
                                    if (fs.Position >= dataChunkStart + dataChunkSize) break;
                                    data[i] = (reader.ReadByte() - 128) / 128f;
                                }
                            }
                        }
                        else if (audioFormat == 3) // Float
                        {
                            if (bitsPerSample == 32)
                            {
                                for (int i = 0; i < numSamples; i++)
                                {
                                    if (fs.Position >= dataChunkStart + dataChunkSize) break;
                                    data[i] = reader.ReadSingle();
                                }
                            }
                        }
                        
                        return new EffekseerSound(data, channels, sampleRate);
                    }
                }
            }
            catch
            {
                return null;
            }
            return null;
        }
    }
}
