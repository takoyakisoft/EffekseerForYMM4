using System;
using System.IO;
using Xunit;
using EffekseerForYMM4.EffekseerAudioEffect;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Audio.Effects;

namespace EffekseerForYMM4.Tests
{
    public class EffekseerAudioEffectTest
    {
        [Fact]
        public void TestAudioGeneration()
        {
            var effect = new EffekseerForYMM4.EffekseerAudioEffect.EffekseerAudioEffect();
            var resourcesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Laser01.efkefc");
            
            Assert.True(File.Exists(resourcesPath), $"Effect file not found: {resourcesPath}");
            
            // Process through EffekseerAudioEffectProcessor
            effect.FilePath = resourcesPath;
            effect.Volume.Values[0].Value = 100;

            // 5秒間の無音ソースを作成
            var duration = TimeSpan.FromSeconds(5);
            using var silentSource = new SilentSource(44100, duration);

            using var processor = effect.CreateAudioEffect(duration);
            processor.Input = silentSource;
            
            int sampleRate = processor.Hz; // Input.Hz (44100)
            Assert.Equal(44100, sampleRate);

            int bufferSize = sampleRate / 10 * 2; // 0.1 sec, Stereo
            float[] buffer = new float[bufferSize];
            var allSamples = new System.Collections.Generic.List<float>();

            // 5秒分回す
            long totalSamplesToRead = silentSource.Duration * 2; // Stereo
            long readTotal = 0;

            while (readTotal < totalSamplesToRead)
            {
                int count = (int)Math.Min(bufferSize, totalSamplesToRead - readTotal);
                int readCount = processor.Read(buffer, 0, count);
                
                if (readCount == 0) break;

                for (int i = 0; i < readCount; i++)
                {
                    allSamples.Add(buffer[i]);
                }
                readTotal += readCount;
            }
            
            // デスクトップに出力
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string outputPath = Path.Combine(desktopPath, "effekseer_test_output_5sec.wav");
            SaveWav(outputPath, allSamples.ToArray(), sampleRate, 2);
            
            // ログ出力（テストランナーには表示されないかもしれないが、Assertメッセージ等で確認可能）
            // Assert.True(false, $"Output saved to: {outputPath}"); // デバッグ用
        }

        [Fact]
        public void TestAudioGeneration_FarDistance()
        {
            var effect = new EffekseerForYMM4.EffekseerAudioEffect.EffekseerAudioEffect();
            var resourcesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Laser01.efkefc");
            
            Assert.True(File.Exists(resourcesPath), $"Effect file not found: {resourcesPath}");
            
            effect.FilePath = resourcesPath;
            effect.Volume.Values[0].Value = 100;

            // CamPosZ has default value of 20.
            if (effect.CamPosZ.Values.Count > 0)
            {
                effect.CamPosZ.Values[0].Value = 10000;
            }
            
            // Ensure X and Y are 0
            if (effect.CamPosX.Values.Count > 0) effect.CamPosX.Values[0].Value = 0;
            if (effect.CamPosY.Values.Count > 0) effect.CamPosY.Values[0].Value = 0;


            var duration = TimeSpan.FromSeconds(5);
            // 5 seconds silent source
            using var silentSource = new SilentSource(44100, duration);
            
            using var processor = effect.CreateAudioEffect(duration);
            processor.Input = silentSource;

            int sampleRate = 44100;
            // Just read chunks
            float[] buffer = new float[4096];
            System.Collections.Generic.List<float> allSamples = new System.Collections.Generic.List<float>();

            long totalSamplesToRead = silentSource.Duration * 2; // Stereo
            long readTotal = 0;
            int bufferSize = 4096;

            while (readTotal < totalSamplesToRead)
            {
                int count = (int)Math.Min(bufferSize, totalSamplesToRead - readTotal);
                // Processor Mix/Read updates camera based on animation parameters
                int readCount = processor.Read(buffer, 0, count);
                
                if (readCount == 0) break;

                for (int i = 0; i < readCount; i++)
                {
                    allSamples.Add(buffer[i]);
                }
                readTotal += readCount;
            }
            
            // Output to desktop
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string outputPath = Path.Combine(desktopPath, "effekseer_test_output_far.wav");
            SaveWav(outputPath, allSamples.ToArray(), sampleRate, 2);
        }

        private void SaveWav(string filename, float[] floatBuffer, int sampleRate, int channels)
        {
            using (var stream = new FileStream(filename, FileMode.Create))
            using (var writer = new BinaryWriter(stream))
            {
                // RIFF header
                writer.Write("RIFF".ToCharArray());
                writer.Write(36 + floatBuffer.Length * 2); // File size
                writer.Write("WAVE".ToCharArray());

                // fmt chunk
                writer.Write("fmt ".ToCharArray());
                writer.Write(16); // Chunk size
                writer.Write((short)1); // PCM
                writer.Write((short)channels);
                writer.Write(sampleRate);
                writer.Write(sampleRate * channels * 2); // Byte rate
                writer.Write((short)(channels * 2)); // Block align
                writer.Write((short)16); // Bits per sample

                // data chunk
                writer.Write("data".ToCharArray());
                writer.Write(floatBuffer.Length * 2);

                // Convert float to Int16
                foreach (var sample in floatBuffer)
                {
                    short s = (short)(Math.Max(-1.0f, Math.Min(1.0f, sample)) * 32767);
                    writer.Write(s);
                }
            }
        }

        // 簡易的な無音ソース
        class SilentSource : IAudioStream
        {
            private readonly long length;
            public int Hz { get; }
            public int Channel => 2; // Stereo
            public long Duration => length;
            public long Position { get; set; }

            public SilentSource(int hz, TimeSpan duration)
            {
                Hz = hz;
                length = (long)(duration.TotalSeconds * hz);
            }

            public int Read(float[] buffer, int offset, int count)
            {
                // 無音で埋める
                Array.Clear(buffer, offset, count);
                
                long remaining = (length * 2) - Position; // Stereo samples
                int readCount = (int)Math.Min(count, remaining);
                
                Position += readCount;
                
                return readCount; 
            }

            public void Seek(long position)
            {
                Position = position * 2; // Frames to Samples (Stereo)
            }

            public void Seek(TimeSpan time)
            {
                Position = (long)(time.TotalSeconds * Hz) * 2; // Time to Samples (Stereo)
            }

            public void Dispose()
            {
            }
        }
    }
}
