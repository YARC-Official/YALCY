using Avalonia.Controls;

namespace YALCY.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        CenteredImage.AttachedToVisualTree += (sender, e) => CenterImage();
        CenteredImage.PropertyChanged += (sender, e) =>
        {
            if (e.Property == BoundsProperty)
            {
                CenterImage();
            }
        };
    }

    private void CenterImage()
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
