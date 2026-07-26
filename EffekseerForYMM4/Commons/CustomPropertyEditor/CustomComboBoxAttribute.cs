using System.Windows;
using System.Windows.Data;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Views.Converters;

namespace EffekseerForYMM4.Commons.CustomPropertyEditor;

public sealed class CustomComboBoxAttribute : PropertyEditorAttribute2
{
    public override FrameworkElement Create() => new CustomComboBox();

    public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(itemProperties);

        ((CustomComboBox)control).SetBinding(
            CustomComboBox.CustomViewModelProperty,
            ItemPropertiesBinding.Create2(itemProperties));
    }

    public override void ClearBindings(FrameworkElement control)
    {
        ArgumentNullException.ThrowIfNull(control);

        BindingOperations.ClearBinding(control, CustomComboBox.CustomViewModelProperty);
    }
}
