using System.IO;

namespace EffekseerForYMM4.Tests;

public sealed class ProjectionModeUiTests
{
    [Fact]
    public void ProjectionSpecificControlsMatchRendererSemantics()
    {
        var root = FindRepositoryRoot();
        var parameterPath = Path.Combine(
            root,
            "EffekseerForYMM4",
            "EffekseerVideoEffect",
            "EffekseerVideoEffect.cs");
        var processorPath = Path.Combine(
            root,
            "EffekseerForYMM4",
            "EffekseerVideoEffect",
            "EffekseerVideoEffectProcessor.cs");
        var parameterSource = File.ReadAllText(parameterPath);
        var processorSource = File.ReadAllText(processorPath);

        Assert.Contains(
            "[ProjectionAnimationSlider(ProjectionMode.Perspective, \"F1\", \"\", -500, 500)]",
            parameterSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "[ProjectionAnimationSlider(ProjectionMode.Perspective, \"F0\", \"°\", 1, 179)]",
            parameterSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "[ProjectionAnimationSlider(ProjectionMode.Orthographic, \"F1\", \"\", 0.1, 10)]",
            parameterSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain("\"px\"", parameterSource, StringComparison.Ordinal);
        Assert.Contains("private const float DefaultCameraZ = 20.0f;", processorSource, StringComparison.Ordinal);
        Assert.Contains(
            "float camZ = item.ProjectionMode == ProjectionMode.Perspective",
            processorSource,
            StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "EffekseerForYMM4")) &&
                Directory.Exists(Path.Combine(directory.FullName, "EffekseerForNative")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
