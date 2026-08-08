using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using YukkuriMovieMaker.Controls;

namespace EffekseerForYMM4.Tests.ParameterTests;

public sealed class ProjectionModeNotificationTests
{
    [Fact]
    public void ProjectionMode_UsesYmm4EnumComboBoxDirectly()
    {
        var effectType = typeof(EffekseerForNative.EffekseerRenderer).Assembly
            .GetType("EffekseerForYMM4.EffekseerVideoEffect", throwOnError: true)!;
        var property = effectType.GetProperty("ProjectionMode")!;

        Assert.NotNull(property.GetCustomAttribute<EnumComboBoxAttribute>());
        var display = property.GetCustomAttribute<DisplayAttribute>();
        Assert.NotNull(display);
        Assert.Equal(nameof(Translate.Video_Projection_Name), display!.Name);
        Assert.Equal(nameof(Translate.Video_Projection_Desc), display.Description);
        Assert.Equal(typeof(Translate), display.ResourceType);
    }

    [Fact]
    public void ProjectionModeChange_NotifiesOwningEffectParameter()
    {
        var effectType = typeof(EffekseerForNative.EffekseerRenderer).Assembly
            .GetType("EffekseerForYMM4.EffekseerVideoEffect", throwOnError: true)!;
        var effect = Activator.CreateInstance(
            effectType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: null,
            culture: null)!;
        var changedProperties = new List<string?>();
        ((INotifyPropertyChanged)effect).PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);
        var projectionModeProperty = effectType.GetProperty("ProjectionMode")!;

        projectionModeProperty.SetValue(effect, ProjectionMode.Orthographic);

        Assert.Equal(ProjectionMode.Orthographic, projectionModeProperty.GetValue(effect));
        Assert.Contains("ProjectionMode", changedProperties);
    }

    [Theory]
    [InlineData(ProjectionMode.Perspective, nameof(Translate.Video_Projection_Perspective))]
    [InlineData(ProjectionMode.Orthographic, nameof(Translate.Video_Projection_Orthographic))]
    public void ProjectionMode_UsesLocalizedYmm4EnumLabels(ProjectionMode mode, string expectedResourceName)
    {
        var member = typeof(ProjectionMode).GetMember(mode.ToString()).Single();
        var display = member.GetCustomAttribute<DisplayAttribute>();

        Assert.NotNull(display);
        Assert.Equal(expectedResourceName, display!.Name);
        Assert.Equal(typeof(Translate), display.ResourceType);
    }
}
