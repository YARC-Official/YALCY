using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace YALCY.ViewModels;

public class IntToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var category = (int)value;
        var targetCategory = int.Parse(parameter.ToString());
        return category == targetCategory;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isChecked = (bool)value;
        var targetCategory = int.Parse(parameter.ToString());
        return isChecked ? targetCategory : AvaloniaProperty.UnsetValue;
    }
}
