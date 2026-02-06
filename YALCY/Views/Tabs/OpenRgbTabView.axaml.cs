using Avalonia.Controls;
using Avalonia.Data.Converters;
using System;
using System.Globalization;
using Avalonia.Media;
using Avalonia.Layout;
using Avalonia;

namespace YALCY.Views.Tabs;

public partial class OpenRgbTabView : UserControl
{
    public OpenRgbTabView()
    {
        InitializeComponent();
        this.SizeChanged += OpenRgbTabView_SizeChanged;
    }

    private void OpenRgbTabView_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateLayoutState(e.NewSize.Width);
    }

    private void UpdateLayoutState(double width)
    {
        bool isCompact = width < 1000;

        if (isCompact)
        {
            // --- Compact Mode (Stacked Vertically) ---

            // Grid Layout: Row 0 = Connection (Auto), Row 1 = Devices (*)
            ContentGrid.RowDefinitions[0].Height = GridLength.Auto;
            ContentGrid.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star);

            ContentGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            ContentGrid.ColumnDefinitions[1].Width = GridLength.Auto;

            Grid.SetRow(ConnectionCard, 0);
            Grid.SetColumn(ConnectionCard, 0);
            Grid.SetColumnSpan(ConnectionCard, 2);

            Grid.SetRow(DevicesCard, 1);
            Grid.SetColumn(DevicesCard, 0);
            Grid.SetColumnSpan(DevicesCard, 2);

            // Connection Card Margin
            ConnectionCard.Margin = new Thickness(0, 0, 0, 16);

            // Connection Card Internals (Horizontal Layout - Single Row Grid)
            // Grid: Header | Form | Status
            ConnectionPanelStack.RowDefinitions.Clear();
            ConnectionPanelStack.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            
            ConnectionPanelStack.ColumnDefinitions.Clear();
            ConnectionPanelStack.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto)); // Header
            ConnectionPanelStack.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star))); // Spacer/Form
            ConnectionPanelStack.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto)); // Status

            // Reposition Children
            Grid.SetRow(HeaderPanel, 0);
            Grid.SetColumn(HeaderPanel, 0);
            
            Grid.SetRow(RequirementsPanel, 0); // Hidden anyway
            
            Grid.SetRow(ConnectionForm, 0);
            Grid.SetColumn(ConnectionForm, 1);
            
            Grid.SetRow(StatusPanel, 0);
            Grid.SetColumn(StatusPanel, 2);

            // Alignment & Spacing
            ConnectionPanelStack.HorizontalAlignment = HorizontalAlignment.Stretch;

            // Header
            HeaderPanel.Margin = new Thickness(0, 0, 24, 0);
            HeaderPanel.VerticalAlignment = VerticalAlignment.Center;
           
            // Hide Requirements
            RequirementsPanel.IsVisible = false;

            // Form (Horizontal)
            ConnectionForm.Orientation = Orientation.Horizontal;
            ConnectionForm.Spacing = 12;
            ConnectionForm.Margin = new Thickness(0);
            ConnectionForm.VerticalAlignment = VerticalAlignment.Center;
            ConnectionForm.HorizontalAlignment = HorizontalAlignment.Left; // Keep close to header

            // Reduce widths for better fit
            ServerIpPanel.Width = 160;
            ServerIpLabel.IsVisible = true;
            
            ServerPortPanel.Width = 100;
            ServerPortInput.ShowButtonSpinner = false;

            ConnectButton.Width = 120;
            ConnectButton.Margin = new Thickness(0, 0, 0, 0);
            ConnectButton.VerticalAlignment = VerticalAlignment.Bottom; // Align with textboxes
            ConnectButton.Height = 36; // Match TextBox height

            // Status
            StatusPanel.Margin = new Thickness(0, 0, 0, 0); 
            StatusPanel.VerticalAlignment = VerticalAlignment.Center;
            StatusPanel.Padding = new Thickness(12, 6);
            StatusText.IsVisible = false; // Hide status text in compact mode
        }
        else
        {
            // --- Wide Mode (Sidebar + Main) ---

            // Grid Layout: Col 0 = Connection (400), Col 1 = Devices (*)
            
            // Note: In XAML we defined Row 0 = *, Row 1 = Auto.
            // For Wide mode, we want both in Row 0, spanning full height (or Auto if content dictates, but usually *)
            
            ContentGrid.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
            ContentGrid.RowDefinitions[1].Height = GridLength.Auto; // Unused row

            ContentGrid.ColumnDefinitions[0].Width = new GridLength(400);
            ContentGrid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);

            Grid.SetRow(ConnectionCard, 0);
            Grid.SetColumn(ConnectionCard, 0);
            Grid.SetColumnSpan(ConnectionCard, 1);

            Grid.SetRow(DevicesCard, 0);
            Grid.SetColumn(DevicesCard, 1);
            Grid.SetColumnSpan(DevicesCard, 1);

            // Connection Card Margin
            ConnectionCard.Margin = new Thickness(0, 0, 16, 0);

            // Connection Card Internals (Vertical Layout - Stacked Grid)
            ConnectionPanelStack.ColumnDefinitions.Clear(); // Single column (implicit or explicit)
            
            ConnectionPanelStack.RowDefinitions.Clear();
            ConnectionPanelStack.RowDefinitions.Add(new RowDefinition(GridLength.Auto)); // Header
            ConnectionPanelStack.RowDefinitions.Add(new RowDefinition(GridLength.Auto)); // Req
            ConnectionPanelStack.RowDefinitions.Add(new RowDefinition(GridLength.Auto)); // Form
            ConnectionPanelStack.RowDefinitions.Add(new RowDefinition(GridLength.Auto)); // Status

            // Reposition Children
            Grid.SetRow(HeaderPanel, 0);
            Grid.SetColumn(HeaderPanel, 0);
            
            Grid.SetRow(RequirementsPanel, 1);
            Grid.SetColumn(RequirementsPanel, 0);
            
            Grid.SetRow(ConnectionForm, 2);
            Grid.SetColumn(ConnectionForm, 0);
            
            Grid.SetRow(StatusPanel, 3);
            Grid.SetColumn(StatusPanel, 0);

            ConnectionPanelStack.HorizontalAlignment = HorizontalAlignment.Stretch;

            // Header
            HeaderPanel.Margin = new Thickness(0, 0, 0, 16);
            HeaderPanel.VerticalAlignment = VerticalAlignment.Stretch; // Default

            // Show Requirements
            RequirementsPanel.IsVisible = true;

            // Form (Vertical)
            ConnectionForm.Orientation = Orientation.Vertical;
            ConnectionForm.Spacing = 14;
            ConnectionForm.Margin = new Thickness(0); 
            ConnectionForm.VerticalAlignment = VerticalAlignment.Stretch;
            ConnectionForm.HorizontalAlignment = HorizontalAlignment.Stretch; // Default

            ServerIpPanel.Width = double.NaN; // Auto
            ServerIpLabel.IsVisible = true;

            ServerPortPanel.Width = double.NaN;
            ServerPortInput.ShowButtonSpinner = true;

            ConnectButton.Width = double.NaN;
            ConnectButton.Margin = new Thickness(0, 4, 0, 0);
            ConnectButton.VerticalAlignment = VerticalAlignment.Stretch;
            ConnectButton.Height = 38;

            // Status
            StatusPanel.Margin = new Thickness(0, 16, 0, 0);
            StatusPanel.VerticalAlignment = VerticalAlignment.Stretch;
            StatusPanel.Padding = new Thickness(12);
            StatusText.IsVisible = true; // Show text in wide mode
        }
    }
}

public class DeviceTypeToIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null)
        {
            System.Diagnostics.Debug.WriteLine($"DeviceType is NULL");
            return GetIconPath("default");
        }

        var typeString = value.ToString();
        
        if (string.IsNullOrWhiteSpace(typeString))
        {
            System.Diagnostics.Debug.WriteLine($"DeviceType is empty/whitespace");
            return GetIconPath("default");
        }

        var type = typeString.ToLowerInvariant().Trim();

        if (type.Contains("keyboard")) return GetIconPath("keyboard");
        if (type.Contains("mouse")) return GetIconPath("mouse");
        if (type.Contains("headset") || type.Contains("headphone")) return GetIconPath("headset");
        if (type.Contains("motherboard") || type.Contains("mainboard")) return GetIconPath("motherboard");
        if (type.Contains("gpu") || type.Contains("graphics") || type.Contains("video")) return GetIconPath("gpu");
        if (type.Contains("ram") || type.Contains("memory") || type.Contains("dram")) return GetIconPath("ram");
        if (type.Contains("cooler") || type.Contains("fan")) return GetIconPath("fan");
        if (type.Contains("strip") || type.Contains("led") || type.Contains("light")) return GetIconPath("led");
        if (type.Contains("case") || type.Contains("chassis")) return GetIconPath("case");
        if (type.Contains("speaker") || type.Contains("audio")) return GetIconPath("speaker");
        if (type.Contains("monitor") || type.Contains("display")) return GetIconPath("monitor");
        if (type.Contains("controller") || type.Contains("gamepad")) return GetIconPath("controller");
        
        return GetIconPath("default");
    }

    private static Geometry GetIconPath(string iconType)
    {
        return iconType switch
        {
            "keyboard" => Geometry.Parse("M19,10H17V8H19M19,13H17V11H19M16,10H14V8H16M16,13H14V11H16M16,17H8V15H16M7,10H5V8H7M7,13H5V11H7M8,11H10V13H8M8,8H10V10H8M11,11H13V13H11M11,8H13V10H11M20,5H4C2.89,5 2,5.89 2,7V17A2,2 0 0,0 4,19H20A2,2 0 0,0 22,17V7C22,5.89 21.1,5 20,5Z"),
            "mouse" => Geometry.Parse("M11,1.07C7.05,1.56 4,4.92 4,9H11M4,15A8,8 0 0,0 12,23A8,8 0 0,0 20,15V11H4M13,1.07V9H20C20,4.92 16.94,1.56 13,1.07Z"),
            "headset" => Geometry.Parse("M12,1C7,1 3,5 3,10V17A3,3 0 0,0 6,20H9V12H5V10A7,7 0 0,1 12,3A7,7 0 0,1 19,10V12H15V20H19V21H12V23H18A3,3 0 0,0 21,20V10C21,5 16.97,1 12,1Z"),
            "motherboard" => Geometry.Parse("M19,5V7H15V5H19M9,5V11H5V5H9M19,13V19H15V13H19M9,17V19H5V17H9M21,3H13V9H21V3M11,3H3V13H11V3M21,11H13V21H21V11M11,15H3V21H11V15Z"),
            "gpu" => Geometry.Parse("M2,7V8.5H3V17H4.5V7C3.7,7 2.8,7 2,7M6,7V7L6,16H7V17H14V16H22V7H6M17.5,9A2.5,2.5 0 0,1 20,11.5A2.5,2.5 0 0,1 17.5,14A2.5,2.5 0 0,1 15,11.5A2.5,2.5 0 0,1 17.5,9Z"),
            "ram" => Geometry.Parse("M22 19V16H2V19H22M22 11V6H2V11H3.5C4.33 11 5 11.67 5 12.5S4.33 14 3.5 14L2 14V15H22V14H20.5C19.67 14 19 13.33 19 12.5S19.67 11 20.5 11H22M13 13C13 13.55 12.55 14 12 14S11 13.55 11 13V11C11 10.45 11.45 10 12 10S13 10.45 13 11V13Z"),
            "fan" => Geometry.Parse("M12,11A1,1 0 0,0 11,12A1,1 0 0,0 12,13A1,1 0 0,0 13,12A1,1 0 0,0 12,11M12.5,2C17,2 17.11,5.57 14.75,6.75C13.76,7.24 13.32,8.29 13.13,9.22C13.61,9.42 14.03,9.73 14.35,10.13C18.05,8.13 22.03,8.92 22.03,12.5C22.03,17 18.46,17.1 17.28,14.73C16.78,13.74 15.72,13.3 14.79,13.11C14.59,13.59 14.28,14 13.88,14.34C15.87,18.03 15.08,22 11.5,22C7,22 6.91,18.42 9.27,17.24C10.25,16.75 10.69,15.71 10.89,14.79C10.4,14.59 9.97,14.27 9.65,13.87C5.96,15.85 2,15.07 2,11.5C2,7 5.56,6.89 6.74,9.26C7.24,10.25 8.29,10.68 9.22,10.87C9.41,10.39 9.73,9.97 10.14,9.65C8.15,5.96 8.94,2 12.5,2Z"),
            "led" => Geometry.Parse("M12,6A6,6 0 0,1 18,12C18,14.22 16.79,16.16 15,17.2V19A1,1 0 0,1 14,20H10A1,1 0 0,1 9,19V17.2C7.21,16.16 6,14.22 6,12A6,6 0 0,1 12,6M14,21V22A1,1 0 0,1 13,23H11A1,1 0 0,1 10,22V21H14M20,11H23V13H20V11M1,11H4V13H1V11M13,1V4H11V1H13M4.92,3.5L7.05,5.64L5.63,7.05L3.5,4.93L4.92,3.5M16.95,5.63L19.07,3.5L20.5,4.93L18.37,7.05L16.95,5.63Z"),
            "case" => Geometry.Parse("M8,2H16A2,2 0 0,1 18,4V20A2,2 0 0,1 16,22H8A2,2 0 0,1 6,20V4A2,2 0 0,1 8,2M8,4V6H16V4H8M16,8H8V10H16V8M16,18H14V20H16V18Z"),
            "speaker" => Geometry.Parse("M12,12A3,3 0 0,0 9,15A3,3 0 0,0 12,18A3,3 0 0,0 15,15A3,3 0 0,0 12,12M12,20A5,5 0 0,1 7,15A5,5 0 0,1 12,10A5,5 0 0,1 17,15A5,5 0 0,1 12,20M12,4A2,2 0 0,1 14,6A2,2 0 0,1 12,8C10.89,8 10,7.1 10,6C10,4.89 10.89,4 12,4M17,2H7C5.89,2 5,2.89 5,4V20A2,2 0 0,0 7,22H17A2,2 0 0,0 19,20V4C19,2.89 18.1,2 17,2Z"),
            "monitor" => Geometry.Parse("M21,16H3V4H21M21,2H3C1.89,2 1,2.89 1,4V16A2,2 0 0,0 3,18H10V20H8V22H16V20H14V18H21A2,2 0 0,0 23,16V4C23,2.89 22.1,2 21,2Z"),
            "controller" => Geometry.Parse("M7.97,16L5,19C4.67,19.3 4.23,19.5 3.75,19.5A1.75,1.75 0 0,1 2,17.75V17.5L3,10.12C3.21,7.81 5.14,6 7.5,6H16.5C18.86,6 20.79,7.81 21,10.12L22,17.5V17.75A1.75,1.75 0 0,1 20.25,19.5C19.77,19.5 19.33,19.3 19,19L16.03,16H7.97M7,8V10H5V11H7V13H8V11H10V10H8V8H7M16.5,8A0.75,0.75 0 0,0 15.75,8.75A0.75,0.75 0 0,0 16.5,9.5A0.75,0.75 0 0,0 17.25,8.75A0.75,0.75 0 0,0 16.5,8M14.75,9.75A0.75,0.75 0 0,0 14,10.5A0.75,0.75 0 0,0 14.75,11.25A0.75,0.75 0 0,0 15.5,10.5A0.75,0.75 0 0,0 14.75,9.75M18.25,9.75A0.75,0.75 0 0,0 17.5,10.5A0.75,0.75 0 0,0 18.25,11.25A0.75,0.75 0 0,0 19,10.5A0.75,0.75 0 0,0 18.25,9.75M16.5,11.5A0.75,0.75 0 0,0 15.75,12.25A0.75,0.75 0 0,0 16.5,13A0.75,0.75 0 0,0 17.25,12.25A0.75,0.75 0 0,0 16.5,11.5Z"),
            _ => Geometry.Parse("M15,7V11H16V13H13V5H15L12,1L9,5H11V13H8V10.93C8.7,10.56 9.2,9.85 9.2,9C9.2,7.78 8.21,6.8 7,6.8C5.78,6.8 4.8,7.78 4.8,9C4.8,9.85 5.3,10.56 6,10.93V13A2,2 0 0,0 8,15H11V18.05C10.29,18.41 9.8,19.15 9.8,20A2.2,2.2 0 0,0 12,22.2A2.2,2.2 0 0,0 14.2,20C14.2,19.15 13.71,18.41 13,18.05V15H16A2,2 0 0,0 18,13V11H19V7H15Z")
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
