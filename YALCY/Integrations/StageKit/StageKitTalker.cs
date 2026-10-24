using System;
using YALCY.Lighting;
using YALCY.Usb;
using YALCY.Views.Components;

namespace YALCY.Integrations.StageKit;

// Stage Kit is an output adapter; game cue processing belongs to LightingEngine.
public class StageKitTalker
{
    private readonly Action<LightingCommand, byte> _send;
    private readonly object _gate = new();
    private bool _isEnabled;

    public StageKitTalker() : this(UsbDeviceMonitor.SendReport) { }

    internal StageKitTalker(Action<LightingCommand, byte> send) => _send = send;

    public void EnableStageKitTalker(bool isEnabled)
    {
        lock (_gate)
        {
            if (_isEnabled == isEnabled) return;
            _isEnabled = isEnabled;
            if (isEnabled)
                LightingOutput.CommandReceived += OnCommand;
            else
            {
                LightingOutput.CommandReceived -= OnCommand;
                // Black out only this output when it is disabled.
                _send(LightingCommand.DisableAll, 0);
            }
        }
        StatusFooter.UpdateStatus("StageKit", isEnabled ? IntegrationStatus.Connected : IntegrationStatus.Off);
    }

    private void OnCommand(LightingCommand command, byte parameter)
    {
        lock (_gate)
        {
            if (_isEnabled) _send(command, parameter);
        }
    }
}
