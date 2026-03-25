using System;
using System.IO;
using System.Runtime.InteropServices;
using EffekseerForYMM4.Commons;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Audio;
using YukkuriMovieMaker.Player.Audio.Effects;

namespace EffekseerForYMM4.EffekseerAudioEffect
{
    internal class EffekseerAudioEffectProcessor : AudioEffectProcessorBase
    {
        private const int EffekseerFps = 60;
        private const int MaxReplaySteps = 600;
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int LoadSoundDelegate([MarshalAs(UnmanagedType.LPWStr)] string path);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void UnloadSoundDelegate(int id);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void PlaySoundDelegate(
            int id, 
            float volume, 
            float pan, 
            float pitch, 
            [MarshalAs(UnmanagedType.I1)] bool mode3d, // C++のbool(1byte)に合わせる
            float x, 
            float y, 
            float z, 
            float distance);

        readonly EffekseerAudioEffect item;
        readonly TimeSpan duration;

        private EffekseerForNative.EffekseerRenderer? nativeRenderer;
        private EffekseerSoundMixer? mixer;
        private LoadSoundDelegate? loadSoundDel;
        private UnloadSoundDelegate? unloadSoundDel;
        private PlaySoundDelegate? playSoundDel;

        private string? loadedFilePath;
        private double currentFrame = 0;
        private bool isInitialized = false;
        private long lastTimelineFrame = 0;
        private bool hasLastTimelineFrame = false;
        private readonly EffekseerLoadErrorNotifier loadErrorNotifier = new();

        //出力サンプリングレート。リサンプリング処理をしない場合はInputのHzをそのまま返す。
        public override int Hz => Input?.Hz ?? 44100;

        //出力するサンプル数
        public override long Duration => (long)(duration.TotalSeconds * Hz);

        public EffekseerAudioEffectProcessor(EffekseerAudioEffect item, TimeSpan duration)
        {
            this.item = item;
            this.duration = duration;
        }

        //シーク処理
        protected override void seek(long position)
        {
            Input?.Seek(position);
            hasLastTimelineFrame = false;
            currentFrame = 0;
        }

        //エフェクトを適用する
        protected override int read(float[] destBuffer, int offset, int count)
        {
            // First read input (if any)
            int readCount = Input?.Read(destBuffer, offset, count) ?? 0;
            
            // Clear remaining buffer if input ended or was silent
            if (readCount < count)
            {
                Array.Clear(destBuffer, offset + readCount, count - readCount);
            }

            if (!isInitialized)
            {
                Initialize();
            }

            if (nativeRenderer == null || mixer == null) return count;
            var renderer = nativeRenderer;
            var soundMixer = mixer;

            // Positionから現在のエフェクトフレームを計算（巻き戻し対応）
            long totalSampleFrames = (long)(duration.TotalSeconds * Hz);
            long currentSampleFrame = Position / 2; // Position is total samples (stereo), so divide by 2
            double targetFrame = (double)currentSampleFrame / Hz * EffekseerFps;
            long timelineFrame = (long)Math.Round((double)currentSampleFrame * EffekseerFps / Hz);
            
            // ファイル読み込み判定
            if (loadedFilePath != item.FilePath)
            {
                if (!string.IsNullOrEmpty(item.FilePath))
                {
                    var ext = Path.GetExtension(item.FilePath).ToLowerInvariant();
                    if (ext != ".efk" && ext != ".efkefc")
                    {
                        loadedFilePath = item.FilePath;
                        loadErrorNotifier.ShowIfNeeded(item.FilePath, string.Format(Translate.Error_InvalidEffectExtension, ".efk, .efkefc"));
                    }
                    else if (!File.Exists(item.FilePath))
                    {
                        loadedFilePath = item.FilePath;
                        loadErrorNotifier.ShowIfNeeded(item.FilePath, Translate.Error_EffectFileNotFound);
                    }
                    else if (renderer.LoadEffect(item.FilePath))
                    {
                        loadedFilePath = item.FilePath;
                        loadErrorNotifier.Reset();
                        currentFrame = 0; // 新しいファイル読み込み時にリセット
                        hasLastTimelineFrame = false;
                    }
                    else
                    {
                        loadedFilePath = item.FilePath;
                        loadErrorNotifier.ShowIfNeeded(item.FilePath, renderer.LastErrorMessage ?? Translate.Error_EffectFilesMayBeInvalid);
                    }
                }
                else
                {
                    renderer.Reset();
                    currentFrame = 0;
                    hasLastTimelineFrame = false;
                    loadErrorNotifier.Reset();
                }
                loadedFilePath = item.FilePath;
            }

            if (!string.IsNullOrEmpty(loadedFilePath))
            {
                int totalFrames = renderer.GetTotalFrame();
                
                // ループ処理
                if (item.IsLoop && totalFrames > 0 && totalFrames < int.MaxValue)
                {
                    targetFrame = targetFrame % totalFrames;
                }
                
                bool requiresReplay = RequiresReplay(timelineFrame);

                // Update Emitter Transform
                float ex = (float)item.PosX.GetValue(currentSampleFrame, totalSampleFrames, Hz);
                float ey = (float)item.PosY.GetValue(currentSampleFrame, totalSampleFrames, Hz);
                float ez = (float)item.PosZ.GetValue(currentSampleFrame, totalSampleFrames, Hz);
                renderer.SetLocation(ex, ey, ez);

                if (requiresReplay)
                {
                    ReplayRendererToTargetFrame(targetFrame);
                }

                float delta = (float)(targetFrame - currentFrame);
                if (!requiresReplay && delta > 0)
                {
                    renderer.Update(delta);
                    currentFrame = targetFrame;
                }

                // Update Camera Position
                // totalSampleFrames と currentSampleFrame は既に上部で計算済み
                float cx = (float)item.CamPosX.GetValue(currentSampleFrame, totalSampleFrames, Hz);
                float cy = (float)item.CamPosY.GetValue(currentSampleFrame, totalSampleFrames, Hz);
                float cz = (float)item.CamPosZ.GetValue(currentSampleFrame, totalSampleFrames, Hz);
                
                renderer.SetCameraLookAt(cx, cy, cz, 0, 0, 0, 0, 1, 0);
                
                // Also update listener position for sound mixer
                soundMixer.SetListenerPosition(cx, cy, cz);
            }

            // Mix Effekseer sound
            soundMixer.Mix(destBuffer, offset, count);

            // Apply Master Volume from item parameters (if using Animatable)
            // item.Volume.GetValue returns 0-100?
            // "F0", "%", 0, 100
            // Use local variable totalSampleFrames to avoid dependency on Duration property implementation
            long volTotalFrames = (long)(duration.TotalSeconds * Hz);
            double itemVol = item.Volume.GetValue(Position / 2, volTotalFrames, Hz);
            
            float masterVol = (float)(itemVol / 100.0);
            
            if (masterVol != 1.0f)
            {
                for (int i = 0; i < count; i++)
                {
                    destBuffer[offset + i] *= masterVol;
                }
            }

            return count; // Always return full count as we generate/mix
        }

        private void Initialize()
        {
            if (isInitialized) return;

            nativeRenderer = new EffekseerForNative.EffekseerRenderer();
            
            // Headless init
            if (!nativeRenderer.Initialize(IntPtr.Zero, IntPtr.Zero, 800, 600))
            {
               // Failed
               return;
            }

            mixer = new EffekseerSoundMixer(Hz);

            // Create delegates
            // Wrap LoadSound to resolve relative paths
            loadSoundDel = new LoadSoundDelegate((path) => 
            {
                if (!string.IsNullOrEmpty(path) && !Path.IsPathRooted(path))
                {
                    if (!string.IsNullOrEmpty(item.FilePath))
                    {
                        var effectDir = Path.GetDirectoryName(item.FilePath);
                        if (!string.IsNullOrEmpty(effectDir))
                        {
                            path = Path.Combine(effectDir, path);
                        }
                    }
                }
                return mixer!.LoadSound(path);
            });
            
            unloadSoundDel = new UnloadSoundDelegate(mixer!.UnloadSound);
            playSoundDel = new PlaySoundDelegate(mixer.PlaySound);

            nativeRenderer.SetSoundCallback(
                Marshal.GetFunctionPointerForDelegate(loadSoundDel),
                Marshal.GetFunctionPointerForDelegate(unloadSoundDel),
                Marshal.GetFunctionPointerForDelegate(playSoundDel)
            );

            // Set camera to mimic video effect default (at 0,0,20 looking at 0,0,0)
            nativeRenderer.SetCameraLookAt(0, 0, 20, 0, 0, 0, 0, 1, 0);

            isInitialized = true;
        }

        private bool RequiresReplay(long timelineFrame)
        {
            if (!hasLastTimelineFrame)
            {
                lastTimelineFrame = timelineFrame;
                hasLastTimelineFrame = true;
                return true;
            }

            var deltaFrames = timelineFrame - lastTimelineFrame;
            lastTimelineFrame = timelineFrame;
            return deltaFrames < 0 || deltaFrames > 1;
        }

        private void ReplayRendererToTargetFrame(double targetFrame)
        {
            if (nativeRenderer == null)
            {
                return;
            }

            nativeRenderer.Reset();
            currentFrame = 0;

            if (targetFrame <= 0)
            {
                return;
            }

            var replayStep = Math.Max(1.0, targetFrame / MaxReplaySteps);
            var replayed = 0.0;
            while (replayed < targetFrame)
            {
                var next = Math.Min(targetFrame, replayed + replayStep);
                nativeRenderer.Update((float)(next - replayed));
                replayed = next;
            }

            currentFrame = targetFrame;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                if (nativeRenderer != null)
                {
                    nativeRenderer.Destroy();
                    nativeRenderer.Dispose();
                    nativeRenderer = null;
                }
            }
            // Keep delegates alive until here? Yes.
        }
    }
}
