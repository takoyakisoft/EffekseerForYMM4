using System.Globalization;
using System.Windows;
using System.Windows.Data;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;

namespace EffekseerForYMM4;

internal sealed class ProjectionAnimationSliderAttribute : PropertyEditorAttribute2
{
    private readonly AnimationSliderAttribute innerAttribute;
    private readonly ProjectionMode enabledMode;

    public ProjectionAnimationSliderAttribute(
        ProjectionMode enabledMode,
        string format,
        string unit,
        double sliderMinimum,
        double sliderMaximum)
    {
        this.enabledMode = enabledMode;
        innerAttribute = new AnimationSliderAttribute(format, unit, sliderMinimum, sliderMaximum);
    }

    public override FrameworkElement Create() => innerAttribute.Create();

    public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
    {
        innerAttribute.SetBindings(control, itemProperties);

        var modeBinding = new MultiBinding
        {
            Mode = BindingMode.OneWay,
            Converter = ProjectionModeEnabledConverter.Instance,
            ConverterParameter = enabledMode,
        };

        foreach (var itemProperty in itemProperties)
        {
            if (itemProperty.PropertyOwner is EffekseerVideoEffect effect)
            {
                modeBinding.Bindings.Add(new Binding(nameof(EffekseerVideoEffect.ProjectionMode))
                {
                    Source = effect,
                    Mode = BindingMode.OneWay,
                });
            }
        }

        if (modeBinding.Bindings.Count == 0)
        {
            control.IsEnabled = false;
            return;
        }

        BindingOperations.SetBinding(control, UIElement.IsEnabledProperty, modeBinding);
    }

    public override void ClearBindings(FrameworkElement control)
    {
        BindingOperations.ClearBinding(control, UIElement.IsEnabledProperty);
        innerAttribute.ClearBindings(control);
    }

    private sealed class ProjectionModeEnabledConverter : IMultiValueConverter
    {
        public static ProjectionModeEnabledConverter Instance { get; } = new();

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            _ = targetType;
            _ = culture;

            return parameter is ProjectionMode mode &&
                values.Length > 0 &&
                values.All(value => value is ProjectionMode valueMode && valueMode == mode);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
