using System.ComponentModel;
using System.Reflection;

namespace EffekseerForYMM4.Tests.ParameterTests;

public sealed class ProjectionModeNotificationTests
{
    [Fact]
    public void ComboBoxSelectionNotifiesOwningEffectParameter()
    {
        var effectType = typeof(EffekseerForNative.EffekseerRenderer).Assembly
            .GetType("EffekseerForYMM4.EffekseerVideoEffect", throwOnError: true)!;
        var effect = Activator.CreateInstance(
            effectType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: null,
            culture: null)!;
        var projection = (ProjectionModeViewModel)effectType
            .GetProperty("Projection")!
            .GetValue(effect)!;
        var changedProperties = new List<string?>();
        ((INotifyPropertyChanged)effect).PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        projection.UpdateItemsSource();
        projection.SelectedValue = projection.ItemsSource
            .OfType<ProjectionModeItem>()
            .Single(item => item.Mode == ProjectionMode.Orthographic);

        Assert.Equal(
            ProjectionMode.Orthographic,
            effectType.GetProperty("ProjectionMode")!.GetValue(effect));
        Assert.Contains("ProjectionMode", changedProperties);
    }

    [Fact]
    public void SerializedProjectionModeIsPreservedBeforeComboBoxLoads()
    {
        var effectType = typeof(EffekseerForNative.EffekseerRenderer).Assembly
            .GetType("EffekseerForYMM4.EffekseerVideoEffect", throwOnError: true)!;
        var effect = Activator.CreateInstance(
            effectType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: null,
            culture: null)!;
        var projectionModeProperty = effectType.GetProperty("ProjectionMode")!;
        var projection = (ProjectionModeViewModel)effectType.GetProperty("Projection")!.GetValue(effect)!;

        projectionModeProperty.SetValue(effect, ProjectionMode.Orthographic);

        Assert.Equal(ProjectionMode.Orthographic, projectionModeProperty.GetValue(effect));
        Assert.Equal(ProjectionMode.Orthographic, projection.SelectedProjectionMode);
    }
}
