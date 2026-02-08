using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.IO;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Audio;
using YukkuriMovieMaker.Player.Audio.Effects;

namespace EffekseerForYMM4.EffekseerAudioEffect
{
    internal class EffekseerAudioEffectProcessor : AudioEffectProcessorBase
    {
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

        private string loadedFilePath = null;
        private double currentFrame = 0;
        private bool isInitialized = false;

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
            
            if (nativeRenderer != null)
            {
                nativeRenderer.Reset();
                
                // シーク後、エフェクトを最初から再生し直す
                // ※厳密なシーク位置の再現はUpdateの空回しが必要で重いため、
                //   音声プレビューとしては「頭出し再生」で妥協するのが一般的です。
                if (!string.IsNullOrEmpty(loadedFilePath))
                {
                    nativeRenderer.PlayEffect(loadedFilePath, 0, 0, 0);
                }
            }
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

            int sampleCount = count / 2;
            double deltaSeconds = (double)sampleCount / Hz;
            float deltaFrames = (float)(deltaSeconds * 60.0);

            // Positionから現在のエフェクトフレームを計算（巻き戻し対応）
            long totalSampleFrames = (long)(duration.TotalSeconds * Hz);
            long currentSampleFrame = Position / 2; // Position is total samples (stereo), so divide by 2
            double targetFrame = (double)currentSampleFrame / Hz * 60.0; // 60fps換算
            
            // ファイル読み込み判定
            if (loadedFilePath != item.FilePath)
            {
                if (!string.IsNullOrEmpty(item.FilePath))
                {
                    var ext = System.IO.Path.GetExtension(item.FilePath).ToLower();
                    if ((ext == ".efk" || ext == ".efkefc") && nativeRenderer.LoadEffect(item.FilePath))
                    {
                        loadedFilePath = item.FilePath;
                        currentFrame = 0; // 新しいファイル読み込み時にリセット
                    }
                }
                else
                {
                    nativeRenderer.Reset();
                    currentFrame = 0;
                }
                loadedFilePath = item.FilePath;
            }

            if (!string.IsNullOrEmpty(loadedFilePath))
            {
                int totalFrames = nativeRenderer.GetTotalFrame();
                
                // ループ処理
                if (item.IsLoop && totalFrames > 0 && totalFrames < int.MaxValue)
                {
                    targetFrame = targetFrame % totalFrames;
                }
                
                // 巻き戻し検出：targetFrameがcurrentFrameより小さい場合はReset
                if (targetFrame < currentFrame)
                {
                    nativeRenderer.Reset();
                    currentFrame = 0;
                }

                // 差分だけUpdate
                float delta = (float)(targetFrame - currentFrame);
                if (delta > 0)
                {
                    nativeRenderer.Update(delta);
                    currentFrame = targetFrame;
                }

                // Update Camera Position
                // totalSampleFrames と currentSampleFrame は既に上部で計算済み
                float cx = (float)item.CamPosX.GetValue(currentSampleFrame, totalSampleFrames, Hz);
                float cy = (float)item.CamPosY.GetValue(currentSampleFrame, totalSampleFrames, Hz);
                float cz = (float)item.CamPosZ.GetValue(currentSampleFrame, totalSampleFrames, Hz);
                
                nativeRenderer.SetCameraLookAt(cx, cy, cz, 0, 0, 0, 0, 1, 0);
                
                // Also update listener position for sound mixer
                mixer?.SetListenerPosition(cx, cy, cz);
            }

            // Mix Effekseer sound
            mixer.Mix(destBuffer, offset, count);

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
            playSoundDel = new PlaySoundDelegate(mixer!.PlaySound);

            nativeRenderer.SetSoundCallback(
                Marshal.GetFunctionPointerForDelegate(loadSoundDel),
                Marshal.GetFunctionPointerForDelegate(unloadSoundDel),
                Marshal.GetFunctionPointerForDelegate(playSoundDel)
            );

            // Set camera to mimic video effect default (at 0,0,20 looking at 0,0,0)
            nativeRenderer.SetCameraLookAt(0, 0, 20, 0, 0, 0, 0, 1, 0);

            isInitialized = true;
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