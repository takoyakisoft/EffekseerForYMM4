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
    public void PlaybackUsesBoundedThreeStateAccessModel()
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
        Assert.Contains("delta <= MaxSimulationAdvanceFrames", source, StringComparison.Ordinal);
        Assert.Contains("private void ReplayRendererAt(double targetFrame)", source, StringComparison.Ordinal);
        Assert.Contains("var replayFrames = Math.Min(targetFrame, MaxSimulationAdvanceFrames);", source, StringComparison.Ordinal);
        Assert.Contains("var replayStartFrame = targetFrame - replayFrames;", source, StringComparison.Ordinal);
        Assert.Contains("nativeRenderer.Update((float)replayStartFrame);", source, StringComparison.Ordinal);
        Assert.Contains("hasAppliedCamera", source, StringComparison.Ordinal);
        Assert.Contains("hasAppliedProjection", source, StringComparison.Ordinal);
        Assert.Contains("hasAppliedTransform", source, StringComparison.Ordinal);

        var replayStart = source.IndexOf("private void ReplayRendererAt", StringComparison.Ordinal);
        var classifyStart = source.IndexOf("private PlaybackAccessKind ClassifyPlaybackAccess", StringComparison.Ordinal);
        Assert.True(replayStart >= 0);
        Assert.True(classifyStart > replayStart);
        var replayMethod = source[replayStart..classifyStart];
        Assert.Contains("AdvanceRenderer((float)replayFrames);", replayMethod, StringComparison.Ordinal);

        var restartStart = source.IndexOf("private void RestartPlaybackAt", StringComparison.Ordinal);
        var resetTrackingStart = source.IndexOf("private void ResetPlaybackTracking", StringComparison.Ordinal);
        Assert.True(restartStart >= 0);
        Assert.True(resetTrackingStart > restartStart);
        var restartMethod = source[restartStart..resetTrackingStart];
        Assert.Contains("ReplayRendererAt(targetFrame);", restartMethod, StringComparison.Ordinal);
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
