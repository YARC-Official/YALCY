using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using System;
using System.Linq;

namespace YALCY.Views.Components;

public partial class LedVisualizerPanel : UserControl
{
    private bool _isInitialized = false;
    private bool _isVisible = false;

    public LedVisualizerPanel()
    {
        InitializeComponent();
        
        // Only attach events after the control is fully loaded and visible
        this.AttachedToVisualTree += (sender, e) => 
        {
            if (!_isInitialized)
            {
                InitializeEvents();
                _isInitialized = true;
            }
        };
        
        // Monitor visibility changes to optimize rendering
        this.GetObservable(IsVisibleProperty).Subscribe(isVisible =>
        {
            _isVisible = isVisible;
            if (isVisible && _isInitialized)
            {
                // Force a layout update when becoming visible
                InvalidateArrange();
            }
        });
    }

    private void InitializeEvents()
    {
        // Center image when attached to visual tree
        CenterImage();
        
        // Only listen to bounds changes when necessary and visible
        CenteredImage.PropertyChanged += (sender, e) =>
        {
            if (e.Property == Image.BoundsProperty && _isInitialized && _isVisible)
            {
                CenterImage();
            }
        };
    }

    private void CenterImage()
    {
        if (CanvasContainer?.Bounds.Width > 0 && CenteredImage?.Bounds.Width > 0)
        {
            var containerBounds = CanvasContainer.Bounds;
            var imageBounds = CenteredImage.Bounds;

            // Calculate the top-left position to center the image
            var left = (containerBounds.Width - imageBounds.Width) / 2;
            var top = (containerBounds.Height - imageBounds.Height) / 2;

            // Set Canvas.Left and Canvas.Top to position the image
            Canvas.SetLeft(CenteredImage, left);
            Canvas.SetTop(CenteredImage, top);
        }
    }
}
