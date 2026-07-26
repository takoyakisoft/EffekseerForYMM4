using System.Windows;
using System.Windows.Controls;
using YukkuriMovieMaker.Commons;

namespace EffekseerForYMM4.Commons.CustomPropertyEditor;

public partial class CustomComboBox : UserControl, IPropertyEditorControl
{
    private bool suppressSelectionChanged;

    public CustomComboBoxViewModelBase CustomViewModel
    {
        get => (CustomComboBoxViewModelBase)GetValue(CustomViewModelProperty);
        set => SetValue(CustomViewModelProperty, value);
    }

    public static readonly DependencyProperty CustomViewModelProperty =
        DependencyProperty.Register(
            nameof(CustomViewModel),
            typeof(CustomComboBoxViewModelBase),
            typeof(CustomComboBox),
            new FrameworkPropertyMetadata(null, OnViewModelChanged));

    public event EventHandler? BeginEdit;
    public event EventHandler? EndEdit;

    public CustomComboBox()
    {
        InitializeComponent();
    }

    private static void OnViewModelChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        _ = e;
        ((CustomComboBox)dependencyObject).UpdateView();
    }

    private void UpdateView()
    {
        if (CustomViewModel == null)
        {
            return;
        }

        suppressSelectionChanged = true;
        try
        {
            CustomViewModel.UpdateItemsSource();
            CustomViewModel.UpdateSelectedValue();
        }
        finally
        {
            suppressSelectionChanged = false;
        }
    }

    private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _ = e;
        if (!IsLoaded || suppressSelectionChanged || CustomViewModel == null || sender is not ComboBox comboBox)
        {
            return;
        }

        if (comboBox.SelectedItem is CustomComboBoxValueBase selected &&
            !ReferenceEquals(CustomViewModel.SelectedValue, selected))
        {
            BeginEdit?.Invoke(this, EventArgs.Empty);
            CustomViewModel.SelectedValue = selected;
            EndEdit?.Invoke(this, EventArgs.Empty);
        }
    }
}
