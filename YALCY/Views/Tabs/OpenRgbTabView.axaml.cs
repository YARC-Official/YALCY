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
