using System.IO;

namespace EffekseerForYMM4.Tests.Interop;

public sealed class RenderStateSafetyTests
{
    [Fact]
    public void VideoProcessorKeepsSimulationOutsideSharedGraphicsContextLock()
    {
        var root = FindRepositoryRoot();
        var processorPath = Path.Combine(
            root,
            "EffekseerForYMM4",
            "EffekseerVideoEffectProcessor.cs");
        var source = File.ReadAllText(processorPath);

        Assert.Contains("private static readonly Lock RenderLock = new();", source, StringComparison.Ordinal);
        var animationIndex = source.IndexOf("double animFrame", StringComparison.Ordinal);
        Assert.True(animationIndex >= 0);
        var renderLockIndex = source.IndexOf("lock (RenderLock)", animationIndex, StringComparison.Ordinal);
        Assert.True(renderLockIndex > animationIndex);
    }

    [Fact]
    public void NativeRendererSynchronizesLodViewerPositionWithCamera()
    {
        var root = FindRepositoryRoot();
        var sourcePath = Path.Combine(
            root,
            "EffekseerForNative",
            "src",
            "Core",
            "EffectsManager.cpp");
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("auto layerParameter = manager_->GetLayerParameter(0);", source, StringComparison.Ordinal);
        Assert.Contains("layerParameter.ViewerPosition =", source, StringComparison.Ordinal);
        Assert.Contains("::Effekseer::Vector3D(positionX, positionY, positionZ);", source, StringComparison.Ordinal);
        Assert.Contains("manager_->SetLayerParameter(0, layerParameter);", source, StringComparison.Ordinal);
    }

    [Fact]
    public void NativeRendererRestoresViewportAndAllRenderTargets()
    {
        var root = FindRepositoryRoot();
        var sourcePath = Path.Combine(
            root,
            "EffekseerForNative",
            "src",
            "Core",
            "EffectsManager.cpp");
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("D3D11_SIMULTANEOUS_RENDER_TARGET_COUNT", source, StringComparison.Ordinal);
        Assert.Contains("OMGetRenderTargets", source, StringComparison.Ordinal);
        Assert.Contains("RSGetViewports", source, StringComparison.Ordinal);
        Assert.Contains("RSSetViewports", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PlaybackUsesBoundedRandomAccessAndExactContinuousAdvance()
    {
        var root = FindRepositoryRoot();
        var processorPath = Path.Combine(
            root,
            "EffekseerForYMM4",
            "EffekseerVideoEffectProcessor.cs");
        var source = File.ReadAllText(processorPath);

        Assert.Contains("enum PlaybackAccessKind", source, StringComparison.Ordinal);
        Assert.Contains("PlaybackAccessKind.Initial", source, StringComparison.Ordinal);
        Assert.Contains("PlaybackAccessKind.Continuous", source, StringComparison.Ordinal);
        Assert.Contains("PlaybackAccessKind.Random", source, StringComparison.Ordinal);
        Assert.Contains("nativeRenderer?.Reset();", source, StringComparison.Ordinal);
        Assert.Contains("private void ReplayRendererAt(double targetFrame)", source, StringComparison.Ordinal);
        Assert.Contains("var replayFrames = Math.Min(targetFrame, MaxSimulationAdvanceFrames);", source, StringComparison.Ordinal);
        Assert.Contains("hasAppliedCamera", source, StringComparison.Ordinal);
        Assert.Contains("hasAppliedProjection", source, StringComparison.Ordinal);
        Assert.Contains("hasAppliedTransform", source, StringComparison.Ordinal);

        var advanceStart = source.IndexOf("private void AdvanceRenderer", StringComparison.Ordinal);
        var replayStart = source.IndexOf("private void ReplayRendererAt", StringComparison.Ordinal);
        var classifyStart = source.IndexOf("private PlaybackAccessKind ClassifyPlaybackAccess", StringComparison.Ordinal);
        var initializeStart = source.IndexOf("private void InitializePlayback", StringComparison.Ordinal);
        Assert.True(advanceStart >= 0);
        Assert.True(replayStart > advanceStart);
        Assert.True(classifyStart > replayStart);
        Assert.True(initializeStart > classifyStart);

        var advanceMethod = source[advanceStart..replayStart];
        Assert.DoesNotContain("MaxSimulationAdvanceFrames", advanceMethod, StringComparison.Ordinal);

        var replayMethod = source[replayStart..classifyStart];
        Assert.Contains("AdvanceRenderer((float)replayFrames);", replayMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("replayStartFrame", replayMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("nativeRenderer.Update(", replayMethod, StringComparison.Ordinal);

        var classifyMethod = source[classifyStart..initializeStart];
        Assert.Contains("currentItemFrame == previousItemFrame + 1", classifyMethod, StringComparison.Ordinal);
        Assert.Contains("delta >= 0", classifyMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxSimulationAdvanceFrames", classifyMethod, StringComparison.Ordinal);

        var restartStart = source.IndexOf("private void RestartPlaybackAt", StringComparison.Ordinal);
        var resetTrackingStart = source.IndexOf("private void ResetPlaybackTracking", StringComparison.Ordinal);
        Assert.True(restartStart >= 0);
        Assert.True(resetTrackingStart > restartStart);
        var restartMethod = source[restartStart..resetTrackingStart];
        Assert.Contains("ReplayRendererAt(targetFrame);", restartMethod, StringComparison.Ordinal);
    }

    [Fact]
    public void AudioPlaybackUsesBoundedRandomAccessWithoutCoarseJump()
    {
        var root = FindRepositoryRoot();
        var processorPath = Path.Combine(
            root,
            "EffekseerForYMM4",
            "EffekseerAudioEffectProcessor.cs");
        var source = File.ReadAllText(processorPath);

        var replayStart = source.IndexOf("private void ReplayRendererAt(long targetSampleFrame", StringComparison.Ordinal);
        var advanceStart = source.IndexOf("private void AdvanceReplay", StringComparison.Ordinal);
        Assert.True(replayStart >= 0);
        Assert.True(advanceStart > replayStart);

        var replayMethod = source[replayStart..advanceStart];
        Assert.Contains("Math.Min(replayTarget, maxReplaySampleFrames)", replayMethod, StringComparison.Ordinal);
        Assert.Contains("AdvanceReplay(replaySampleFrames, sampleRate);", replayMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("replayStartSampleFrame", replayMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("renderer.Update(", replayMethod, StringComparison.Ordinal);
        Assert.Contains("effectMixBuffer.Length <= int.MaxValue / 2", source, StringComparison.Ordinal);
        Assert.Contains("new float[Math.Max(count, doubledLength)]", source, StringComparison.Ordinal);
    }

    [Fact]
    public void NativeRendererCachesEffectTermAndResetsRendererTime()
    {
        var root = FindRepositoryRoot();
        var sourcePath = Path.Combine(
            root,
            "EffekseerForNative",
            "src",
            "Core",
            "EffectsManager.cpp");
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("totalFrame_ = effect_->CalculateTerm().TermMax;", source, StringComparison.Ordinal);
        Assert.Contains("return totalFrame_;", source, StringComparison.Ordinal);
        Assert.Contains("renderer_->SetTime(0.0f);", source, StringComparison.Ordinal);
    }

    [Fact]
    public void NativeRendererInitializesAndComputesGpuParticles()
    {
        var root = FindRepositoryRoot();
        var nativeSource = File.ReadAllText(Path.Combine(
            root,
            "EffekseerForNative",
            "src",
            "Core",
            "EffectsManager.cpp"));
        var wrapperHeader = File.ReadAllText(Path.Combine(
            root,
            "EffekseerForNative",
            "src",
            "Wrapper",
            "EffekseerRenderer.h"));
        var nativeMethods = File.ReadAllText(Path.Combine(
            root,
            "EffekseerForYMM4",
            "Commons",
            "NativeMethods.cs"));
        var processor = File.ReadAllText(Path.Combine(
            root,
            "EffekseerForYMM4",
            "EffekseerVideoEffectProcessor.cs"));

        Assert.Contains("renderer_->CreateGpuParticleFactory()", nativeSource, StringComparison.Ordinal);
        Assert.Contains("renderer_->CreateGpuParticleSystem()", nativeSource, StringComparison.Ordinal);
        Assert.Contains("manager_->SetGpuParticleFactory(gpuParticleFactory);", nativeSource, StringComparison.Ordinal);
        Assert.Contains("manager_->SetGpuParticleSystem(gpuParticleSystem);", nativeSource, StringComparison.Ordinal);
        Assert.Contains("manager_->SetCurveLoader(", nativeSource, StringComparison.Ordinal);
        Assert.Contains("MakeRefPtr<::Effekseer::CurveLoader>()", nativeSource, StringComparison.Ordinal);
        Assert.Contains("ComputeShaderStateGuard stateGuard(renderer_->GetContext());", nativeSource, StringComparison.Ordinal);
        Assert.Contains("CSGetShader", nativeSource, StringComparison.Ordinal);
        Assert.Contains("CSGetConstantBuffers", nativeSource, StringComparison.Ordinal);
        Assert.Contains("CSGetShaderResources", nativeSource, StringComparison.Ordinal);
        Assert.Contains("CSGetSamplers", nativeSource, StringComparison.Ordinal);
        Assert.Contains("CSGetUnorderedAccessViews", nativeSource, StringComparison.Ordinal);
        Assert.Contains("CSSetShader", nativeSource, StringComparison.Ordinal);
        Assert.Contains("CSSetConstantBuffers", nativeSource, StringComparison.Ordinal);
        Assert.Contains("CSSetShaderResources", nativeSource, StringComparison.Ordinal);
        Assert.Contains("CSSetSamplers", nativeSource, StringComparison.Ordinal);
        Assert.Contains("CSSetUnorderedAccessViews", nativeSource, StringComparison.Ordinal);
        Assert.Contains("DispatchParameter::BufferSlotCount", nativeSource, StringComparison.Ordinal);
        Assert.Contains("DispatchParameter::ResourceSlotCount", nativeSource, StringComparison.Ordinal);
        Assert.Contains("manager_->Compute();", nativeSource, StringComparison.Ordinal);
        Assert.Contains("effekseer_renderer_compute", wrapperHeader, StringComparison.Ordinal);
        Assert.Contains("EntryPoint = \"effekseer_renderer_compute\"", nativeMethods, StringComparison.Ordinal);
        Assert.Contains("private void ComputeGpuParticles()", processor, StringComparison.Ordinal);
        var computeStart = processor.IndexOf("private void ComputeGpuParticles()", StringComparison.Ordinal);
        Assert.True(computeStart >= 0);
        Assert.Contains("lock (RenderLock)", processor[computeStart..], StringComparison.Ordinal);
        Assert.Contains("nativeRenderer.Compute();", processor, StringComparison.Ordinal);
    }

    [Fact]
    public void NativeRendererUsesPremultipliedAlphaForTransparentIntermediateTarget()
    {
        var root = FindRepositoryRoot();
        var project = File.ReadAllText(Path.Combine(
            root,
            "EffekseerForNative",
            "EffekseerForNative.vcxproj"));
        var rendererImpl = File.ReadAllText(Path.Combine(
            root,
            "EffekseerForNative",
            "vendor",
            "effekseer",
            "src",
            "EffekseerRendererCommon",
            "EffekseerRendererCommon",
            "EffekseerRenderer.Renderer_Impl.h"));

        Assert.Contains(
            "EFFEKSEER_FOR_YMM4_PREMULTIPLIED_ALPHA",
            project,
            StringComparison.Ordinal);
        Assert.Contains(
            "#if defined(EFFEKSEER_FOR_YMM4_PREMULTIPLIED_ALPHA)",
            rendererImpl,
            StringComparison.Ordinal);
        Assert.Contains(
            "bool IsPremultipliedAlphaEnabled = true;",
            rendererImpl,
            StringComparison.Ordinal);
    }

    [Fact]
    public void EffectMaterialBasePathKeepsTrailingDirectorySeparator()
    {
        var root = FindRepositoryRoot();
        var sourcePath = Path.Combine(
            root,
            "EffekseerForNative",
            "src",
            "Core",
            "EffectsManager.cpp");
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("directoryPath.push_back(u'\\\\');", source, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "EffekseerForYMM4.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
