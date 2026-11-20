using System;
using System.Collections.Generic;

namespace YALCY.Integrations;

// YARG sends beats and keyframes as pulses that last one packet (88 packets per second), shorter than an
// output frame (44 per second), so a frame often went out after the value had already gone back to idle.
// This queues each pulse and shows it for a few frames, with idle frames before the next one so pulses that
// arrive back to back stay separate.
public sealed class PulseHold
{
    private const int HoldFrames = 2;
    private const int GapFrames = 2;

    private readonly object _lock = new();
    private readonly Queue<byte> _pending = new();
    private readonly Func<byte, bool> _isPulse;
    private readonly int _maxPending;

    private byte _lastInput;
    private byte _idle;
    private byte _value;
    private int _holdLeft;
    private int _gapLeft;

    // isPulse picks out pulse values; anything else is the idle value shown between pulses.
    // Past maxPending, the oldest waiting pulse is dropped, so a burst can't build up a backlog.
    public PulseHold(Func<byte, bool> isPulse, int maxPending)
    {
        _isPulse = isPulse;
        _maxPending = maxPending;
    }

    // Call for every received packet
    public void Add(byte input)
    {
        lock (_lock)
        {
            if (!_isPulse(input))
            {
                _idle = input;
            }
            else if (input != _lastInput)
            {
                if (_pending.Count >= _maxPending)
                {
                    _pending.Dequeue();
                }

                _pending.Enqueue(input);
            }

            _lastInput = input;
        }
    }

    // Call once per output frame; returns the value to send
    public byte NextFrame()
    {
        lock (_lock)
        {
            if (_holdLeft > 0)
            {
                _holdLeft--;
                return _value;
            }

            if (_gapLeft > 0)
            {
                _gapLeft--;
                return _idle;
            }

            if (_pending.Count == 0)
            {
                return _idle;
            }

            _value = _pending.Dequeue();
            _holdLeft = HoldFrames - 1;
            _gapLeft = GapFrames;
            return _value;
        }
    }

    // Drop waiting pulses and stop the current one, e.g. while output is suppressed
    public void Clear()
    {
        lock (_lock)
        {
            _pending.Clear();
            _holdLeft = 0;
            _gapLeft = 0;
        }
    }
}
