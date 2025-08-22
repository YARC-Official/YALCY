using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using YALCY.Views.Components;
using YALCY.Views.Windows;
using Avalonia.Interactivity;

namespace YALCY.Views.Tabs;

public partial class YargTabView : UserControl
{
    private static LedVisualizerWindow? _allDetachedWindow;
    private static StrobeVisualizerWindow? _allStrobeWindow;
    
    private LedVisualizerWindow? _detachedWindow;
    private StrobeVisualizerWindow? strobeVisualizerWindow;
    private Grid? _mainGrid;
    private LedVisualizerPanel? _originalVisualizer;
    private bool _isVisualizerDetached = false;
    
    public static void CloseAllDetachedWindows()
    {
        // Close detached LED window
        if (_allDetachedWindow != null && _allDetachedWindow.IsVisible)
        {
            try
            {
                _allDetachedWindow.Close();
            }
            catch (Exception) { }
            _allDetachedWindow = null;
        }
        
        // Close strobe window
        if (_allStrobeWindow != null && _allStrobeWindow.IsVisible)
        {
            try
            {
                _allStrobeWindow.Close();
            }
            catch (Exception) { }
            _allStrobeWindow = null;
        }
    }

    public YargTabView()
    {
        InitializeComponent();
        _mainGrid = this.FindControl<Grid>("MainGrid");
        _originalVisualizer = LedVisualizer;
    }

    private void OnDetachVisualizerClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var toggleButton = sender as Avalonia.Controls.Primitives.ToggleButton;
        if (toggleButton?.IsChecked == true)
        {
            DetachLedVisualizer();
        }
        else
        {
            AttachLedVisualizer();
        }
    }
    
    private void OnStrobeVisualizerClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var toggleButton = sender as Avalonia.Controls.Primitives.ToggleButton;
        if (toggleButton?.IsChecked == false)
        {
            // Close strobe visualizer
            if (strobeVisualizerWindow != null)
            {
                strobeVisualizerWindow.Close();
                strobeVisualizerWindow = null;
            }
        }
        else
        {
            // Open strobe visualizer
            if (strobeVisualizerWindow == null)
            {
                strobeVisualizerWindow = new StrobeVisualizerWindow();
                _allStrobeWindow = strobeVisualizerWindow;
                
                strobeVisualizerWindow.Closed += (sender, e) =>
                {
                    // Reset button state when window is closed
                    if (toggleButton != null)
                    {
                        toggleButton.IsChecked = false;
                    }
                    _allStrobeWindow = null;
                    strobeVisualizerWindow = null;
                };
                strobeVisualizerWindow.Show();
            }
        }
    }

    private void DetachLedVisualizer()
    {
        if (_detachedWindow != null)
        {
            _detachedWindow.Activate();
            return;
        }

        if (_isVisualizerDetached)
        {
            return;
        }
        
        if (_originalVisualizer != null)
        {
            _originalVisualizer.IsVisible = false;
            _originalVisualizer.Opacity = 0.0;
            _originalVisualizer.Width = 0;
            _originalVisualizer.Height = 0;
            _originalVisualizer.Margin = new Avalonia.Thickness(-1000, -1000, 0, 0);
            
            var namedVisualizer = this.FindControl<LedVisualizerPanel>("LedVisualizer");
            if (namedVisualizer != null)
            {
                namedVisualizer.IsVisible = false;
                namedVisualizer.Opacity = 0.0;
                namedVisualizer.Width = 0;
                namedVisualizer.Height = 0;
                namedVisualizer.Margin = new Avalonia.Thickness(-1000, -1000, 0, 0);
            }
            
            if (_mainGrid != null)
            {
                var visualizersToRemove = _mainGrid.Children.OfType<LedVisualizerPanel>().ToList();
                foreach (var viz in visualizersToRemove)
                {
                    _mainGrid.Children.Remove(viz);
                }
            }
            
            this.InvalidateVisual();
            this.InvalidateMeasure();
            this.InvalidateArrange();
            
            var dataGrid = this.FindControl<Avalonia.Controls.DataGrid>("MainDataGrid");
            if (dataGrid != null)
            {
                Grid.SetColumnSpan(dataGrid, 2);
                dataGrid.InvalidateVisual();
            }
            
            _isVisualizerDetached = true;
            DetachVisualizerButton.Content = "Attach LED Visualizer";
        }

        _detachedWindow = new LedVisualizerWindow();
        _allDetachedWindow = _detachedWindow;
        var mainWindow = this.VisualRoot as Window;
        if (mainWindow != null)
        {
            var position = mainWindow.Position;
            _detachedWindow.Position = new PixelPoint(
                (int)(position.X + mainWindow.Width + 10),
                (int)position.Y
            );
        }

        _detachedWindow.Closed += (sender, e) =>
        {
            if (_detachedWindow != null)
            {
                _allDetachedWindow = null;
            }
            
            _detachedWindow = null;
            AttachLedVisualizer();
        };

        _detachedWindow.Show();
    }

    private void AttachLedVisualizer()
    {
        if (!_isVisualizerDetached)
        {
            return;
        }

        if (_detachedWindow != null)
        {
            _allDetachedWindow = null;
            _detachedWindow.Close();
            _detachedWindow = null;
        }

        if (_mainGrid != null)
        {
            var newVisualizer = new LedVisualizerPanel();
            newVisualizer.DataContext = this.DataContext;
            Grid.SetRow(newVisualizer, 3);
            Grid.SetColumn(newVisualizer, 1);
            newVisualizer.Height = 320;
            newVisualizer.Width = 320;
            newVisualizer.Margin = new Thickness(20, 0, 20, 0);
            
            _mainGrid.Children.Add(newVisualizer);
            _originalVisualizer = newVisualizer;
            
            var dataGrid = this.FindControl<Avalonia.Controls.DataGrid>("MainDataGrid");
            if (dataGrid != null)
            {
                Grid.SetColumnSpan(dataGrid, 1);
            }
            
            _isVisualizerDetached = false;
            DetachVisualizerButton.Content = "Detach LED Visualizer";
            DetachVisualizerButton.IsChecked = false;
        }
    }
}
