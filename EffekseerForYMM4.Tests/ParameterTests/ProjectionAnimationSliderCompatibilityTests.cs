using System.Reflection;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;

namespace EffekseerForYMM4.Tests.ParameterTests;

public sealed class ProjectionAnimationSliderCompatibilityTests
{
    [Fact]
    public void MultiEdit_FiltersPropertiesUsingDifferentEditorTypes()
    {
        var primary = new PerspectiveOwner();
        var foreign = new StandardSliderOwner();
        var primaryProperty = typeof(PerspectiveOwner).GetProperty(nameof(PerspectiveOwner.Value))!;
        var foreignProperty = typeof(StandardSliderOwner).GetProperty(nameof(StandardSliderOwner.Value))!;
        var attribute = primaryProperty.GetCustomAttribute<ProjectionAnimationSliderAttribute>()!;
        var properties = new[]
        {
            new ItemProperty(primary, primary, primaryProperty, null),
            new ItemProperty(foreign, foreign, foreignProperty, null),
        };

        var compatible = attribute.GetCompatibleItemProperties(properties);

        Assert.Single(compatible);
        Assert.Same(primary, compatible[0].PropertyOwner);
    }

    [Fact]
    public void MultiEdit_FiltersSameEditorTypeWithDifferentBindingConfiguration()
    {
        var primary = new PerspectiveOwner();
        var incompatible = new OrthographicOwner();
        var primaryProperty = typeof(PerspectiveOwner).GetProperty(nameof(PerspectiveOwner.Value))!;
        var incompatibleProperty = typeof(OrthographicOwner).GetProperty(nameof(OrthographicOwner.Value))!;
        var attribute = primaryProperty.GetCustomAttribute<ProjectionAnimationSliderAttribute>()!;
        var properties = new[]
        {
            new ItemProperty(primary, primary, primaryProperty, null),
            new ItemProperty(incompatible, incompatible, incompatibleProperty, null),
        };

        var compatible = attribute.GetCompatibleItemProperties(properties);

        Assert.Single(compatible);
        Assert.Same(primary, compatible[0].PropertyOwner);
    }

    private sealed class PerspectiveOwner
    {
        [ProjectionAnimationSlider(ProjectionMode.Perspective, "F1", "px", -500.0, 500.0)]
        public Animation Value { get; } = new(0.0, -100000.0, 100000.0);
    }

    private sealed class OrthographicOwner
    {
        [ProjectionAnimationSlider(ProjectionMode.Orthographic, "F1", "", 0.1, 10.0)]
        public Animation Value { get; } = new(1.0, 0.0, 100000.0);
    }

    private sealed class StandardSliderOwner
    {
        [AnimationSlider("F1", "px", -500.0, 500.0)]
        public Animation Value { get; } = new(0.0, -100000.0, 100000.0);
    }
}