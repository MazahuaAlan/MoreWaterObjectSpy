using System.Globalization;
using System.Windows.Data;
using Wpf.Ui.Controls;

namespace MoreWaterObjectSpy.Infrastructure;

/// <summary>Mapea un ControlType (UIA) a un icono Fluent para las listas y el arbol.</summary>
public class ControlTypeIcon : IValueConverter
{
    public static SymbolRegular Symbol(string? controlType) => (controlType ?? "") switch
    {
        "Button" or "SplitButton" => SymbolRegular.Cursor24,
        "Edit" or "Document" => SymbolRegular.TextField24,
        "CheckBox" => SymbolRegular.CheckboxChecked24,
        "RadioButton" => SymbolRegular.RadioButton24,
        "ComboBox" => SymbolRegular.ChevronDown24,
        "List" or "ListItem" => SymbolRegular.AppsList24,
        "DataGrid" or "Table" or "DataItem" or "Grid" => SymbolRegular.Table24,
        "Tree" or "TreeItem" => SymbolRegular.TextBulletListTree24,
        "TabItem" or "Tab" => SymbolRegular.TabDesktop24,
        "Image" => SymbolRegular.Image24,
        "Text" => SymbolRegular.TextT24,
        "MenuItem" or "Menu" or "MenuBar" => SymbolRegular.Navigation24,
        "Window" or "Pane" => SymbolRegular.Window24,
        "Hyperlink" => SymbolRegular.Link24,
        "Slider" => SymbolRegular.Options24,
        "ProgressBar" => SymbolRegular.DataBarHorizontal24,
        _ => SymbolRegular.SquareHint24,
    };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Symbol(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
