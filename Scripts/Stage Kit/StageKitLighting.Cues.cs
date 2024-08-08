using System;
using System.Collections.Generic;

namespace YALCY
{
    // Parent of primitives
    // Grandparent of cues
    public abstract class StageKitLighting
    {
        protected const byte None = 0b00000000;
        protected const byte Zero = 0b00000001;
        protected const byte One = 0b00000010;
        protected const byte Two = 0b00000100;
        protected const byte Three = 0b00001000;
        protected const byte Four = 0b00010000;
        protected const byte Five = 0b00100000;
        protected const byte Six = 0b01000000;
        protected const byte Seven = 0b10000000;
        protected const byte All = 0b11111111;

        [Flags]
        public enum ListenTypes
        {
            Next = 1,
            MajorBeat = 2,
            MinorBeat = 4,
            RedFretDrums = 8,
        }

        public virtual void Enable()
        {
        }

        public virtual void KillSelf()
        {
        }
    }

    // This is the parent class of all lighting cues. (not primitives)
    public abstract class StageKitLightingCue : StageKitLighting
    {
        protected const StageKitTalker.CommandId Blue = StageKitTalker.CommandId.BlueLeds;
        protected const StageKitTalker.CommandId Green = StageKitTalker.CommandId.GreenLeds;
        protected const StageKitTalker.CommandId Yellow = StageKitTalker.CommandId.YellowLeds;
        protected const StageKitTalker.CommandId Red = StageKitTalker.CommandId.RedLeds;

        public List<StageKitLighting> CuePrimitives = new();
    }
    public class NoCue : StageKitLightingCue
    {
        public override void Enable()
        {
            UsbDeviceMonitor.SendReport(StageKitTalker.CommandId.DisableAll, None);
        }
    }

    public class BigRockEnding : StageKitLightingCue
    {
        private static readonly (StageKitTalker.CommandId, byte)[] PatternList1 =
        {
            (Red, All),
            (Red, None),
            (Red, None),
            (Red, None),
        };
        private static readonly (StageKitTalker.CommandId, byte)[] PatternList2 =
        {
            (Yellow, None),
            (Yellow, None),
            (Yellow, All),
            (Yellow, None),
        };
        private static readonly (StageKitTalker.CommandId, byte)[] PatternList3 =
        {
            (Green, None),
            (Green, All),
            (Green, None),
            (Green, None),
        };
        private static readonly (StageKitTalker.CommandId, byte)[] PatternList4 =
        {
            (Blue, None),
            (Blue, None),
            (Blue, None),
            (Blue, All),
        };

        public override void Enable()
        {
            CuePrimitives.Add(new BeatPattern(PatternList1, 2f)); //?
            CuePrimitives.Add(new BeatPattern(PatternList2, 2f)); //?
            CuePrimitives.Add(new BeatPattern(PatternList3, 2f)); //?
            CuePrimitives.Add(new BeatPattern(PatternList4, 2f)); //?
            UsbDeviceMonitor.SendReport(Blue, All);
            UsbDeviceMonitor.SendReport(Red, All);
            UsbDeviceMonitor.SendReport(Yellow, All);
            UsbDeviceMonitor.SendReport(Green, All);

            foreach (var primitive in CuePrimitives)
            {
                primitive.Enable();
            }
        }
    }

    public class LoopWarm : StageKitLightingCue
    {
        private static readonly (StageKitTalker.CommandId, byte)[] PatternList1 =
        {
            (Red, Zero | Four),
            (Red, One | Five),
            (Red, Two | Six),
            (Red, Three | Seven),
        };

        private static readonly (StageKitTalker.CommandId, byte)[] PatternList2 =
        {
            (Yellow, Two),
            (Yellow, One),
            (Yellow, Zero),
            (Yellow, Seven),
            (Yellow, Six),
            (Yellow, Five),
            (Yellow, Four),
            (Yellow, Three),
        };

        public override void Enable()
        {
            UsbDeviceMonitor.SendReport(Green, None);
            UsbDeviceMonitor.SendReport(Blue, None);
            CuePrimitives.Add(new BeatPattern(PatternList1, 1f/PatternList1.Length)); //Set
            CuePrimitives.Add(new BeatPattern(PatternList2, 1f/PatternList2.Length)); //Set

            foreach (var primitive in CuePrimitives)
            {
                primitive.Enable();
            }
        }
    }

    public class LoopCool : StageKitLightingCue
    {
        private static readonly (StageKitTalker.CommandId, byte)[] PatternList1 =
        {
            (Blue, Zero | Four),
            (Blue, One | Five),
            (Blue, Two | Six),
            (Blue, Three | Seven),
        };

        private static readonly (StageKitTalker.CommandId, byte)[] PatternList2 =
        {
            (Green, Two),
            (Green, One),
            (Green, Zero),
            (Green, Seven),
            (Green, Six),
            (Green, Five),
            (Green, Four),
            (Green, Three),
        };

        public override void Enable()
        {
            UsbDeviceMonitor.SendReport(Yellow, None);
            UsbDeviceMonitor.SendReport(Red, None);
            CuePrimitives.Add(new BeatPattern(PatternList1, 1f/PatternList1.Length)); //Set
            CuePrimitives.Add(new BeatPattern(PatternList2, 1f/PatternList2.Length)); //Set

            foreach (var primitive in CuePrimitives)
            {
                primitive.Enable();
            }
        }
    }

    public class Harmony : StageKitLightingCue
    {
        private static readonly (StageKitTalker.CommandId, byte)[] LargePatternList1 =
        {
            (Yellow, Three),
            (Yellow, Two),
            (Yellow, One),
            (Yellow, Zero),
            (Yellow, Seven),
            (Yellow, Six),
            (Yellow, Five),
            (Yellow, Four),
        };

        private static readonly (StageKitTalker.CommandId, byte)[] LargePatternList2 =
        {
            (Red, Four),
            (Red, Three),
            (Red, Two),
            (Red, One),
            (Red, Zero),
            (Red, Seven),
            (Red, Six),
            (Red, Five),
        };

        private static readonly (StageKitTalker.CommandId, byte)[] SmallPatternList1 =
        {
            (Green, Four),
            (Green, Five),
            (Green, Six),
            (Green, Seven),
            (Green, Zero),
            (Green, One),
            (Green, Two),
            (Green, Three),
        };

        private static readonly (StageKitTalker.CommandId, byte)[] SmallPatternList2 =
        {
            (Blue, Four),
            (Blue, Five),
            (Blue, Six),
            (Blue, Seven),
            (Blue, Zero),
            (Blue, One),
            (Blue, Two),
            (Blue, Three),
        };

        public override void Enable()
        {
            if (UdpIntake.Buffer[(int)UdpIntake.ByteIndexName.VenueSize] == (byte)UdpIntake.VenueSizeByte.Large)
            {
                UsbDeviceMonitor.SendReport(Blue, None);
                UsbDeviceMonitor.SendReport(Green, None);
                CuePrimitives.Add(new BeatPattern(LargePatternList1, 1f/LargePatternList1.Length)); //set
                CuePrimitives.Add(new BeatPattern(LargePatternList2, 1f/LargePatternList2.Length)); //set
            }
            else
            {
                UsbDeviceMonitor.SendReport(Red, None);
                UsbDeviceMonitor.SendReport(Yellow, None);
                CuePrimitives.Add(new BeatPattern(SmallPatternList1, 1f/SmallPatternList1.Length)); //set
                CuePrimitives.Add(new BeatPattern(SmallPatternList2, 1f/SmallPatternList1.Length)); //set
            }

            foreach (var primitive in CuePrimitives)
            {
                primitive.Enable();
            }
        }
    }

    public class Sweep : StageKitLightingCue
    {
        private static readonly (StageKitTalker.CommandId, byte)[] LargePatternList1 =
        {
            (Red, Six | Two),
            (Red, Five | One),
            (Red, Four | Zero),
            (Red, Three | Seven),
        };

        private static readonly (StageKitTalker.CommandId, byte)[] SmallPatternList1 =
        {
            (Yellow, Six | Two),
            (Yellow, Five | One),
            (Yellow, Four | Zero),
            (Yellow, Three | Seven),
        };

        private static readonly (StageKitTalker.CommandId, byte)[] SmallPatternList2 =
        {
            (Blue, Zero),
            (Blue, One),
            (Blue, Two),
            (Blue, Three),
            (Blue, Four),
            (Blue, None),
            (Blue, None),
        };

        private static readonly (StageKitTalker.CommandId, byte)[] SmallPatternList3 =
        {
            (Green, None),
            (Green, None),
            (Green, None),
            (Green, None),
            (Green, Four),
            (Green, Three),
            (Green, Two),
            (Green, One),
            (Green, Zero),
        };

        public override void Enable()
        {
            if (UdpIntake.Buffer[(int)UdpIntake.ByteIndexName.VenueSize] == (byte)UdpIntake.VenueSizeByte.Large)
            {
                UsbDeviceMonitor.SendReport(Yellow, None);
                UsbDeviceMonitor.SendReport(Blue, None);
                UsbDeviceMonitor.SendReport(Green, None);
                CuePrimitives.Add(new BeatPattern(LargePatternList1, 1f/LargePatternList1.Length)); //set
            }
            else
            {
                UsbDeviceMonitor.SendReport(Red, None);
                CuePrimitives.Add(new BeatPattern(SmallPatternList1, 1f/SmallPatternList1.Length)); //set
                CuePrimitives.Add(new BeatPattern(SmallPatternList2, 1f/SmallPatternList1.Length)); //set
                CuePrimitives.Add(new BeatPattern(SmallPatternList3, 1f/SmallPatternList3.Length)); //set
            }

            foreach (var primitive in CuePrimitives)
            {
                primitive.Enable();
            }
        }
    }

    public class Frenzy : StageKitLightingCue
    {
        // Red off blue yellow
        private static readonly (StageKitTalker.CommandId, byte)[] LargePatternList1 =
        {
            (Red, All),
            (Red, None),
            (Red, None),
            (Red, None),
        };

        private static readonly (StageKitTalker.CommandId, byte)[] LargePatternList2 =
        {
            (Blue, None),
            (Blue, None),
            (Blue, All),
            (Blue, None),
        };

        private static readonly (StageKitTalker.CommandId, byte)[] LargePatternList3 =
        {
            (Yellow, None),
            (Yellow, None),
            (Yellow, None),
            (Yellow, All),
        };

        // Small venue: half red, other half red, 4 green , 2 side blue, other 6 blue

        private static readonly (StageKitTalker.CommandId, byte)[] SmallPatternList1 =
        {
            (Red, None),
            (Red, All),
            (Red, Zero | Two | Four | Six),
            (Red, One | Three | Five | Seven),
        };

        private static readonly (StageKitTalker.CommandId, byte)[] SmallPatternList2 =
        {
            (Green, None),
            (Green, None),
            (Green, One | Three | Five | Seven),
            (Green, None),
        };

        private static readonly (StageKitTalker.CommandId, byte)[] SmallPatternList3 =
        {
            (Blue, All),
            (Blue, None),
            (Blue, None),
            (Blue, Six | Two),
        };

        public override void Enable()
        {
            if (UdpIntake.Buffer[(int)UdpIntake.ByteIndexName.VenueSize] == (byte)UdpIntake.VenueSizeByte.Large)
            {
                UsbDeviceMonitor.SendReport(Green, None);
                // 4 times a beats to control on and off because of the 2 different patterns on one color
                CuePrimitives.Add(new BeatPattern(LargePatternList1, 1f)); //set
                CuePrimitives.Add(new BeatPattern(LargePatternList2, 1f)); //set
                CuePrimitives.Add(new BeatPattern(LargePatternList3, 1f)); //set
            }
            else
            {
                UsbDeviceMonitor.SendReport(Yellow, None);
                // 4 times a beats to control on and off because of the 2 different patterns on one color
                CuePrimitives.Add(new BeatPattern(SmallPatternList1, 1f)); //set
                CuePrimitives.Add(new BeatPattern(SmallPatternList2, 1f)); //set
                CuePrimitives.Add(new BeatPattern(SmallPatternList3, 1f)); //set
            }

            foreach (var primitive in CuePrimitives)
            {
                primitive.Enable();
            }
        }
    }

    public class SearchLight : StageKitLightingCue
    {
        private static readonly (StageKitTalker.CommandId, byte)[] LargePatternList1 =
        {
            (Yellow, Two),
            (Yellow, Three),
            (Yellow, Four),
            (Yellow, Five),
            (Yellow, Six),
            (Yellow, Seven),
            (Yellow, Zero),
            (Yellow, One),
        };

        private static readonly (StageKitTalker.CommandId, byte)[] LargePatternList2 =
        {
            (Blue, Zero),
            (Blue, Seven),
            (Blue, Six),
            (Blue, Five),
            (Blue, Four),
            (Blue, Three),
            (Blue, Two),
            (Blue, One),
        };

        private static readonly (StageKitTalker.CommandId, byte)[] SmallPatternList1 =
        {
            (Yellow, Zero),
            (Yellow, Seven),
            (Yellow, Six),
            (Yellow, Five),
            (Yellow, Four),
            (Yellow, Three),
            (Yellow, Two),
            (Yellow, One),
        };

        private static readonly (StageKitTalker.CommandId, byte)[] SmallPatternList2 =
        {
            (Red, Zero),
            (Red, Seven),
            (Red, Six),
            (Red, Five),
            (Red, Four),
            (Red, Three),
            (Red, Two),
            (Red, One),
        };

        public override void Enable()
        {
            if (UdpIntake.Buffer[(int)UdpIntake.ByteIndexName.VenueSize] == (byte)UdpIntake.VenueSizeByte.Large)
            {
                UsbDeviceMonitor.SendReport(Red, None);
                CuePrimitives.Add(new BeatPattern(LargePatternList1, 0.5f)); //Set
                CuePrimitives.Add(new BeatPattern(LargePatternList2, 0.5f)); //Set
            }
            else
            {
                UsbDeviceMonitor.SendReport(Blue, None);
                CuePrimitives.Add(new BeatPattern(SmallPatternList1, 0.5f)); //Set
                CuePrimitives.Add(new BeatPattern(SmallPatternList2, 0.5f)); //Set
            }

            UsbDeviceMonitor.SendReport(Green, None);

            foreach (var primitive in CuePrimitives)
            {
                primitive.Enable();
            }
        }
    }

    public class Intro : StageKitLightingCue
    {
        public override void Enable()
        {
            UsbDeviceMonitor.SendReport(Yellow, None);
            UsbDeviceMonitor.SendReport(Red, None);
            UsbDeviceMonitor.SendReport(Blue, None);
            UsbDeviceMonitor.SendReport(Green, All);
        }
    }

    public class FlareFast : StageKitLightingCue
    {
        public override void Enable()
        {
            UsbDeviceMonitor.SendReport(Yellow, None);
            UsbDeviceMonitor.SendReport(Red, None);

            if (StageKitTalker.PreviousLightingCue is ManualCool or LoopCool)
            {
                UsbDeviceMonitor.SendReport(Green, All);
            }
            else
            {
                UsbDeviceMonitor.SendReport(Green, None);
            }

            UsbDeviceMonitor.SendReport(Blue, All);
        }
    }

    public class FlareSlow : StageKitLightingCue
    {
        public override void Enable()
        {
            UsbDeviceMonitor.SendReport(Blue, All);
            UsbDeviceMonitor.SendReport(Red, All);
            UsbDeviceMonitor.SendReport(Yellow, All);
            UsbDeviceMonitor.SendReport(Green, All);
        }
    }

    //Probably needs some off logic to unsubscribe from the beat event
    public class SilhouetteSpot : StageKitLightingCue
    {
        private bool _blueOn = true;
        private bool _enableBlueLedVocals;

        public override void Enable()
        {
            if (StageKitTalker.PreviousLightingCue is Intro)
            {
                CuePrimitives.Add(new ListenPattern(new (StageKitTalker.CommandId, byte)[] { (Blue, All) }, ListenTypes.RedFretDrums, true));
            }

            UdpIntake.OnBeat += HandleBeatlineEvent;
            UdpIntake.OnVocalHarmony += HandleVocalEvent;

            if (StageKitTalker.PreviousLightingCue is Dischord)
            {
                UsbDeviceMonitor.SendReport(Red, None);
                UsbDeviceMonitor.SendReport(Yellow, None);
                UsbDeviceMonitor.SendReport(Blue, One | Three | Five | Seven);
                UsbDeviceMonitor.SendReport(Green, All);

                _enableBlueLedVocals = true;
            }
            else if (StageKitTalker.PreviousLightingCue is Stomp)
            {
                // Do nothing (for the chop suey ending at least)
            }
            else
            {
                UsbDeviceMonitor.SendReport(Red, None);
                UsbDeviceMonitor.SendReport(Green, None);
                UsbDeviceMonitor.SendReport(Blue, None);
                UsbDeviceMonitor.SendReport(Yellow, None);
            }

            foreach (var primitive in CuePrimitives)
            {
                primitive.Enable();
            }

        }

        public override void KillSelf()
        {
            UdpIntake.OnBeat -= HandleBeatlineEvent;
            UdpIntake.OnVocalHarmony -= HandleVocalEvent;
            foreach (var primitive in CuePrimitives)
            {
                primitive.KillSelf();
            }
        }

        private void HandleVocalEvent(UdpIntake.VocalHarmonyBytes eventName)
        {
            //seems to active on vocal note end? More testing needing. Baffling.
            if (eventName != UdpIntake.VocalHarmonyBytes.None) return;
            if (!_enableBlueLedVocals) return;

            if (_blueOn)
            {
                UsbDeviceMonitor.SendReport(Blue, None);
                _blueOn = false;
            }
            else
            {
                UsbDeviceMonitor.SendReport(Blue, One | Three | Five | Seven);
                _blueOn = true;
            }

            _enableBlueLedVocals = false;
        }

        private void HandleBeatlineEvent(UdpIntake.BeatByte eventName)
        {
            if (eventName != UdpIntake.BeatByte.Measure || StageKitTalker.PreviousLightingCue is not Dischord) return;
            if (StageKitTalker.PreviousLightingCue is not Dischord) return;
            _enableBlueLedVocals = true;
        }
    }

    public class Silhouettes : StageKitLightingCue
    {
        public override void Enable()
        {
            UsbDeviceMonitor.SendReport(Green, All);
            UsbDeviceMonitor.SendReport(Yellow, None);
            UsbDeviceMonitor.SendReport(Blue, None);
            UsbDeviceMonitor.SendReport(Red, None);
        }
    }

    public class Blackout : StageKitLightingCue
    {
        public override void Enable()
        {
            UsbDeviceMonitor.SendReport(Green, None);
            UsbDeviceMonitor.SendReport(Yellow, None);
            UsbDeviceMonitor.SendReport(Blue, None);
            UsbDeviceMonitor.SendReport(Red, None);
        }
    }

    public class ManualWarm : StageKitLightingCue
    {
        private static readonly (StageKitTalker.CommandId, byte)[] PatternList1 =
        {
            (Red, Zero | Four),
            (Red, One | Five),
            (Red, Two | Six),
            (Red, Three | Seven),
        };

        private static readonly (StageKitTalker.CommandId, byte)[] PatternList2 =
        {
            (Yellow, Two),
            (Yellow, One),
            (Yellow, Zero),
            (Yellow, Seven),
            (Yellow, Six),
            (Yellow, Five),
            (Yellow, Four),
            (Yellow, Three),
        };

        public override void Enable()
        {
            UsbDeviceMonitor.SendReport(Green, None);
            UsbDeviceMonitor.SendReport(Blue, None);
            CuePrimitives.Add(new BeatPattern(PatternList1, 1f/PatternList1.Length)); //Set
            CuePrimitives.Add(new BeatPattern(PatternList2, 1f/PatternList2.Length)); //Set
            // I thought the Manuals listens to the next but it doesn't seem to. I'll save this for funky fresh mode
            // new ListenPattern(new List<(int, byte)>(), StageKitLightingPrimitives.ListenTypes.Next);

            foreach (var primitive in CuePrimitives)
            {
                primitive.Enable();
            }
        }
    }

    public class ManualCool : StageKitLightingCue
    {
        private static readonly (StageKitTalker.CommandId, byte)[] PatternList1 =
        {
            (Blue, Zero | Four),
            (Blue, One | Five),
            (Blue, Two | Six),
            (Blue, Three | Seven),
        };

        private static readonly (StageKitTalker.CommandId, byte)[] PatternList2 =
        {
            (Green, Two),
            (Green, One),
            (Green, Zero),
            (Green, Seven),
            (Green, Six),
            (Green, Five),
            (Green, Four),
            (Green, Three),
        };

        public override void Enable()
        {
            CuePrimitives.Add(new BeatPattern(PatternList1, 1f/PatternList1.Length)); //Set
            CuePrimitives.Add(new BeatPattern(PatternList2, 1f/PatternList2.Length)); //Set
            UsbDeviceMonitor.SendReport(Yellow, None);
            UsbDeviceMonitor.SendReport(Red, None);

            foreach (var primitive in CuePrimitives)
            {
                primitive.Enable();
            }
        }
    }

    public class Stomp : StageKitLightingCue
    {
        private bool _anythingOn;

        public override void Enable()
        {
            UdpIntake.OnKeyFrame += HandleKeyFrameEvent;

            if (UdpIntake.Buffer[(int)UdpIntake.ByteIndexName.VenueSize] == (byte)UdpIntake.VenueSizeByte.Large)
            {
                UsbDeviceMonitor.SendReport(Blue, All);
            }
            else
            {
                UsbDeviceMonitor.SendReport(Blue, None);
            }

            UsbDeviceMonitor.SendReport(Red, All);
            UsbDeviceMonitor.SendReport(Green, All);
            UsbDeviceMonitor.SendReport(Yellow, All);

            _anythingOn = true;

        }
        public override void KillSelf()
        {
            UdpIntake.OnKeyFrame -= HandleKeyFrameEvent;
        }

        private void HandleKeyFrameEvent(UdpIntake.KeyFrameByte eventName)
        {
            if (eventName != UdpIntake.KeyFrameByte.KeyframeNext) return;
            if (_anythingOn)
            {
                UsbDeviceMonitor.SendReport(Red, None);
                UsbDeviceMonitor.SendReport(Green, None);
                UsbDeviceMonitor.SendReport(Blue, None);
                UsbDeviceMonitor.SendReport(Yellow, None);
            }
            else
            {
                if (UdpIntake.Buffer[(int)UdpIntake.ByteIndexName.VenueSize] == (byte)UdpIntake.VenueSizeByte.Large)
                {
                    UsbDeviceMonitor.SendReport(Blue, All);
                }
                else
                {
                    UsbDeviceMonitor.SendReport(Blue, None);
                }

                UsbDeviceMonitor.SendReport(Red, All);
                UsbDeviceMonitor.SendReport(Green, All);
                UsbDeviceMonitor.SendReport(Yellow, All);
            }

            _anythingOn = !_anythingOn;
        }
    }

    //Probably needs some off logic to unsubscribe from the beat event
    public class Dischord : StageKitLightingCue
    {
        private bool _greenIsSpinning;
        private bool _blueOnTwo = true;
        private StageKitLighting _greenPattern;
        private BeatPattern _blueFour;
        private BeatPattern _blueTwo;

        private static readonly (StageKitTalker.CommandId, byte)[] PatternList1 =
        {
            (Yellow, Zero),
            (Yellow, One),
            (Yellow, Two),
            (Yellow, Three),
            (Yellow, Four),
            (Yellow, Five),
            (Yellow, Six),
            (Yellow, Seven),
        };
        private static readonly (StageKitTalker.CommandId, byte)[] PatternList2 =
        {
            (Green, Zero),
            (Green, Seven),
            (Green, Six),
            (Green, Five),
            (Green, Four),
            (Green, Three),
            (Green, Two),
            (Green, One),
        };

        private static readonly (StageKitTalker.CommandId, byte)[] BlueFourPattern =
        {
            (Blue, None),
            (Blue, Zero | Two | Four | Six),
        };

        private static readonly (StageKitTalker.CommandId, byte)[] BlueTwoPattern =
        {
            (Blue, None),
            (Blue, Two | Six),
        };

        public override void Enable()
        {
            _greenIsSpinning = true;
            _greenPattern = new BeatPattern(PatternList2, 0.5f); //set
            _blueFour = new BeatPattern(BlueFourPattern, 1f, false); //?
            _blueTwo = new BeatPattern(BlueTwoPattern, 1f, false); //?
            CuePrimitives.Add(new ListenPattern(PatternList1, ListenTypes.MajorBeat | ListenTypes.MinorBeat));
            CuePrimitives.Add(new ListenPattern(new (StageKitTalker.CommandId, byte)[] { (Red, All) }, ListenTypes.RedFretDrums, true));
            CuePrimitives.Add(_blueTwo);
            CuePrimitives.Add(_blueFour);
            CuePrimitives.Add(_greenPattern);

            UdpIntake.OnBeat += HandleBeatlineEvent;
            UdpIntake.OnKeyFrame += HandleKeyFrameEvent;

            UsbDeviceMonitor.SendReport(Red, None);
            UsbDeviceMonitor.SendReport(Blue, Two | Six);

            // Don't want to enable all, that turns on both blue patterns.
            CuePrimitives[0].Enable();
            CuePrimitives[1].Enable();
            _blueTwo.Enable();
            _greenPattern.Enable();

        }

        public override void KillSelf()
        {
            UdpIntake.OnBeat -= HandleBeatlineEvent;
            UdpIntake.OnKeyFrame -= HandleKeyFrameEvent;
        }

        private void HandleKeyFrameEvent(UdpIntake.KeyFrameByte eventName)
        {
            if (eventName != UdpIntake.KeyFrameByte.KeyframeNext)
            {
                return;
            }

            if (_blueOnTwo)
            {
                _blueTwo.KillSelf();
                _blueFour.Enable();
                _blueOnTwo = false;
            }
            else
            {
                _blueFour.KillSelf();
                _blueTwo.Enable();
                _blueOnTwo = true;
            }
        }

        private void HandleBeatlineEvent(UdpIntake.BeatByte eventName)
        {
            if (UdpIntake.Buffer[(int)UdpIntake.ByteIndexName.VenueSize] == (byte)UdpIntake.VenueSizeByte.Large || eventName != UdpIntake.BeatByte.Measure) return;
            if (_greenIsSpinning)
            {
                _greenPattern.KillSelf();

                UsbDeviceMonitor.SendReport(Green, All);
            }
            else
            {
                _greenPattern.Enable();
            }

            _greenIsSpinning = !_greenIsSpinning;
        }
    }

    public class Default : StageKitLightingCue
    {
        private static readonly (StageKitTalker.CommandId, byte)[] LargePatternList1 =
        {
            (Blue, All),
            (Red, All),
        };

        private static readonly (StageKitTalker.CommandId, byte)[] SmallPatternList1 =
        {
            (Red, All),
            (Blue, All),
        };

        public override void Enable()
        {
            UsbDeviceMonitor.SendReport(Green, None);

            if (UdpIntake.Buffer[(int)UdpIntake.ByteIndexName.VenueSize] == (byte)UdpIntake.VenueSizeByte.Large)
            {
                CuePrimitives.Add(new ListenPattern(LargePatternList1, ListenTypes.Next));
            }
            else
            {
                UsbDeviceMonitor.SendReport(Yellow, None);
                CuePrimitives.Add(new ListenPattern(new (StageKitTalker.CommandId, byte)[] { (Yellow, All) }, ListenTypes.RedFretDrums, true, true));
                CuePrimitives.Add(new ListenPattern(SmallPatternList1, ListenTypes.Next));
            }

            foreach (var primitive in CuePrimitives)
            {
                primitive.Enable();
            }
        }
    }

    public class MenuLighting : StageKitLightingCue
    {
        private static readonly (StageKitTalker.CommandId, byte)[] PatternList1 =
        {
            (Blue, Zero),
            (Blue, One),
            (Blue, Two),
            (Blue, Three),
            (Blue, Four),
            (Blue, Five),
            (Blue, Six),
            (Blue, Seven),
        };

        public override void Enable()
        {
            UsbDeviceMonitor.SendReport(Green, None);
            UsbDeviceMonitor.SendReport(Red, None);
            UsbDeviceMonitor.SendReport(Yellow, None);
            CuePrimitives.Add(new TimedPattern(PatternList1, 2f));
            foreach (var primitive in CuePrimitives)
            {
                primitive.Enable();
            }
        }
    }

    public class ScoreLighting : StageKitLightingCue
    {
        private static readonly (StageKitTalker.CommandId, byte)[] LargePatternList1 =
        {
            (Red, Six | Two),
            (Red, One | Five),
            (Red, Zero | Four),
            (Red, Seven | Three),
        };

        private static readonly (StageKitTalker.CommandId, byte)[] SmallPatternList1 =
        {
            (Blue, Zero),
            (Blue, Seven),
            (Blue, Six),
            (Blue, Five),
            (Blue, Four),
            (Blue, Three),
            (Blue, Two),
            (Blue, One),
        };

        private static readonly (StageKitTalker.CommandId, byte)[] PatternList2 =
        {
            (Yellow, Six | Two),
            (Yellow, Seven | Three),
            (Yellow, Zero | Four),
            (Yellow, One | Five),
        };

        public override void Enable()
        {
            UsbDeviceMonitor.SendReport(Green, None);

            if (UdpIntake.Buffer[(int)UdpIntake.ByteIndexName.VenueSize] == (byte)UdpIntake.VenueSizeByte.Large)
            {
                UsbDeviceMonitor.SendReport(Blue, None);
                CuePrimitives.Add(new TimedPattern(LargePatternList1, 1f));
            }
            else
            {
                UsbDeviceMonitor.SendReport(Red, None);
                CuePrimitives.Add(new TimedPattern(SmallPatternList1, 1f));
            }

            CuePrimitives.Add(new TimedPattern(PatternList2, 2f));

            foreach (var primitive in CuePrimitives)
            {
                primitive.Enable();
            }
        }
    }
}
