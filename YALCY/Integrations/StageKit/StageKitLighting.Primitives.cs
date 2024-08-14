using System;
using System.Threading;
using System.Threading.Tasks;
using YALCY.Usb;

namespace YALCY.Integrations.StageKit;

    public class BeatPattern : StageKitLighting
    {
        private readonly bool _continuous;
        private int _patternIndex;
        private readonly (StageKitTalker.CommandId color, byte data)[] _patternList;
        private readonly float _cyclesPerBeat;
        private CancellationTokenSource _cancellationTokenSource;

        public BeatPattern((StageKitTalker.CommandId, byte)[] patternList, float cyclesPerBeat, bool continuous = true)
        {
            _continuous = continuous;
            _patternList = patternList;
            _cyclesPerBeat = cyclesPerBeat;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public override void Enable()
        {
            _patternIndex = 0;
            _cancellationTokenSource = new CancellationTokenSource();
            Task.Run(async () =>
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    UsbDeviceMonitor.SendReport(_patternList[_patternIndex].color, _patternList[_patternIndex].data);
                    _patternIndex++;

                    // Some beat patterns are not continuous (single fire), so we need to kill them after they've run once
                    // otherwise they pile up.
                    if (!_continuous && _patternIndex == _patternList.Length)
                    {
                        KillSelf();
                    }

                    if (_patternIndex >= _patternList.Length)
                    {
                        _patternIndex = 0;
                    }

                    var stepsPerBeats = _patternList.Length * _cyclesPerBeat;
                    var secondsPerBeat = 60f/ Udp.UdpIntake.Buffer[(int)Udp.UdpIntake.ByteIndexName.BeatsPerMinute];
                    await Task.Delay(TimeSpan.FromSeconds(  secondsPerBeat / stepsPerBeats   ), cancellationToken: _cancellationTokenSource.Token);
                }
            });
        }

        public override void KillSelf()
        {
            _cancellationTokenSource?.Cancel();
        }
    }

    public class ListenPattern : StageKitLighting
    {
        private readonly ListenTypes _listenType;
        private int _patternIndex;
        private readonly (StageKitTalker.CommandId color, byte data)[] _patternList;
        private readonly bool _flash;
        private readonly bool _inverse;
        private bool _enabled;

        public ListenPattern((StageKitTalker.CommandId color, byte data)[] patternList, ListenTypes listenType,
            bool flash = false, bool inverse = false)
        {
            _flash = flash;
            _patternList = patternList;
            _listenType = listenType;
            _inverse = inverse;
        }

        public override void Enable()
        {
            Udp.UdpIntake.OnBeat += HandleBeatlineEvent;
            Udp.UdpIntake.OnKeyFrame += HandleKeyFrameEvent;
            Udp.UdpIntake.OnDrum += HandleDrumEvent;

            _patternIndex = 0;
            _enabled = true;
            if (!_inverse) return;
            UsbDeviceMonitor.SendReport(_patternList[_patternIndex].color, _patternList[_patternIndex].data);
            _patternIndex++;

            if (_patternIndex >= _patternList.Length)
            {
                _patternIndex = 0;
            }
        }

        private void HandleBeatlineEvent(Udp.UdpIntake.BeatByte eventName)
        {
            if (!_enabled)
            {
                return;
            }

            if (((_listenType & ListenTypes.MajorBeat) == 0 || eventName != Udp.UdpIntake.BeatByte.Measure) &&
                ((_listenType & ListenTypes.MinorBeat) == 0 || eventName != Udp.UdpIntake.BeatByte.Strong))
            {
                return;
            }

            ProcessEvent();
        }

        private void HandleDrumEvent(Udp.UdpIntake.DrumNotesByte eventName)
        {
            if (!_enabled)
            {
                return;
            }

            if ((_listenType & ListenTypes.RedFretDrums) == 0 || (eventName &  Udp.UdpIntake.DrumNotesByte.RedDrum) == 0)
            {
                return;
            }

            ProcessEvent();
        }

        public void HandleKeyFrameEvent(Udp.UdpIntake.KeyFrameByte eventName)
        {
            if (!_enabled)
            {
                return;
            }

            if ((_listenType & ListenTypes.Next) == 0 || eventName != Udp.UdpIntake.KeyFrameByte.KeyframeNext)
            {
                return;
            }

            ProcessEvent();
        }

        private void ProcessEvent()
        {
            // This might be a bug in the official stage kit code. Instead of turning off the strobe as soon as cue
            // changes, if the cue listens for something, it only turns off the strobe on the first event of it.
            // To make that happen, strobe off would have to be here and removed from the master controller as well as
            // added to the lighting event switch case for all the non-listening cues.

            if (_inverse)
            {
                UsbDeviceMonitor.SendReport(_patternList[_patternIndex].color, None);
            }
            else
            {
                UsbDeviceMonitor.SendReport(_patternList[_patternIndex].color, _patternList[_patternIndex].data);
            }

            if (_flash)
            {
                var e = OnFlash();
            }

            _patternIndex++;

            if (_patternIndex >= _patternList.Length)
            {
                _patternIndex = 0;
            }
        }

        private async Task OnFlash()
        {
            // I wonder if this should be beat based instead of time based. like 1/2 a beat or something.
            // But a really fast song would be bad looking.
            await Task.Delay(200);
            if (_inverse)
            {
                UsbDeviceMonitor.SendReport(_patternList[_patternIndex].color,
                    _patternList[_patternIndex].data);
            }
            else
            {
                UsbDeviceMonitor.SendReport(_patternList[_patternIndex].color, None);
            }
        }

        public override void KillSelf()
        {
            _enabled = false;
            Udp.UdpIntake.OnBeat -= HandleBeatlineEvent;
        }
    }

    public class TimedPattern : StageKitLighting
    {
        private readonly float _seconds;
        private int _patternIndex;
        private readonly (StageKitTalker.CommandId color, byte data)[] _patternList;
        private CancellationTokenSource _cancellationTokenSource;

        public TimedPattern((StageKitTalker.CommandId, byte)[] patternList, float seconds)
        {
            // Token only for timed events
            _cancellationTokenSource = new CancellationTokenSource();
            _seconds = seconds;
            _patternList = patternList;
        }

        public override void Enable()
        {
            _patternIndex = 0;
            _cancellationTokenSource = new CancellationTokenSource();
            var e = TimedCircleCoroutine(_cancellationTokenSource.Token);
        }

        private async Task TimedCircleCoroutine(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                UsbDeviceMonitor.SendReport(_patternList[_patternIndex].color,
                    _patternList[_patternIndex].data);

                await Task.Delay(TimeSpan.FromSeconds(_seconds / _patternList.Length), cancellationToken: cancellationToken);

                _patternIndex++;

                if (_patternIndex >= _patternList.Length)
                {
                    _patternIndex = 0;
                }
            }
        }

        public override void KillSelf()
        {
            _cancellationTokenSource?.Cancel();
        }
    }
