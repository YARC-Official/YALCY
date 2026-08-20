using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace YALCY.ViewModels;

public class IntToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isSelected &&
            targetType == typeof(FontWeight) &&
            string.Equals(parameter?.ToString(), "Bold", StringComparison.OrdinalIgnoreCase))
        {
            return isSelected ? FontWeight.Bold : FontWeight.Normal;
        }

        if (value is not int category || !int.TryParse(parameter?.ToString(), out var targetCategory))
        {
            return false;
        }

        return category == targetCategory;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool isChecked ||
            !int.TryParse(parameter?.ToString(), out var targetCategory))
        {
            return AvaloniaProperty.UnsetValue;
        }

        return isChecked ? targetCategory : AvaloniaProperty.UnsetValue;
    }
}
