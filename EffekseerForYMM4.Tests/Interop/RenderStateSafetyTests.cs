using System.IO;

namespace EffekseerForYMM4.Tests;

public sealed class RenderStateSafetyTests
{
    [Fact]
    public void VideoProcessorSerializesSharedImmediateContextRendering()
    {
        var root = FindRepositoryRoot();
        var processorPath = Path.Combine(
            root,
            "EffekseerForYMM4",
            "EffekseerVideoEffect",
            "EffekseerVideoEffectProcessor.cs");
        var source = File.ReadAllText(processorPath);

        Assert.Contains("private static readonly object RenderLock = new();", source, StringComparison.Ordinal);
        Assert.Contains("lock (RenderLock)", source, StringComparison.Ordinal);
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
