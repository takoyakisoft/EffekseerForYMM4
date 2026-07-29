using System.IO;

namespace EffekseerForYMM4.Tests.ParameterTests;

public sealed class ParameterConventionTests
{
    [Fact]
    public void AnimationRangesUseOnlyPositiveAndSignedConventions()
    {
        Assert.Equal(0.0, EffekseerParameterSettings.PositiveAnimationMinimum);
        Assert.Equal(-100_000.0, EffekseerParameterSettings.SignedAnimationMinimum);
        Assert.Equal(100_000.0, EffekseerParameterSettings.AnimationMaximum);

        var source = ReadEffectSources();
        Assert.DoesNotContain("new(100, 0, 1000)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new Animation(90, 1, 179)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new Animation(10, 0.001, 100000)", source, StringComparison.Ordinal);
        Assert.Contains("EffekseerParameterSettings.PositiveAnimationMinimum", source, StringComparison.Ordinal);
        Assert.Contains("EffekseerParameterSettings.SignedAnimationMinimum", source, StringComparison.Ordinal);
        Assert.Equal(
            CountOccurrences(source, "public Animation "),
            CountOccurrences(source, "EffekseerParameterSettings.AnimationMaximum"));
    }

    [Fact]
    public void UiOrderFollowsPropertyDeclarationOrder()
    {
        var root = FindRepositoryRoot();
        var source = string.Join(
            Environment.NewLine,
                Directory.GetFiles(
                    Path.Combine(root, "EffekseerForYMM4"),
                    "*.cs",
                    SearchOption.AllDirectories)
                .Where(path =>
                    !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                    !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .Select(File.ReadAllText));
        Assert.DoesNotContain("Order =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Order=", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PositionSlidersUseYmm4PixelUnit()
    {
        var source = ReadEffectSources();
        Assert.DoesNotContain(
            "[AnimationSlider(\"F1\", \"\", -500, 500)]",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "[AnimationSlider(\"F1\", \"px\", EffekseerParameterSettings.PositionSliderMinimum, EffekseerParameterSettings.PositionSliderMaximum)]",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void LabelsAndDescriptionsFollowYmm4Terminology()
    {
        var root = FindRepositoryRoot();
        var localization = File.ReadAllText(
            Path.Combine(root, "EffekseerForYMM4", "Localization", "Translate.csv"));

        Assert.Contains("Group_Transform,group,描画,Drawing", localization, StringComparison.Ordinal);
        Assert.Contains("Video_ScreenSize_Name,name,画面サイズ,Screen size", localization, StringComparison.Ordinal);
        Assert.Contains("Audio_Volume_Desc,desc,音量,Volume", localization, StringComparison.Ordinal);
        Assert.Contains("Camera_X_Desc,desc,カメラの位置（横方向）,Camera position (horizontal)", localization, StringComparison.Ordinal);
        Assert.Contains("Transform_PositionX_Desc,desc,描画位置（横方向）,Drawing position (horizontal)", localization, StringComparison.Ordinal);
        Assert.Contains("Transform_RotationX_Name,name,X軸,X axis", localization, StringComparison.Ordinal);
        Assert.Contains("Transform_Scale_Name,name,拡大率,Zoom", localization, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string source, string value) =>
        (source.Length - source.Replace(value, "", StringComparison.Ordinal).Length) / value.Length;

    private static string ReadEffectSources()
    {
        var root = FindRepositoryRoot();
        return string.Join(
            Environment.NewLine,
            File.ReadAllText(Path.Combine(root, "EffekseerForYMM4", "EffekseerVideoEffect.cs")),
            File.ReadAllText(Path.Combine(root, "EffekseerForYMM4", "EffekseerAudioEffect.cs")));
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
