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

        var projections = itemProperties
            .Select(itemProperty => itemProperty.PropertyOwner)
            .OfType<EffekseerVideoEffect>()
            .Select(effect => effect.Projection)
            .Distinct()
            .ToArray();

        if (projections.Length == 0)
        {
            // Do not leave the editor permanently disabled if YMM4 changes
            // how ItemProperty.PropertyOwner is exposed.
            control.IsEnabled = true;
            return;
        }

        subscriptions.Add(
            control,
            new ProjectionModeEnablementSubscription(control, projections, EnabledMode));
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
        private readonly ProjectionModeViewModel[] projections;
        private readonly ProjectionMode enabledMode;
        private bool disposed;

        public ProjectionModeEnablementSubscription(
            FrameworkElement control,
            ProjectionModeViewModel[] projections,
            ProjectionMode enabledMode)
        {
            this.control = control;
            this.projections = projections;
            this.enabledMode = enabledMode;

            foreach (var projection in projections)
            {
                projection.PropertyChanged += Projection_PropertyChanged;
            }

            UpdateIsEnabled();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            foreach (var projection in projections)
            {
                projection.PropertyChanged -= Projection_PropertyChanged;
            }
        }

        private void Projection_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            _ = sender;

            if (string.IsNullOrEmpty(e.PropertyName) ||
                e.PropertyName == nameof(ProjectionModeViewModel.SelectedProjectionMode))
            {
                UpdateIsEnabled();
            }
        }

        private void UpdateIsEnabled()
        {
            control.IsEnabled = projections.All(
                projection => projection.SelectedProjectionMode == enabledMode);
        }
    }
}
