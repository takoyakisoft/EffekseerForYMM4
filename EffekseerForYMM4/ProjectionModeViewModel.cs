using EffekseerForYMM4.Commons.CustomPropertyEditor;

namespace EffekseerForYMM4;

public enum ProjectionMode
{
    Perspective,
    Orthographic,
}

public sealed class ProjectionModeItem : CustomComboBoxValueBase
{
    public required ProjectionMode Mode { get; init; }
    public required string Label { get; init; }
    public override string DisplayMember => Label;
    public override string ToolTipMember => Translate.Video_Projection_Desc;
}

public sealed class ProjectionModeViewModel(ProjectionMode initialMode) : CustomComboBoxViewModelBase
{
    public ProjectionMode SelectedProjectionMode { get; private set; } = initialMode;

    public override CustomComboBoxValueBase SelectedValue
    {
        get => base.SelectedValue;
        set
        {
            base.SelectedValue = value;
            if (value is ProjectionModeItem item && SelectedProjectionMode != item.Mode)
            {
                SelectedProjectionMode = item.Mode;
                OnPropertyChanged(nameof(SelectedProjectionMode));
            }
        }
    }

    public void Select(ProjectionMode mode)
    {
        SelectedProjectionMode = mode;
        if (ItemsSource.Count > 0)
        {
            UpdateSelectedValue();
        }
        OnPropertyChanged(nameof(SelectedProjectionMode));
    }

    public override void UpdateItemsSource()
    {
        ItemsSource.Clear();
        ItemsSource.Add(new ProjectionModeItem
        {
            Mode = ProjectionMode.Perspective,
            Label = GetLabel(ProjectionMode.Perspective),
        });
        ItemsSource.Add(new ProjectionModeItem
        {
            Mode = ProjectionMode.Orthographic,
            Label = GetLabel(ProjectionMode.Orthographic),
        });
    }

    public override void UpdateSelectedValue()
    {
        SelectedValue = ItemsSource
            .OfType<ProjectionModeItem>()
            .FirstOrDefault(item => item.Mode == SelectedProjectionMode)
            ?? ItemsSource.FirstOrDefault()
            ?? new ProjectionModeItem
            {
                Mode = SelectedProjectionMode,
                Label = GetLabel(SelectedProjectionMode),
            };
    }

    private static string GetLabel(ProjectionMode mode) =>
        mode == ProjectionMode.Orthographic
            ? Translate.Video_Projection_Orthographic
            : Translate.Video_Projection_Perspective;
}
