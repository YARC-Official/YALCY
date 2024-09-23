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
        private readonly Point[] _blueLedPositions = new Point[]
        {
            new Point(100, 32),   // LED 0
            new Point(130, 67),   // LED 1
            new Point(166, 98),   // LED 2
            new Point(130, 128),  // LED 3
            new Point(100, 164),  // LED 4
            new Point(70, 128),   // LED 5
            new Point(33, 98),    // LED 6
            new Point(70, 67),    // LED 7
        };

        private readonly Point[] _greenLedPositions = new Point[]
        {
            new Point(134, 11),   // LED 0 (green)
            new Point(187, 65),   // LED 1 (green)
            new Point(187, 133),  // LED 2 (green)
            new Point(134, 186),  // LED 3 (green)
            new Point(65, 186),   // LED 4 (green)
            new Point(12, 133),   // LED 5 (green)
            new Point(12, 65),    // LED 6 (green)
            new Point(65, 11)     // LED 7 (green)
        };

        private readonly Point[] _yellowLedPositions = new Point[]
        {
            new Point(100, -32),  // LED 0 (yellow)
            new Point(192, 9),    // LED 1 (yellow)
            new Point(230, 98),   // LED 2 (yellow)
            new Point(192, 189),  // LED 3 (yellow)
            new Point(100, 229),  // LED 4 (yellow)
            new Point(10, 189),   // LED 5 (yellow)
            new Point(-31, 98),   // LED 6 (yellow)
            new Point(10, 9)      // LED 7 (yellow)
        };

        private readonly Point[] _redLedPositions = new Point[]
        {
            new Point(151, -24),  // LED 0 (red)
            new Point(219, 46),   // LED 1 (red)
            new Point(219, 152),  // LED 2 (red)
            new Point(151, 221),  // LED 3 (red)
            new Point(49, 221),   // LED 4 (red)
            new Point(-23, 152),  // LED 5 (red)
            new Point(-23, 46),   // LED 6 (red)
            new Point(49, -24)    // LED 7 (red)
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
