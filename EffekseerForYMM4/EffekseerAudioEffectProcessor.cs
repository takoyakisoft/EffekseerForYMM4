using System.IO;
using System.Runtime.InteropServices;
using EffekseerForYMM4.Commons;
using EffekseerForYMM4.Diagnostics;
using YukkuriMovieMaker.Player.Audio.Effects;

namespace EffekseerForYMM4;

internal sealed class EffekseerAudioEffectProcessor : AudioEffectProcessorBase
{
    private const double EffekseerFps = 60.0;
    private const double MaxSimulationAdvanceFrames = 8.0;

    private enum PlaybackAccessKind
    {
        Initial,
        Continuous,
        Random,
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int LoadSoundDelegate([MarshalAs(UnmanagedType.LPUTF8Str)] string path);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void UnloadSoundDelegate(int id);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void PlaySoundDelegate(
        int id,
        float volume,
        float pan,
        float pitch,
        [MarshalAs(UnmanagedType.I1)] bool mode3d,
        float x,
        float y,
        float z,
        float distance);

    private readonly EffekseerAudioEffect item;
    private readonly TimeSpan duration;
    private readonly EffekseerSoundMixer mixer;
    private readonly EffekseerForNative.EffekseerRenderer renderer;
    private readonly LoadSoundDelegate loadSound;
    private readonly UnloadSoundDelegate unloadSound;
    private readonly PlaySoundDelegate playSound;
    private readonly EffekseerLoadErrorNotifier loadErrorNotifier = new();

    private string? loadedFilePath;
    private string? lastReadErrorKey;
    private long rendererSampleFrame;
    private long cachedLoopSampleFrames;
    private int cachedLoopHz;
    private int loadedTotalFrames;
    private bool hasLoadedEffect;
    private bool hasPreviousRead;
    private bool disposed;

    public override int Hz => Input?.Hz ?? 44100;

    public override long Duration => (long)(duration.TotalSeconds * Hz) * 2;

    public EffekseerAudioEffectProcessor(EffekseerAudioEffect item, TimeSpan duration)
    {
        this.item = item;
        this.duration = duration;
        mixer = new EffekseerSoundMixer(Hz);
        renderer = new EffekseerForNative.EffekseerRenderer();

        loadSound = LoadSound;
        unloadSound = mixer.UnloadSound;
        playSound = mixer.PlaySound;

        if (!renderer.Initialize(IntPtr.Zero, IntPtr.Zero, 1, 1))
        {
            PluginLog.Warning(
                $"Native audio engine initialization failed. detail={renderer.LastErrorMessage}");
            throw new InvalidOperationException(
                renderer.LastErrorMessage ?? "Failed to initialize the Effekseer audio engine.");
        }

        renderer.SetSoundCallbacks(
            Marshal.GetFunctionPointerForDelegate(loadSound),
            Marshal.GetFunctionPointerForDelegate(unloadSound),
            Marshal.GetFunctionPointerForDelegate(playSound));
        renderer.SetCameraLookAt(0, 0, 20, 0, 0, 0, 0, 1, 0);
        PluginLog.Information("Audio effect processor created");
    }

    protected override void seek(long position)
    {
        Input?.Seek(position);
    }

    protected override int read(float[] destBuffer, int offset, int count)
    {
        try
        {
            var result = ReadCore(destBuffer, offset, count);
            lastReadErrorKey = null;
            return result;
        }
        catch (Exception exception)
        {
            var errorKey = $"{exception.GetType().FullName}|{exception.HResult}|{exception.Message}";
            if (!string.Equals(lastReadErrorKey, errorKey, StringComparison.Ordinal))
            {
                lastReadErrorKey = errorKey;
                PluginLog.Error(
                    $"Audio effect read failed. path={item.FilePath}, sample={Position}",
                    exception);
            }

            throw;
        }
    }

    private int ReadCore(float[] destBuffer, int offset, int count)
    {
        var sampleRate = Hz;
        var startSampleFrame = Position / 2;
        mixer.SetOutputSampleRate(sampleRate);
        var inputRead = Input?.Read(destBuffer, offset, count) ?? 0;
        if (inputRead < count)
        {
            Array.Clear(destBuffer, offset + inputRead, count - inputRead);
        }

        if (!EnsureEffectLoaded())
        {
            return count;
        }

        ApplySpatialParameters(startSampleFrame, sampleRate);
        SynchronizeRenderer(startSampleFrame, sampleRate);

        var stereoSamplesRemaining = count - (count & 1);
        var writeOffset = offset;
        while (stereoSamplesRemaining > 0)
        {
            RestartLoopIfNeeded(sampleRate);
            var sampleFrames = GetNextStepSampleFrames(
                stereoSamplesRemaining / 2,
                sampleRate);
            renderer.Update((float)(sampleFrames * EffekseerFps / sampleRate));
            mixer.Mix(destBuffer, writeOffset, sampleFrames * 2);
            rendererSampleFrame += sampleFrames;
            writeOffset += sampleFrames * 2;
            stereoSamplesRemaining -= sampleFrames * 2;
        }

        ApplyMasterVolume(destBuffer, offset, count, startSampleFrame, sampleRate);
        return count;
    }

    private bool EnsureEffectLoaded()
    {
        if (string.Equals(loadedFilePath, item.FilePath, StringComparison.Ordinal))
        {
            return hasLoadedEffect;
        }

        mixer.StopAll();
        ResetPlaybackTracking();
        cachedLoopSampleFrames = 0;
        cachedLoopHz = 0;
        loadedTotalFrames = 0;
        hasLoadedEffect = false;
        loadedFilePath = item.FilePath;

        if (string.IsNullOrWhiteSpace(item.FilePath))
        {
            renderer.Reset();
            loadErrorNotifier.Reset();
            return false;
        }

        var extension = Path.GetExtension(item.FilePath);
        if (!string.Equals(extension, ".efk", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(extension, ".efkefc", StringComparison.OrdinalIgnoreCase))
        {
            loadErrorNotifier.ShowIfNeeded(
                item.FilePath,
                string.Format(
                    System.Globalization.CultureInfo.CurrentCulture,
                    Translate.Error_InvalidEffectExtension,
                    ".efk, .efkefc"));
            return false;
        }

        if (!File.Exists(item.FilePath))
        {
            loadErrorNotifier.ShowIfNeeded(item.FilePath, Translate.Error_EffectFileNotFound);
            return false;
        }

        if (!renderer.LoadEffect(item.FilePath))
        {
            loadErrorNotifier.ShowIfNeeded(
                item.FilePath,
                renderer.LastErrorMessage ?? Translate.Error_EffectFilesMayBeInvalid);
            return false;
        }

        hasLoadedEffect = true;
        loadedTotalFrames = renderer.GetTotalFrame();
        loadErrorNotifier.Reset();
        if (PluginLog.IsEnabled(PluginLogLevel.Information))
        {
            PluginLog.Information(
                $"Audio effect loaded. path={item.FilePath}, totalFrames={loadedTotalFrames}");
        }
        return true;
    }

    private int LoadSound(string path)
    {
        try
        {
            if (!Path.IsPathRooted(path) && !string.IsNullOrEmpty(item.FilePath))
            {
                var directory = Path.GetDirectoryName(item.FilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    path = Path.Combine(directory, path);
                }
            }
            var soundId = mixer.LoadSound(path);
            if (soundId < 0)
            {
                PluginLog.Warning($"Sound resource load failed. path={path}");
            }
            else if (PluginLog.IsEnabled(PluginLogLevel.Debug))
            {
                PluginLog.Debug($"Sound resource loaded. path={path}, id={soundId}");
            }
            return soundId;
        }
        catch (Exception exception)
        {
            PluginLog.Error($"Sound loading callback failed. path={path}", exception);
            return -1;
        }
    }

    private void SynchronizeRenderer(long targetSampleFrame, int sampleRate)
    {
        var target = Math.Max(0, targetSampleFrame);
        switch (ClassifyPlaybackAccess(target))
        {
            case PlaybackAccessKind.Initial:
                InitializePlayback(target, sampleRate);
                break;
            case PlaybackAccessKind.Continuous:
                break;
            case PlaybackAccessKind.Random:
                RestartPlaybackAt(target, sampleRate);
                break;
        }
        hasPreviousRead = true;
    }

    private PlaybackAccessKind ClassifyPlaybackAccess(long targetSampleFrame)
    {
        if (!hasPreviousRead)
        {
            return PlaybackAccessKind.Initial;
        }

        return targetSampleFrame == rendererSampleFrame
            ? PlaybackAccessKind.Continuous
            : PlaybackAccessKind.Random;
    }

    private void InitializePlayback(long targetSampleFrame, int sampleRate)
    {
        ReplayRendererAt(targetSampleFrame, sampleRate);
    }

    private void RestartPlaybackAt(long targetSampleFrame, int sampleRate)
    {
        mixer.StopAll();
        renderer.Reset();
        ReplayRendererAt(targetSampleFrame, sampleRate);
    }

    private void ReplayRendererAt(long targetSampleFrame, int sampleRate)
    {
        var loopLength = GetLoopLengthInSampleFrames(sampleRate);
        var replayTarget = loopLength > 0
            ? targetSampleFrame % loopLength
            : targetSampleFrame;
        var maxReplaySampleFrames = Math.Max(
            1L,
            (long)Math.Ceiling(MaxSimulationAdvanceFrames * sampleRate / EffekseerFps));
        var replaySampleFrames = Math.Min(replayTarget, maxReplaySampleFrames);
        var replayStartSampleFrame = replayTarget - replaySampleFrames;

        // Match the video processor's random-access model: jump close to the
        // absolute timeline position, then replay only a small trailing window.
        // Sounds emitted during the coarse jump cannot be positioned accurately,
        // so discard them and reconstruct only the bounded trailing window.
        if (replayStartSampleFrame > 0)
        {
            renderer.Update((float)(replayStartSampleFrame * EffekseerFps / sampleRate));
            mixer.StopAll();
        }

        AdvanceReplay(replaySampleFrames, sampleRate);
        rendererSampleFrame = targetSampleFrame;
    }

    private void AdvanceReplay(long sampleFrames, int sampleRate)
    {
        var remaining = sampleFrames;
        var framesPerStep = Math.Max(1, sampleRate / (int)EffekseerFps);
        while (remaining > 0)
        {
            var step = Math.Min(framesPerStep, remaining);
            renderer.Update((float)(step * EffekseerFps / sampleRate));
            mixer.Advance(step);
            remaining -= step;
        }
    }

    private void ResetPlaybackTracking()
    {
        hasPreviousRead = false;
        rendererSampleFrame = 0;
    }

    private long GetLoopLengthInSampleFrames(int sampleRate)
    {
        if (!item.IsLoop)
        {
            return 0;
        }

        if (cachedLoopHz != sampleRate)
        {
            cachedLoopHz = sampleRate;
            cachedLoopSampleFrames =
                loadedTotalFrames > 0 && loadedTotalFrames < int.MaxValue
                    ? Math.Max(
                        1,
                        (long)Math.Ceiling(loadedTotalFrames * sampleRate / EffekseerFps))
                    : 0;
        }

        return cachedLoopSampleFrames;
    }

    private void RestartLoopIfNeeded(int sampleRate)
    {
        var loopLength = GetLoopLengthInSampleFrames(sampleRate);
        if (loopLength > 0 &&
            rendererSampleFrame > 0 &&
            rendererSampleFrame % loopLength == 0)
        {
            mixer.StopAll();
            renderer.Reset();
        }
    }

    private int GetNextStepSampleFrames(int requestedFrames, int sampleRate)
    {
        var result = Math.Min(
            requestedFrames,
            Math.Max(1, sampleRate / (int)EffekseerFps));
        var loopLength = GetLoopLengthInSampleFrames(sampleRate);
        if (loopLength <= 0)
        {
            return result;
        }

        var positionInLoop = rendererSampleFrame % loopLength;
        var untilLoop = loopLength - positionInLoop;
        return (int)Math.Min(result, untilLoop);
    }

    private void ApplySpatialParameters(long sampleFrame, int sampleRate)
    {
        var totalFrames = Math.Max(1L, (long)(duration.TotalSeconds * sampleRate));
        var x = (float)item.PosX.GetValue(sampleFrame, totalFrames, sampleRate);
        var y = (float)item.PosY.GetValue(sampleFrame, totalFrames, sampleRate);
        var z = (float)item.PosZ.GetValue(sampleFrame, totalFrames, sampleRate);
        renderer.SetLocation(x, y, z);

        var cameraX = (float)item.CamPosX.GetValue(sampleFrame, totalFrames, sampleRate);
        var cameraY = (float)item.CamPosY.GetValue(sampleFrame, totalFrames, sampleRate);
        var cameraZ = (float)item.CamPosZ.GetValue(sampleFrame, totalFrames, sampleRate);
        renderer.SetCameraLookAt(cameraX, cameraY, cameraZ, 0, 0, 0, 0, 1, 0);
        mixer.SetListenerPosition(cameraX, cameraY, cameraZ);
    }

    private void ApplyMasterVolume(
        float[] buffer,
        int offset,
        int count,
        long startSampleFrame,
        int sampleRate)
    {
        var totalFrames = Math.Max(1L, (long)(duration.TotalSeconds * sampleRate));
        if (item.Volume.Values.Count <= 1)
        {
            var volume = (float)item.Volume.GetValue(
                startSampleFrame,
                totalFrames,
                sampleRate) / 100f;
            if (volume == 1f)
            {
                return;
            }

            for (var index = 0; index < count; index++)
            {
                buffer[offset + index] *= volume;
            }
            return;
        }

        for (var index = 0; index < count; index += 2)
        {
            var volume = (float)item.Volume.GetValue(
                startSampleFrame + index / 2,
                totalFrames,
                sampleRate) / 100f;
            buffer[offset + index] *= volume;
            if (index + 1 < count)
            {
                buffer[offset + index + 1] *= volume;
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposed && disposing)
        {
            disposed = true;
            renderer.Dispose();
            mixer.Dispose();
            PluginLog.Information("Audio effect processor disposed");
        }
        base.Dispose(disposing);
    }
}
