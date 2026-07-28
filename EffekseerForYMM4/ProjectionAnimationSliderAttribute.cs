using System.Globalization;
using System.Windows;
using System.Windows.Data;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;

namespace EffekseerForYMM4;

internal sealed class ProjectionAnimationSliderAttribute(
    ProjectionMode enabledMode,
    string format,
    string unit,
    double sliderMinimum,
    double sliderMaximum) : PropertyEditorAttribute2
{
    private readonly AnimationSliderAttribute innerAttribute = new(format, unit, sliderMinimum, sliderMaximum);

    public ProjectionMode EnabledMode { get; } = enabledMode;
    public string Format { get; } = format;
    public string Unit { get; } = unit;
    public double SliderMinimum { get; } = sliderMinimum;
    public double SliderMaximum { get; } = sliderMaximum;

    public override FrameworkElement Create() => innerAttribute.Create();

    public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
    {
        innerAttribute.SetBindings(control, itemProperties);

        var modeBinding = new MultiBinding
        {
            Mode = BindingMode.OneWay,
            Converter = ProjectionModeEnabledConverter.Instance,
            ConverterParameter = EnabledMode,
        };

        foreach (var itemProperty in itemProperties)
        {
            if (itemProperty.PropertyOwner is EffekseerVideoEffect effect)
            {
                // Bind to the public view model directly because the owning effect type is internal.
                modeBinding.Bindings.Add(new Binding(nameof(ProjectionModeViewModel.SelectedProjectionMode))
                {
                    Source = effect.Projection,
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
