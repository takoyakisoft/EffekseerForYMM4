using System.Collections.ObjectModel;
using System.ComponentModel;

namespace EffekseerForYMM4.Commons.CustomPropertyEditor;

public abstract class CustomComboBoxViewModelBase : INotifyPropertyChanged
{
    private CustomComboBoxValueBase selectedValue = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<CustomComboBoxValueBase> ItemsSource { get; } = [];
    public virtual bool IsEnabled => true;

    public virtual CustomComboBoxValueBase SelectedValue
    {
        get => selectedValue;
        set
        {
            if (ReferenceEquals(selectedValue, value))
            {
                return;
            }
            selectedValue = value;
            OnPropertyChanged(nameof(SelectedValue));
        }
    }

    public abstract void UpdateItemsSource();
    public abstract void UpdateSelectedValue();

    protected void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
