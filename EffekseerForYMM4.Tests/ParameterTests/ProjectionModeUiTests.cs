using System.IO;

namespace EffekseerForYMM4.Tests.ParameterTests;

public sealed class ProjectionModeUiTests
{
    [Fact]
    public void ProjectionSpecificControlsMatchRendererSemantics()
    {
        var root = FindRepositoryRoot();
        var parameterPath = Path.Combine(
            root,
            "EffekseerForYMM4",
            "EffekseerVideoEffect.cs");
        var processorPath = Path.Combine(
            root,
            "EffekseerForYMM4",
            "EffekseerVideoEffectProcessor.cs");
        var projectionSliderPath = Path.Combine(
            root,
            "EffekseerForYMM4",
            "ProjectionAnimationSliderAttribute.cs");
        var parameterSource = File.ReadAllText(parameterPath);
        var processorSource = File.ReadAllText(processorPath);
        var projectionSliderSource = File.ReadAllText(projectionSliderPath);

        Assert.Contains(
            "[ProjectionAnimationSlider(ProjectionMode.Perspective, \"F1\", \"px\", EffekseerParameterSettings.PositionSliderMinimum, EffekseerParameterSettings.PositionSliderMaximum)]",
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
        Assert.Contains("\"px\"", parameterSource, StringComparison.Ordinal);
        Assert.Contains("private const float DefaultCameraZ = 20.0f;", processorSource, StringComparison.Ordinal);
        Assert.Contains(
            "float camZ = item.ProjectionMode == ProjectionMode.Perspective",
            processorSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "float fov = Math.Clamp(",
            processorSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "new Binding(nameof(ProjectionModeViewModel.SelectedProjectionMode))",
            projectionSliderSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "Source = effect.Projection",
            projectionSliderSource,
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
