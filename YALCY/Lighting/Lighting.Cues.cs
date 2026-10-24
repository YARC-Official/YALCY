using System;
using System.Collections.Generic;

namespace YALCY.Lighting;

    // Parent of primitives
    // Grandparent of cues
    public abstract class LightingPattern
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
    public abstract class LightingCue : LightingPattern
    {
        protected const LightingCommand Blue = LightingCommand.BlueLeds;
        protected const LightingCommand Green = LightingCommand.GreenLeds;
        protected const LightingCommand Yellow = LightingCommand.YellowLeds;
        protected const LightingCommand Red = LightingCommand.RedLeds;

        public List<LightingPattern> CuePrimitives = new();
    }
    public class NoCue : LightingCue
    {
        public override void Enable()
        {
            LightingOutput.Publish(LightingCommand.DisableAll, None);
        }
    }

    public class BigRockEnding : LightingCue
    {
        private static readonly (LightingCommand, byte)[] PatternList1 =
        {
            (Red, All),
            (Red, None),
            (Red, None),
            (Red, None),
        };
        private static readonly (LightingCommand, byte)[] PatternList2 =
        {
            (Yellow, None),
            (Yellow, None),
            (Yellow, All),
            (Yellow, None),
        };
        private static readonly (LightingCommand, byte)[] PatternList3 =
        {
            (Green, None),
            (Green, All),
            (Green, None),
            (Green, None),
        };
        private static readonly (LightingCommand, byte)[] PatternList4 =
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
            LightingOutput.Publish(Blue, All);
            LightingOutput.Publish(Red, All);
            LightingOutput.Publish(Yellow, All);
            LightingOutput.Publish(Green, All);

            foreach (var primitive in CuePrimitives)
            {
                primitive.Enable();
            }
        }
    }

    public class LoopWarm : LightingCue
    {
        private static readonly (LightingCommand, byte)[] PatternList1 =
        {
            (Red, Zero | Four),
            (Red, One | Five),
            (Red, Two | Six),
            (Red, Three | Seven),
        };

        private static readonly (LightingCommand, byte)[] PatternList2 =
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
            LightingOutput.Publish(Green, None);
            LightingOutput.Publish(Blue, None);
            CuePrimitives.Add(new BeatPattern(PatternList1, 1f/PatternList1.Length)); //Set
            CuePrimitives.Add(new BeatPattern(PatternList2, 1f/PatternList2.Length)); //Set

            foreach (var primitive in CuePrimitives)
            {
                primitive.Enable();
            }
        }
    }

    public class LoopCool : LightingCue
    {
        private static readonly (LightingCommand, byte)[] PatternList1 =
        {
            (Blue, Zero | Four),
            (Blue, One | Five),
            (Blue, Two | Six),
            (Blue, Three | Seven),
        };

        private static readonly (LightingCommand, byte)[] PatternList2 =
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
            LightingOutput.Publish(Yellow, None);
            LightingOutput.Publish(Red, None);
            CuePrimitives.Add(new BeatPattern(PatternList1, 1f/PatternList1.Length)); //Set
            CuePrimitives.Add(new BeatPattern(PatternList2, 1f/PatternList2.Length)); //Set

            foreach (var primitive in CuePrimitives)
            {
                primitive.Enable();
            }
        }
    }

    public class Harmony : LightingCue
    {
        private static readonly (LightingCommand, byte)[] LargePatternList1 =
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

        private static readonly (LightingCommand, byte)[] LargePatternList2 =
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

        private static readonly (LightingCommand, byte)[] SmallPatternList1 =
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

        private static readonly (LightingCommand, byte)[] SmallPatternList2 =
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
            if (Udp.UdpIntake.Venue.Value == (byte)Udp.UdpIntake.VenueSizeByte.Large)
            {
                LightingOutput.Publish(Blue, None);
                LightingOutput.Publish(Green, None);
                CuePrimitives.Add(new BeatPattern(LargePatternList1, 1f/LargePatternList1.Length)); //set
                CuePrimitives.Add(new BeatPattern(LargePatternList2, 1f/LargePatternList2.Length)); //set
            }
            else
            {
                LightingOutput.Publish(Red, None);
                LightingOutput.Publish(Yellow, None);
                CuePrimitives.Add(new BeatPattern(SmallPatternList1, 1f/SmallPatternList1.Length)); //set
                CuePrimitives.Add(new BeatPattern(SmallPatternList2, 1f/SmallPatternList1.Length)); //set
            }

            foreach (var primitive in CuePrimitives)
            {
                primitive.Enable();
            }
        }
    }

    public class Sweep : LightingCue
    {
        private static readonly (LightingCommand, byte)[] LargePatternList1 =
        {
            (Red, Six | Two),
            (Red, Five | One),
            (Red, Four | Zero),
            (Red, Three | Seven),
        };

        private static readonly (LightingCommand, byte)[] SmallPatternList1 =
        {
            (Yellow, Six | Two),
            (Yellow, Five | One),
            (Yellow, Four | Zero),
            (Yellow, Three | Seven),
        };

        private static readonly (LightingCommand, byte)[] SmallPatternList2 =
        {
            (Blue, Zero),
            (Blue, One),
            (Blue, Two),
            (Blue, Three),
            (Blue, Four),
            (Blue, None),
            (Blue, None),
        };

        private static readonly (LightingCommand, byte)[] SmallPatternList3 =
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
            if (Udp.UdpIntake.Venue.Value == (byte)Udp.UdpIntake.VenueSizeByte.Large)
            {
                LightingOutput.Publish(Yellow, None);
                LightingOutput.Publish(Blue, None);
                LightingOutput.Publish(Green, None);
                CuePrimitives.Add(new BeatPattern(LargePatternList1, 1f/LargePatternList1.Length)); //set
            }
            else
            {
                LightingOutput.Publish(Red, None);
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

    public class Frenzy : LightingCue
    {
        // Red off blue yellow
        private static readonly (LightingCommand, byte)[] LargePatternList1 =
        {
            (Red, All),
            (Red, None),
            (Red, None),
            (Red, None),
        };

        private static readonly (LightingCommand, byte)[] LargePatternList2 =
        {
            (Blue, None),
            (Blue, None),
            (Blue, All),
            (Blue, None),
        };

        private static readonly (LightingCommand, byte)[] LargePatternList3 =
        {
            (Yellow, None),
            (Yellow, None),
            (Yellow, None),
            (Yellow, All),
        };

        // Small venue: half red, other half red, 4 green , 2 side blue, other 6 blue

        private static readonly (LightingCommand, byte)[] SmallPatternList1 =
        {
            (Red, None),
            (Red, All),
            (Red, Zero | Two | Four | Six),
            (Red, One | Three | Five | Seven),
        };

        private static readonly (LightingCommand, byte)[] SmallPatternList2 =
        {
            (Green, None),
            (Green, None),
            (Green, One | Three | Five | Seven),
            (Green, None),
        };

        private static readonly (LightingCommand, byte)[] SmallPatternList3 =
        {
            (Blue, All),
            (Blue, None),
            (Blue, None),
            (Blue, Six | Two),
        };

        public override void Enable()
        {
            if (Udp.UdpIntake.Venue.Value == (byte)Udp.UdpIntake.VenueSizeByte.Large)
            {
                LightingOutput.Publish(Green, None);
                // 4 times a beats to control on and off because of the 2 different patterns on one color
                CuePrimitives.Add(new BeatPattern(LargePatternList1, 1f)); //set
                CuePrimitives.Add(new BeatPattern(LargePatternList2, 1f)); //set
                CuePrimitives.Add(new BeatPattern(LargePatternList3, 1f)); //set
            }
            else
            {
                LightingOutput.Publish(Yellow, None);
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

    public class SearchLight : LightingCue
    {
        private static readonly (LightingCommand, byte)[] LargePatternList1 =
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

        private static readonly (LightingCommand, byte)[] LargePatternList2 =
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

        private static readonly (LightingCommand, byte)[] SmallPatternList1 =
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

        private static readonly (LightingCommand, byte)[] SmallPatternList2 =
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
            if (Udp.UdpIntake.Venue.Value == (byte)Udp.UdpIntake.VenueSizeByte.Large)
            {
                LightingOutput.Publish(Red, None);
                CuePrimitives.Add(new BeatPattern(LargePatternList1, 0.5f)); //Set
                CuePrimitives.Add(new BeatPattern(LargePatternList2, 0.5f)); //Set
            }
            else
            {
                LightingOutput.Publish(Blue, None);
                CuePrimitives.Add(new BeatPattern(SmallPatternList1, 0.5f)); //Set
                CuePrimitives.Add(new BeatPattern(SmallPatternList2, 0.5f)); //Set
            }

            LightingOutput.Publish(Green, None);

            foreach (var primitive in CuePrimitives)
            {
                primitive.Enable();
            }
        }
    }

    public class Intro : LightingCue
    {
        public override void Enable()
        {
            LightingOutput.Publish(Yellow, None);
            LightingOutput.Publish(Red, None);
            LightingOutput.Publish(Blue, None);
            LightingOutput.Publish(Green, All);
        }
    }

    public class FlareFast : LightingCue
    {
        public override void Enable()
        {
            LightingOutput.Publish(Yellow, None);
            LightingOutput.Publish(Red, None);

            if (LightingEngine.PreviousLightingCue is ManualCool or LoopCool)
            {
                LightingOutput.Publish(Green, All);
            }
            else
            {
                LightingOutput.Publish(Green, None);
            }

            LightingOutput.Publish(Blue, All);
        }
    }

    public class FlareSlow : LightingCue
    {
        public override void Enable()
        {
            LightingOutput.Publish(Blue, All);
            LightingOutput.Publish(Red, All);
            LightingOutput.Publish(Yellow, All);
            LightingOutput.Publish(Green, All);
        }
    }

    //Probably needs some off logic to unsubscribe from the beat event
    public class SilhouetteSpot : LightingCue
    {
        private bool _blueOn = true;
        private bool _enableBlueLedVocals;

        public override void Enable()
        {
            if (LightingEngine.PreviousLightingCue is Intro)
            {
                CuePrimitives.Add(new ListenPattern(new (LightingCommand, byte)[] { (Blue, All) }, ListenTypes.RedFretDrums, true));
            }

            Udp.UdpIntake.OnBeat += HandleBeatlineEvent;
            Udp.UdpIntake.OnVocalHarmony += HandleVocalEvent;

            if (LightingEngine.PreviousLightingCue is Dischord)
            {
                LightingOutput.Publish(Red, None);
                LightingOutput.Publish(Yellow, None);
                LightingOutput.Publish(Blue, One | Three | Five | Seven);
                LightingOutput.Publish(Green, All);

                _enableBlueLedVocals = true;
            }
            else if (LightingEngine.PreviousLightingCue is Stomp)
            {
                // Do nothing (for the chop suey ending at least)
            }
            else
            {
                LightingOutput.Publish(Red, None);
                LightingOutput.Publish(Green, None);
                LightingOutput.Publish(Blue, None);
                LightingOutput.Publish(Yellow, None);
            }

            foreach (var primitive in CuePrimitives)
            {
                primitive.Enable();
            }

        }

        public override void KillSelf()
        {
            Udp.UdpIntake.OnBeat -= HandleBeatlineEvent;
            Udp.UdpIntake.OnVocalHarmony -= HandleVocalEvent;
            foreach (var primitive in CuePrimitives)
            {
                primitive.KillSelf();
            }
        }

        private void HandleVocalEvent(float eventName)
        {
            //seems to active on vocal note end? More testing needing. Baffling.
            if (eventName != (byte)Udp.UdpIntake.VocalHarmonyBytes.None) return;
            if (!_enableBlueLedVocals) return;

            if (_blueOn)
            {
                LightingOutput.Publish(Blue, None);
                _blueOn = false;
            }
            else
            {
                LightingOutput.Publish(Blue, One | Three | Five | Seven);
                _blueOn = true;
            }

            _enableBlueLedVocals = false;
        }

        private void HandleBeatlineEvent(byte eventName)
        {
            if (eventName != (byte)Udp.UdpIntake.BeatByte.Measure || LightingEngine.PreviousLightingCue is not Dischord) return;
            _enableBlueLedVocals = true;
        }
    }

    public class Silhouettes : LightingCue
    {
        public override void Enable()
        {
            LightingOutput.Publish(Green, All);
            LightingOutput.Publish(Yellow, None);
            LightingOutput.Publish(Blue, None);
            LightingOutput.Publish(Red, None);
        }
    }

    public class Blackout : LightingCue
    {
        public override void Enable()
        {
            LightingOutput.Publish(Green, None);
            LightingOutput.Publish(Yellow, None);
            LightingOutput.Publish(Blue, None);
            LightingOutput.Publish(Red, None);
        }
    }

    public class ManualWarm : LightingCue
    {
        private static readonly (LightingCommand, byte)[] PatternList1 =
        {
            (Red, Zero | Four),
            (Red, One | Five),
            (Red, Two | Six),
            (Red, Three | Seven),
        };

        private static readonly (LightingCommand, byte)[] PatternList2 =
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
            LightingOutput.Publish(Green, None);
            LightingOutput.Publish(Blue, None);
            CuePrimitives.Add(new BeatPattern(PatternList1, 1f/PatternList1.Length)); //Set
            CuePrimitives.Add(new BeatPattern(PatternList2, 1f/PatternList2.Length)); //Set
            // I thought the Manuals listens to the next but it doesn't seem to. I'll save this for funky fresh mode
            // new ListenPattern(new List<(int, byte)>(), LightingPatternPrimitives.ListenTypes.Next);

            foreach (var primitive in CuePrimitives)
            {
                primitive.Enable();
            }
        }
    }

    public class ManualCool : LightingCue
    {
        private static readonly (LightingCommand, byte)[] PatternList1 =
        {
            (Blue, Zero | Four),
            (Blue, One | Five),
            (Blue, Two | Six),
            (Blue, Three | Seven),
        };

        private static readonly (LightingCommand, byte)[] PatternList2 =
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
            LightingOutput.Publish(Yellow, None);
            LightingOutput.Publish(Red, None);

            foreach (var primitive in CuePrimitives)
            {
                primitive.Enable();
            }
        }
    }

    public class Stomp : LightingCue
    {
        private bool _anythingOn;

        public override void Enable()
        {
            Udp.UdpIntake.OnKeyFrame += HandleKeyFrameEvent;

            if (Udp.UdpIntake.Venue.Value == (byte)Udp.UdpIntake.VenueSizeByte.Large)
            {
                LightingOutput.Publish(Blue, All);
            }
            else
            {
                LightingOutput.Publish(Blue, None);
            }

            LightingOutput.Publish(Red, All);
            LightingOutput.Publish(Green, All);
            LightingOutput.Publish(Yellow, All);

            _anythingOn = true;

        }
        public override void KillSelf()
        {
            Udp.UdpIntake.OnKeyFrame -= HandleKeyFrameEvent;
        }

        private void HandleKeyFrameEvent(byte eventName)
        {
            if (eventName != (byte)Udp.UdpIntake.KeyFrameByte.KeyframeNext) return;
            if (_anythingOn)
            {
                LightingOutput.Publish(Red, None);
                LightingOutput.Publish(Green, None);
                LightingOutput.Publish(Blue, None);
                LightingOutput.Publish(Yellow, None);
            }
            else
            {
                if (Udp.UdpIntake.Venue.Value == (byte)Udp.UdpIntake.VenueSizeByte.Large)
                {
                    LightingOutput.Publish(Blue, All);
                }
                else
                {
                    LightingOutput.Publish(Blue, None);
                }

                LightingOutput.Publish(Red, All);
                LightingOutput.Publish(Green, All);
                LightingOutput.Publish(Yellow, All);
            }

            _anythingOn = !_anythingOn;
        }
    }

    //Probably needs some off logic to unsubscribe from the beat event
    public class Dischord : LightingCue
    {
        private bool _greenIsSpinning;
        private bool _blueOnTwo = true;
        private LightingPattern _greenPattern;
        private BeatPattern _blueFour;
        private BeatPattern _blueTwo;

        private static readonly (LightingCommand, byte)[] PatternList1 =
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
        private static readonly (LightingCommand, byte)[] PatternList2 =
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

        private static readonly (LightingCommand, byte)[] BlueFourPattern =
        {
            (Blue, None),
            (Blue, Zero | Two | Four | Six),
        };

        private static readonly (LightingCommand, byte)[] BlueTwoPattern =
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
            CuePrimitives.Add(new ListenPattern(new (LightingCommand, byte)[] { (Red, All) }, ListenTypes.RedFretDrums, true));
            CuePrimitives.Add(_blueTwo);
            CuePrimitives.Add(_blueFour);
            CuePrimitives.Add(_greenPattern);

            Udp.UdpIntake.OnBeat += HandleBeatlineEvent;
            Udp.UdpIntake.OnKeyFrame += HandleKeyFrameEvent;

            LightingOutput.Publish(Red, None);
            LightingOutput.Publish(Blue, Two | Six);

            // Don't want to enable all, that turns on both blue patterns.
            CuePrimitives[0].Enable();
            CuePrimitives[1].Enable();
            _blueTwo.Enable();
            _greenPattern.Enable();

        }

        public override void KillSelf()
        {
            Udp.UdpIntake.OnBeat -= HandleBeatlineEvent;
            Udp.UdpIntake.OnKeyFrame -= HandleKeyFrameEvent;
        }

        private void HandleKeyFrameEvent(byte eventName)
        {
            if (eventName != (byte)Udp.UdpIntake.KeyFrameByte.KeyframeNext)
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

        private void HandleBeatlineEvent(byte eventName)
        {
            if (Udp.UdpIntake.Venue.Value == (byte)Udp.UdpIntake.VenueSizeByte.Large || eventName != (byte)Udp.UdpIntake.BeatByte.Measure) return;
            if (_greenIsSpinning)
            {
                _greenPattern.KillSelf();

                LightingOutput.Publish(Green, All);
            }
            else
            {
                _greenPattern.Enable();
            }

            _greenIsSpinning = !_greenIsSpinning;
        }
    }

    public class Default : LightingCue
    {
        private static readonly (LightingCommand, byte)[] LargePatternList1 =
        {
            (Blue, All),
            (Red, All),
        };

        private static readonly (LightingCommand, byte)[] SmallPatternList1 =
        {
            (Red, All),
            (Blue, All),
        };

        public override void Enable()
        {
            LightingOutput.Publish(Green, None);

            if (Udp.UdpIntake.Venue.Value == (byte)Udp.UdpIntake.VenueSizeByte.Large)
            {
                CuePrimitives.Add(new ListenPattern(LargePatternList1, ListenTypes.Next));
            }
            else
            {
                LightingOutput.Publish(Yellow, None);
                CuePrimitives.Add(new ListenPattern(new (LightingCommand, byte)[] { (Yellow, All) }, ListenTypes.RedFretDrums, true, true));
                CuePrimitives.Add(new ListenPattern(SmallPatternList1, ListenTypes.Next));
            }

            foreach (var primitive in CuePrimitives)
            {
                primitive.Enable();
            }
        }
    }

    public class MenuLighting : LightingCue
    {
        private static readonly (LightingCommand, byte)[] PatternList1 =
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
            LightingOutput.Publish(Green, None);
            LightingOutput.Publish(Red, None);
            LightingOutput.Publish(Yellow, None);
            CuePrimitives.Add(new TimedPattern(PatternList1, 2f));
            foreach (var primitive in CuePrimitives)
            {
                primitive.Enable();
            }
        }
    }

    public class ScoreLighting : LightingCue
    {
        private static readonly (LightingCommand, byte)[] LargePatternList1 =
        {
            (Red, Six | Two),
            (Red, One | Five),
            (Red, Zero | Four),
            (Red, Seven | Three),
        };

        private static readonly (LightingCommand, byte)[] SmallPatternList1 =
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

        private static readonly (LightingCommand, byte)[] PatternList2 =
        {
            (Yellow, Six | Two),
            (Yellow, Seven | Three),
            (Yellow, Zero | Four),
            (Yellow, One | Five),
        };

        public override void Enable()
        {
            LightingOutput.Publish(Green, None);

            if (Udp.UdpIntake.Venue.Value == (byte)Udp.UdpIntake.VenueSizeByte.Large)
            {
                LightingOutput.Publish(Blue, None);
                CuePrimitives.Add(new TimedPattern(LargePatternList1, 1f));
            }
            else
            {
                LightingOutput.Publish(Red, None);
                CuePrimitives.Add(new TimedPattern(SmallPatternList1, 1f));
            }

            CuePrimitives.Add(new TimedPattern(PatternList2, 2f));

            foreach (var primitive in CuePrimitives)
            {
                primitive.Enable();
            }
        }
    }
