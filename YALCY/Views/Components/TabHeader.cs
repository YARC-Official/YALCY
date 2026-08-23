using Avalonia.Controls;
using Avalonia.Media;

namespace YALCY.Views.Components;

public class TabHeader : TextBlock
{
    public TabHeader()
    {
        FontSize = 13;
        FontWeight = Avalonia.Media.FontWeight.SemiBold;
        Margin = new Avalonia.Thickness(0);
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        TextWrapping = Avalonia.Media.TextWrapping.NoWrap;
    }
}
