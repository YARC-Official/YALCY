using System;

namespace YALCY.Safety;

internal enum LightingSafetyState
{
    WaitingForYarg,
    Receiving,
    TimedOut,
    ManualBlackout
}

internal interface ILightingSafetyActions
{
    void EnterSafeMode();
    void ExitSafeMode(bool replayState);
}

internal sealed class LightingSafetyController
{
    private readonly object _stateLock = new();
    private readonly TimeProvider _timeProvider;
    private readonly ILightingSafetyActions _actions;
    private long _lastValidPacketTimestamp;
    private bool _hasReceivedValidPacket;
    private bool _automaticBlackout;
    private bool _manualBlackout;
    private TimeSpan _timeout;
    private LightingSafetyState _state = LightingSafetyState.WaitingForYarg;

    public LightingSafetyController(
        TimeProvider timeProvider,
        ILightingSafetyActions actions,
        TimeSpan timeout)
    {
        _timeProvider = timeProvider;
        _actions = actions;
        Timeout = timeout;
    }

    public event Action<LightingSafetyState>? StateChanged;

    public LightingSafetyState State
    {
        get
        {
            lock (_stateLock)
            {
                return _state;
            }
        }
    }

    public bool IsOutputSuppressed
    {
        get
        {
            lock (_stateLock)
            {
                return _automaticBlackout || _manualBlackout;
            }
        }
    }

    public bool IsManualBlackout
    {
        get
        {
            lock (_stateLock)
            {
                return _manualBlackout;
            }
        }
    }

    public TimeSpan Timeout
    {
        get
        {
            lock (_stateLock)
            {
                return _timeout;
            }
        }
        set
        {
            lock (_stateLock)
            {
                _timeout = TimeSpan.FromSeconds(Math.Clamp(value.TotalSeconds, 1, 30));
            }
        }
    }

    public void NotifyValidPacket()
    {
        LightingSafetyState state;
        var resumeOutput = false;
        var stateChanged = false;

        lock (_stateLock)
        {
            var wasAutomaticallySuppressed = _automaticBlackout && !_manualBlackout;
            _lastValidPacketTimestamp = _timeProvider.GetTimestamp();
            _hasReceivedValidPacket = true;
            _automaticBlackout = false;

            if (_manualBlackout)
            {
                state = LightingSafetyState.ManualBlackout;
            }
            else
            {
                state = LightingSafetyState.Receiving;
                resumeOutput = wasAutomaticallySuppressed;
            }

            stateChanged = SetStateLocked(state);
        }

        if (resumeOutput)
        {
            _actions.ExitSafeMode(replayState: true);
        }

        if (stateChanged)
        {
            RaiseStateChanged(state);
        }
    }

    public void CheckForTimeout()
    {
        LightingSafetyState? state = null;
        var enterSafeMode = false;

        lock (_stateLock)
        {
            if (!_hasReceivedValidPacket || _automaticBlackout ||
                _timeProvider.GetElapsedTime(_lastValidPacketTimestamp) < _timeout)
            {
                return;
            }

            enterSafeMode = !_manualBlackout;
            _automaticBlackout = true;
            state = _manualBlackout
                ? LightingSafetyState.ManualBlackout
                : LightingSafetyState.TimedOut;
            SetStateLocked(state.Value);
        }

        if (enterSafeMode)
        {
            _actions.EnterSafeMode();
        }

        RaiseStateChanged(state.Value);
    }

    public void NotifyStreamStopped()
    {
        LightingSafetyState? state = null;
        var enterSafeMode = false;

        lock (_stateLock)
        {
            if (!_hasReceivedValidPacket || _automaticBlackout)
            {
                return;
            }

            enterSafeMode = !_manualBlackout;
            _automaticBlackout = true;
            state = _manualBlackout
                ? LightingSafetyState.ManualBlackout
                : LightingSafetyState.TimedOut;
            SetStateLocked(state.Value);
        }

        if (enterSafeMode)
        {
            _actions.EnterSafeMode();
        }

        RaiseStateChanged(state.Value);
    }

    public void SetManualBlackout(bool enabled)
    {
        LightingSafetyState state;
        var enterSafeMode = false;
        var resumeOutput = false;
        var replayState = false;

        lock (_stateLock)
        {
            if (_manualBlackout == enabled)
            {
                return;
            }

            if (!enabled && _hasReceivedValidPacket && !_automaticBlackout &&
                _timeProvider.GetElapsedTime(_lastValidPacketTimestamp) >= _timeout)
            {
                _automaticBlackout = true;
            }

            var wasSuppressed = _manualBlackout || _automaticBlackout;
            _manualBlackout = enabled;
            var isSuppressed = _manualBlackout || _automaticBlackout;

            if (enabled)
            {
                state = LightingSafetyState.ManualBlackout;
                enterSafeMode = !wasSuppressed;
            }
            else if (_automaticBlackout)
            {
                state = LightingSafetyState.TimedOut;
            }
            else if (_hasReceivedValidPacket)
            {
                state = LightingSafetyState.Receiving;
                resumeOutput = wasSuppressed && !isSuppressed;
                replayState = true;
            }
            else
            {
                state = LightingSafetyState.WaitingForYarg;
                resumeOutput = wasSuppressed && !isSuppressed;
            }

            SetStateLocked(state);
        }

        if (enterSafeMode)
        {
            _actions.EnterSafeMode();
        }
        else if (resumeOutput)
        {
            _actions.ExitSafeMode(replayState);
        }

        RaiseStateChanged(state);
    }

    private bool SetStateLocked(LightingSafetyState state)
    {
        if (_state == state)
        {
            return false;
        }

        _state = state;
        return true;
    }

    private void RaiseStateChanged(LightingSafetyState state)
    {
        StateChanged?.Invoke(state);
    }
}
