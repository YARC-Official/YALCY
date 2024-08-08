using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using System;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace YALCY.ViewModels
{
    public class LedDisplay : Control
    {
        private bool[] _ledStates;

        public static readonly StyledProperty<int> ColorProperty = AvaloniaProperty.Register<LedDisplay, int>(nameof(Color));
        Point[] BlueledPositions = new Point[]
        {
            new Point(-34, -101),   // LED 0
            new Point(-4, -66), // LED 1
            new Point(32, -35),// LED 2
            new Point(-4, -5), // LED 3
            new Point(-34, 30), // LED 4
            new Point(-64, -5),  // LED 5
            new Point(-101, -35),  // LED 6
            new Point(-64, -66),  // LED 7
        };

        private Point[] GreenledPositions = new Point[]
        {
            new Point(0, -122), // LED 0 (green)
            new Point(53, -68), // LED 1 (green)
            new Point(53, 0), // LED 2 (green)
            new Point(0, 53), // LED 3 (green)
            new Point(-69, 53), // LED 4 (green)
            new Point(-122, 0), // LED 5 (green)
            new Point(-122, -68), // LED 6 (green)
            new Point(-69, -122) // LED 7 (green)
        };
        Point[] YellowledPositions = new Point[]
        {
            new Point(-34, -165), // LED 0 (yellow)
            new Point(58, -124), // LED 1 (yellow)
            new Point(96, -35), // LED 2 (yellow)
            new Point(58, 56), // LED 3 (yellow)
            new Point(-34, 96), // LED 4 (yellow)
            new Point(-124, 56), // LED 5 (yellow)
            new Point(-165, -35), // LED 6 (yellow)
            new Point(-124, -124) // LED 7 (yellow)
        };
        Point[] RedledPositions = new Point[]
        {
            new Point(17, -157), // LED 0 (red)
            new Point(85, -87), // LED 1 (red)
            new Point(85, 19), // LED 2 (red)
            new Point(17, 88), // LED 3 (red)
            new Point(-85, 88), // LED 4 (red)
            new Point(-157, 19), // LED 5 (red)
            new Point(-157, -87), // LED 6 (red)
            new Point(-85, -157) // LED 7 (red)
        };

        static LedDisplay()
        {
            AffectsRender<LedDisplay>(ColorProperty);
        }

        public LedDisplay()
        {
            LedStates = new bool[8];
            USBDeviceMonitor.OnStageKitCommand += OnStageKitEvent;
        }

        public int Color
        {
            get => GetValue(ColorProperty);
            set => SetValue(ColorProperty, value);
        }

        public bool[] LedStates
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
            Uri imageSource = null;
            Point[] ledPositions = null;

            switch (Color)
            {
                case 0:
                    imageSource = new Uri("avares://YALCY/Assets/Stage Kit/BlueLed.png");
                    ledPositions = BlueledPositions;
                    break;

                case 1:
                    imageSource = new Uri("avares://YALCY/Assets/Stage Kit/GreenLed.png");
                    ledPositions = GreenledPositions;
                    break;

                case 2:
                    imageSource = new Uri("avares://YALCY/Assets/Stage Kit/YellowLed.png");
                    ledPositions = YellowledPositions;
                    break;

                case 3:
                    imageSource = new Uri("avares://YALCY/Assets/Stage Kit/RedLed.png");
                    ledPositions = RedledPositions;
                    break;
            }

            var bitmap = new Bitmap(AssetLoader.Open(imageSource));

            // Draw each LED at the specified position if it is on
            for (int i = 0; i < LedStates.Length; i++)
            {
                if (LedStates[i])
                {
                    context.DrawImage(bitmap,new Rect(ledPositions[i], new Size(bitmap.Size.Width, bitmap.Size.Height)));
                }
            }
        }

        private void OnStageKitEvent(StageKitTalker.CommandId commandId, byte parameter)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                bool updateLed = false;
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

                if (updateLed)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        LedStates[i] = (parameter & (1 << i)) != 0;
                    }
                    InvalidateVisual();
                }
            });
        }
    }
}
