using Avalonia.Controls;
using Avalonia.Data.Converters;
using System;
using System.Globalization;
using Avalonia.Media;
using Avalonia.Layout;
using Avalonia;
using YALCY.ViewModels;

namespace YALCY.Views.Tabs;

public partial class OpenRgbTabView : UserControl
{
    private bool? _lastIsCompact = null;

    public OpenRgbTabView()
    {
        InitializeComponent();
        this.DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(MainWindowViewModel.IsNarrowMode))
                {
                    ApplyLayoutState(vm.IsNarrowMode);
                }
            };
            ApplyLayoutState(vm.IsNarrowMode);
        }
    }

    private void ApplyLayoutState(bool isCompact)
    {
        if (_lastIsCompact == isCompact) return;
        _lastIsCompact = isCompact;

        try
        {
            if (isCompact)
            {
                // Reset child row/col assignments to 0 first to prevent index out of range exceptions
                Grid.SetRow(HeaderPanel, 0);
                Grid.SetColumn(HeaderPanel, 0);
                Grid.SetRow(ConnectionForm, 0);
                Grid.SetColumn(ConnectionForm, 0);
                Grid.SetRow(StrobeModePanel, 0);
                Grid.SetColumn(StrobeModePanel, 0);
                Grid.SetRow(StatusPanel, 0);
                Grid.SetColumn(StatusPanel, 0);
                Grid.SetRow(RequirementsPanel, 0);
                Grid.SetColumn(RequirementsPanel, 0);

                // --- Compact Mode (2 Rows Layout) ---
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

                ConnectionCard.Margin = new Thickness(0, 0, 0, 10);
                ConnectionCard.Padding = new Thickness(16, 12);

                // 2-row grid:
                //   Row 0: [Title + Divider] | [IP + Port + Connect Button]
                //   Row 1: [Strobe Options]  | [Status Indicator Pill]
                ConnectionPanelStack.ColumnDefinitions.Clear();
                ConnectionPanelStack.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));                      // Col 0: Title / Strobe
                ConnectionPanelStack.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star))); // Col 1: Form / Status

                ConnectionPanelStack.RowDefinitions.Clear();
                ConnectionPanelStack.RowDefinitions.Add(new RowDefinition(GridLength.Auto)); // Row 0: Title + Form
                ConnectionPanelStack.RowDefinitions.Add(new RowDefinition(GridLength.Auto)); // Row 1: Strobe + Status

                // --- Row 0: Title + Form ---
                CompactDivider.IsVisible = true;
                HeaderSubtitle.IsVisible = false;
                HeaderPanel.Margin = new Thickness(0, 0, 16, 0);
                HeaderPanel.VerticalAlignment = VerticalAlignment.Center;
                HeaderPanel.Spacing = 14;
                Grid.SetRow(HeaderPanel, 0);
                Grid.SetColumn(HeaderPanel, 0);
                Grid.SetColumnSpan(HeaderPanel, 1);

                RequirementsPanel.IsVisible = false;

                ConnectionForm.Orientation = Orientation.Horizontal;
                ConnectionForm.Margin = new Thickness(0);
                ConnectionForm.VerticalAlignment = VerticalAlignment.Center;
                ConnectionForm.HorizontalAlignment = HorizontalAlignment.Left;
                Grid.SetRow(ConnectionForm, 0);
                Grid.SetColumn(ConnectionForm, 1);
                Grid.SetColumnSpan(ConnectionForm, 1);

                ServerIpLabel.IsVisible = false;
                ServerIpPanel.Width = double.NaN;
                ServerIpPanel.Margin = new Thickness(0, 0, 10, 0);
                ServerIpPanel.VerticalAlignment = VerticalAlignment.Center;
                ServerIpInput.Width = 130;
                ServerIpInput.Height = 32;

                ServerPortLabel.IsVisible = false;
                ServerPortPanel.Width = double.NaN;
                ServerPortPanel.Margin = new Thickness(0, 0, 12, 0);
                ServerPortPanel.VerticalAlignment = VerticalAlignment.Center;
                ServerPortInput.Width = 75;
                ServerPortInput.Height = 32;
                ServerPortInput.ShowButtonSpinner = false;

                ConnectButton.Width = double.NaN;
                ConnectButton.Margin = new Thickness(0);
                ConnectButton.VerticalAlignment = VerticalAlignment.Center;
                ConnectButton.Height = 32;
                ConnectButton.Padding = new Thickness(14, 0);

                // --- Row 1: Strobe Options (Left) + Status Pill (Right) ---
                StrobeModePanel.IsVisible = true;
                StrobeModeLabel.IsVisible = false;
                StrobeModePanel.Margin = new Thickness(0, 10, 0, 0);
                StrobeModePanel.VerticalAlignment = VerticalAlignment.Center;
                StrobeModePanel.HorizontalAlignment = HorizontalAlignment.Left;
                StrobeModePanel.Width = 260;
                Grid.SetRow(StrobeModePanel, 1);
                Grid.SetColumn(StrobeModePanel, 0);
                Grid.SetColumnSpan(StrobeModePanel, 1);

                StatusPanel.Margin = new Thickness(16, 10, 0, 0);
                StatusPanel.VerticalAlignment = VerticalAlignment.Center;
                StatusPanel.HorizontalAlignment = HorizontalAlignment.Right;
                StatusPanel.Padding = new Thickness(12, 5);
                StatusPanel.CornerRadius = new CornerRadius(6);
                StatusText.IsVisible = true;
                StatusText.MaxWidth = 320;
                StatusText.TextTrimming = TextTrimming.CharacterEllipsis;
                Grid.SetRow(StatusPanel, 1);
                Grid.SetColumn(StatusPanel, 1);
                Grid.SetColumnSpan(StatusPanel, 1);

                ConnectionPanelStack.HorizontalAlignment = HorizontalAlignment.Stretch;
            }
            else
            {
                // Reset child row/col assignments to 0 first to prevent index out of range exceptions
                Grid.SetRow(HeaderPanel, 0);
                Grid.SetColumn(HeaderPanel, 0);
                Grid.SetRow(ConnectionForm, 0);
                Grid.SetColumn(ConnectionForm, 0);
                Grid.SetRow(StrobeModePanel, 0);
                Grid.SetColumn(StrobeModePanel, 0);
                Grid.SetRow(StatusPanel, 0);
                Grid.SetColumn(StatusPanel, 0);
                Grid.SetRow(RequirementsPanel, 0);
                Grid.SetColumn(RequirementsPanel, 0);

                // --- Wide Mode (Left Sidebar + Main Devices Area) ---
                ContentGrid.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
                ContentGrid.RowDefinitions[1].Height = GridLength.Auto;

                ContentGrid.ColumnDefinitions[0].Width = new GridLength(380);
                ContentGrid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);

                Grid.SetRow(ConnectionCard, 0);
                Grid.SetColumn(ConnectionCard, 0);
                Grid.SetColumnSpan(ConnectionCard, 1);

                Grid.SetRow(DevicesCard, 0);
                Grid.SetColumn(DevicesCard, 1);
                Grid.SetColumnSpan(DevicesCard, 1);

                ConnectionCard.Margin = new Thickness(0, 0, 16, 0);
                ConnectionCard.Padding = new Thickness(20);

                // Connection Card Internals (Single column, 5 stacked rows)
                ConnectionPanelStack.ColumnDefinitions.Clear();
                ConnectionPanelStack.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));

                ConnectionPanelStack.RowDefinitions.Clear();
                ConnectionPanelStack.RowDefinitions.Add(new RowDefinition(GridLength.Auto)); // Row 0: Header
                ConnectionPanelStack.RowDefinitions.Add(new RowDefinition(GridLength.Auto)); // Row 1: Requirements
                ConnectionPanelStack.RowDefinitions.Add(new RowDefinition(GridLength.Auto)); // Row 2: Form
                ConnectionPanelStack.RowDefinitions.Add(new RowDefinition(GridLength.Auto)); // Row 3: Strobe
                ConnectionPanelStack.RowDefinitions.Add(new RowDefinition(GridLength.Auto)); // Row 4: Status

                Grid.SetRow(HeaderPanel, 0);
                Grid.SetColumn(HeaderPanel, 0);
                Grid.SetColumnSpan(HeaderPanel, 1);

                HeaderSubtitle.IsVisible = true;
                HeaderPanel.Margin = new Thickness(0, 0, 0, 16);
                HeaderPanel.VerticalAlignment = VerticalAlignment.Stretch;
                HeaderPanel.Spacing = 12;

                CompactDivider.IsVisible = false;

                RequirementsPanel.IsVisible = true;

                Grid.SetRow(RequirementsPanel, 1);
                Grid.SetColumn(RequirementsPanel, 0);
                Grid.SetColumnSpan(RequirementsPanel, 1);

                Grid.SetRow(ConnectionForm, 2);
                Grid.SetColumn(ConnectionForm, 0);
                Grid.SetColumnSpan(ConnectionForm, 1);

                Grid.SetRow(StrobeModePanel, 3);
                Grid.SetColumn(StrobeModePanel, 0);
                Grid.SetColumnSpan(StrobeModePanel, 1);

                Grid.SetRow(StatusPanel, 4);
                Grid.SetColumn(StatusPanel, 0);
                Grid.SetColumnSpan(StatusPanel, 1);

                ConnectionPanelStack.HorizontalAlignment = HorizontalAlignment.Stretch;

                ConnectionForm.Orientation = Orientation.Vertical;
                ConnectionForm.Margin = new Thickness(0, 0, 0, 16);
                ConnectionForm.VerticalAlignment = VerticalAlignment.Top;
                ConnectionForm.HorizontalAlignment = HorizontalAlignment.Stretch;

                ServerIpLabel.IsVisible = true;
                ServerIpPanel.Width = double.NaN;
                ServerIpPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
                ServerIpPanel.Margin = new Thickness(0, 0, 0, 14);
                ServerIpInput.Width = double.NaN;
                ServerIpInput.HorizontalAlignment = HorizontalAlignment.Stretch;
                ServerIpInput.Height = 36;

                ServerPortLabel.IsVisible = true;
                ServerPortPanel.Width = double.NaN;
                ServerPortPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
                ServerPortPanel.Margin = new Thickness(0, 0, 0, 16);
                ServerPortInput.Width = double.NaN;
                ServerPortInput.HorizontalAlignment = HorizontalAlignment.Stretch;
                ServerPortInput.Height = 36;
                ServerPortInput.ShowButtonSpinner = false;

                ConnectButton.Width = double.NaN;
                ConnectButton.Margin = new Thickness(0);
                ConnectButton.VerticalAlignment = VerticalAlignment.Stretch;
                ConnectButton.HorizontalAlignment = HorizontalAlignment.Stretch;
                ConnectButton.Height = 38;
                ConnectButton.Padding = new Thickness(14, 0);

                StrobeModeLabel.IsVisible = true;
                StrobeModePanel.IsVisible = true;
                StrobeModePanel.Margin = new Thickness(0, 0, 0, 18);
                StrobeModePanel.HorizontalAlignment = HorizontalAlignment.Stretch;
                StrobeModePanel.Width = double.NaN;

                StatusPanel.Margin = new Thickness(0);
                StatusPanel.VerticalAlignment = VerticalAlignment.Stretch;
                StatusPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
                StatusPanel.Padding = new Thickness(12);
                StatusPanel.CornerRadius = new CornerRadius(8);
                StatusText.IsVisible = true;
                StatusText.MaxWidth = double.PositiveInfinity;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OpenRgbTabView] Layout update error: {ex}");
        }
    }
}
