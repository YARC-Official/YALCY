using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using System;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using YALCY.Integrations.StageKit;
using YALCY.Usb;

namespace YALCY.ViewModels;

    public class LedDisplay : Control
    {
        private bool[] _ledStates;

        public static readonly StyledProperty<int> ColorProperty = AvaloniaProperty.Register<LedDisplay, int>(nameof(Color));

        //Yeah, harded coded positions... I know, I know... I'll fix it later.
        // Normalized LED positions (0-200 coordinate system, centered at 100,100)
        private readonly Point[] _blueLedPositions = new Point[]
        {
            new Point(100, 32),   // LED 0 - Top
            new Point(130, 67),   // LED 1 - Top Right
            new Point(166, 98),   // LED 2 - Right
            new Point(130, 128),  // LED 3 - Bottom Right
            new Point(100, 164),  // LED 4 - Bottom
            new Point(70, 128),   // LED 5 - Bottom Left
            new Point(33, 98),    // LED 6 - Left
            new Point(70, 67),    // LED 7 - Top Left
        };

        private readonly Point[] _greenLedPositions = new Point[]
        {
            new Point(134, 11),   // LED 0 (green) - Outer Top
            new Point(187, 65),   // LED 1 (green) - Outer Top Right
            new Point(187, 133),  // LED 2 (green) - Outer Bottom Right
            new Point(134, 186),  // LED 3 (green) - Outer Bottom
            new Point(65, 186),   // LED 4 (green) - Outer Bottom Left
            new Point(12, 133),   // LED 5 (green) - Outer Bottom Left
            new Point(12, 65),    // LED 6 (green) - Outer Top Left
            new Point(65, 11)     // LED 7 (green) - Outer Top
        };

        private readonly Point[] _yellowLedPositions = new Point[]
        {
            new Point(100, -32),  // LED 0 (yellow) - Far Top
            new Point(192, 9),    // LED 1 (yellow) - Far Top Right
            new Point(230, 98),   // LED 2 (yellow) - Far Right
            new Point(192, 189),  // LED 3 (yellow) - Far Bottom Right
            new Point(100, 229),  // LED 4 (yellow) - Far Bottom
            new Point(10, 189),   // LED 5 (yellow) - Far Bottom Left
            new Point(-31, 98),   // LED 6 (yellow) - Far Left
            new Point(10, 9)      // LED 7 (yellow) - Far Top Left
        };

        private readonly Point[] _redLedPositions = new Point[]
        {
            new Point(151, -24),  // LED 0 (red) - Farthest Top
            new Point(219, 46),   // LED 1 (red) - Farthest Top Right
            new Point(219, 152),  // LED 2 (red) - Farthest Right
            new Point(151, 221),  // LED 3 (red) - Farthest Bottom Right
            new Point(49, 221),   // LED 4 (red) - Farthest Bottom
            new Point(-23, 152),  // LED 5 (red) - Farthest Bottom Left
            new Point(-23, 46),   // LED 6 (red) - Farthest Left
            new Point(49, -24)    // LED 7 (red) - Farthest Top Left
        };

        static LedDisplay()
        {
            AffectsRender<LedDisplay>(ColorProperty);
        }

        public LedDisplay()
        {
            LedStates = new bool[8];
            UsbDeviceMonitor.OnStageKitCommand += OnStageKitEvent;
        }

        public int Color
        {
            get => GetValue(ColorProperty);
            set => SetValue(ColorProperty, value);
        }

        private bool[] LedStates
        {
            get => _ledStates;
            set
            {
                _ledStates = value;
                InvalidateVisual();
            }
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (LedStates == null) return;

            // Determine the image source based on the color
            Uri? imageSource = null;
            Point[] ledPositions = null;

            switch (Color)
            {
                case 0:
                    imageSource = new Uri("avares://YALCY/Resources/StageKit/BlueLed.png");
                    ledPositions = _blueLedPositions;
                    break;

                case 1:
                    imageSource = new Uri("avares://YALCY/Resources/StageKit/GreenLed.png");
                    ledPositions = _greenLedPositions;
                    break;

                case 2:
                    imageSource = new Uri("avares://YALCY/Resources/StageKit/YellowLed.png");
                    ledPositions = _yellowLedPositions;
                    break;

                case 3:
                    imageSource = new Uri("avares://YALCY/Resources/StageKit/RedLed.png");
                    ledPositions = _redLedPositions;
                    break;
            }

            if (imageSource == null) return;

            var bitmap = new Bitmap(AssetLoader.Open(imageSource));

            // Calculate half the size of the image for centering
            var halfWidth = bitmap.Size.Width / 2;
            var halfHeight = bitmap.Size.Height / 2;

            // Draw each LED at the specified position if it is on
            for (int i = 0; i < LedStates.Length; i++)
            {
                if (!LedStates[i]) continue;
                if (ledPositions == null) continue;
                var position = new Point(ledPositions[i].X - halfWidth, ledPositions[i].Y - halfHeight);
                context.DrawImage(bitmap, new Rect(position, new Size(bitmap.Size.Width, bitmap.Size.Height)));
            }
        }

        private void OnStageKitEvent(StageKitTalker.CommandId commandId, byte parameter)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                var updateLed = false;
                switch (commandId)
                {
                    case StageKitTalker.CommandId.BlueLeds:
                        updateLed = (Color == 0);
                        break;

                    case StageKitTalker.CommandId.GreenLeds:
                        updateLed = (Color == 1);
                        break;

                    case StageKitTalker.CommandId.YellowLeds:
                        updateLed = (Color == 2);
                        break;

                    case StageKitTalker.CommandId.RedLeds:
                        updateLed = (Color == 3);
                        break;
                }

                if (!updateLed) return;
                for (int i = 0; i < 8; i++)
                {
                    LedStates[i] = (parameter & (1 << i)) != 0;
                }
                InvalidateVisual();
            });
        }
    }
