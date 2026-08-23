using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace YALCY.ViewModels;

public class BoolToHighlightBackgroundConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true)
        {
            var accent = AccentHelper.GetAccentColor();
            // Refined 12% alpha tint of Windows Accent Color (Fluent Dark Standard)
            return new SolidColorBrush(Color.FromArgb(32, accent.R, accent.G, accent.B));
        }
        return Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToHighlightForegroundConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true)
        {
            if (Application.Current?.TryGetResource("AppTextPrimary", null, out var resPrimary) == true && resPrimary is IBrush brushPrimary)
            {
                return brushPrimary;
            }
            return Brushes.Black;
        }

        if (Application.Current?.TryGetResource("AppTextSecondary", null, out var resSecondary) == true && resSecondary is IBrush brushSecondary)
        {
            return brushSecondary;
        }
        return Brushes.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToHighlightStarBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true)
        {
            return new SolidColorBrush(AccentHelper.GetAccentColor());
        }
        if (Application.Current?.TryGetResource("AppTextMuted", null, out var resource) == true && resource is IBrush brush)
        {
            return brush;
        }

        return Brushes.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToHighlightBorderBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true)
        {
            return new SolidColorBrush(AccentHelper.GetAccentColor());
        }
        return Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
