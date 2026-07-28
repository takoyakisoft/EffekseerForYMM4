using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
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
    private readonly ConditionalWeakTable<FrameworkElement, ProjectionModeEnablementSubscription> subscriptions = new();

    public ProjectionMode EnabledMode { get; } = enabledMode;
    public string Format { get; } = format;
    public string Unit { get; } = unit;
    public double SliderMinimum { get; } = sliderMinimum;
    public double SliderMaximum { get; } = sliderMaximum;

    public override FrameworkElement Create() => innerAttribute.Create();

    public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
    {
        innerAttribute.SetBindings(control, itemProperties);
        RemoveSubscription(control);

        var effect = itemProperties
            .Select(itemProperty => itemProperty.PropertyOwner)
            .OfType<EffekseerVideoEffect>()
            .FirstOrDefault();

        if (effect is null)
        {
            // Do not leave the editor permanently disabled if YMM4 changes
            // how ItemProperty.PropertyOwner is exposed.
            control.IsEnabled = true;
            return;
        }

        subscriptions.Add(
            control,
            new ProjectionModeEnablementSubscription(control, effect, EnabledMode));
    }

    public override void ClearBindings(FrameworkElement control)
    {
        RemoveSubscription(control);
        innerAttribute.ClearBindings(control);
    }

    private void RemoveSubscription(FrameworkElement control)
    {
        if (!subscriptions.TryGetValue(control, out var subscription))
        {
            return;
        }

        subscription.Dispose();
        subscriptions.Remove(control);
    }

    private sealed class ProjectionModeEnablementSubscription : IDisposable
    {
        private readonly FrameworkElement control;
        private readonly EffekseerVideoEffect effect;
        private readonly ProjectionMode enabledMode;
        private bool disposed;

        public ProjectionModeEnablementSubscription(
            FrameworkElement control,
            EffekseerVideoEffect effect,
            ProjectionMode enabledMode)
        {
            this.control = control;
            this.effect = effect;
            this.enabledMode = enabledMode;

            effect.PropertyChanged += Effect_PropertyChanged;

            UpdateIsEnabled();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            effect.PropertyChanged -= Effect_PropertyChanged;
        }

        private void Effect_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            _ = sender;

            if (string.IsNullOrEmpty(e.PropertyName) ||
                e.PropertyName == nameof(EffekseerVideoEffect.ProjectionMode))
            {
                UpdateIsEnabled();
            }
        }

        private void UpdateIsEnabled()
        {
            control.IsEnabled = effect.ProjectionMode == enabledMode;
        }
    }
}
