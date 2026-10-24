using System;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;
using System.Windows.Input;
using Avalonia.Threading;
using ReactiveUI;
using YALCY.Diagnostics;
using YALCY.Integrations.WledSerial;

namespace YALCY.ViewModels;

public partial class MainWindowViewModel
{
    public WledSerialPanelViewModel WledSerial { get; } = new(SettingsManager.WledSerialOptions);
    public EnableSetting WledSerialEnabledSetting { get; private set; } = null!;
}

public sealed class WledSerialPanelViewModel : ReactiveObject
{
    public WledSerialOptions Options { get; }
    public WledSerialOutput Output { get; } = new();
    public ObservableCollection<string> Ports { get; } = new();
    public int[] BaudRates => WledSerialOptions.BaudRates;
    public string[] TestColors { get; } = { "Red", "Green", "Blue", "Yellow", "White" };
    public string TestColor { get; set; } = "Blue";
    private string _status = "Disabled. Select the USB serial port for your WLED controller.";
    public string Status { get => _status; private set => this.RaiseAndSetIfChanged(ref _status, value); }
    public WledSerialMapping? SelectedMapping { get; set; }
    public ICommand RefreshPortsCommand { get; }
    public ICommand TestColorCommand { get; }
    public ICommand AddMappingCommand { get; }
    public ICommand RemoveMappingCommand { get; }
    public ICommand ResetMappingsCommand { get; }
    public WledSerialPanelViewModel(WledSerialOptions options)
    {
        Options = options;
        Output.StatusChanged += text =>
        {
            if (Dispatcher.UIThread.CheckAccess()) Status = text;
            else Dispatcher.UIThread.Post(() => Status = text);
        };
        RefreshPortsCommand = ReactiveCommand.Create(RefreshPorts);
        TestColorCommand = ReactiveCommand.Create(() => Output.TestColor(TestColor));
        AddMappingCommand = ReactiveCommand.Create(() => Options.Mappings.Add(new WledSerialMapping
        { Start = Options.Mappings.Count == 0 ? 1 : Options.Mappings.Max(m => m.Start + m.Count) }));
        RemoveMappingCommand = ReactiveCommand.Create(() => { if (SelectedMapping != null) Options.Mappings.Remove(SelectedMapping); });
        ResetMappingsCommand = ReactiveCommand.Create(() =>
        {
            if (Options.LedCount is < 1 or > 2048) return;
            Options.Mappings.Clear();
            var colors = new[] { "Blue", "Red", "Green", "Yellow" };
            for (var bank = 0; bank < 4; bank++)
            {
                var start = bank * Options.LedCount / 4;
                var end = (bank + 1) * Options.LedCount / 4;
                if (end > start) Options.Mappings.Add(new WledSerialMapping { Start = start + 1, Count = end - start, Assignment = colors[bank] + " bank" });
            }
        });
        RefreshPorts();
    }
    private void RefreshPorts()
    {
        try
        {
            var ports = SerialPort.GetPortNames().OrderBy(p => p).ToArray();
            var selected = Options.Port;
            Ports.Clear(); foreach (var port in ports) Ports.Add(port);
            if (!string.IsNullOrWhiteSpace(selected) && !Ports.Contains(selected)) Ports.Add(selected);
            Options.Port = selected;
        }
        catch (Exception ex) { AppLog.Write(LogLevel.Error, "WLED Serial", $"Could not list serial ports: {ex.Message}"); }
    }
}
