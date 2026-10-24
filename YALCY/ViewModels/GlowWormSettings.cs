using System.Linq;
using System.Windows.Input;
using Avalonia.Threading;
using ReactiveUI;
using YALCY.Integrations.WledSerial;
using YALCY.Integrations.GlowWorm;

namespace YALCY.ViewModels;

public partial class MainWindowViewModel
{
    public GlowWormPanelViewModel GlowWorm { get; } = new(SettingsManager.GlowWormOptions);
    public EnableSetting GlowWormEnabledSetting { get; private set; } = null!;
}

public sealed class GlowWormPanelViewModel : ReactiveObject
{
    public GlowWormOptions Options { get; }
    public GlowWormOutput Output { get; } = new();
    public string[] TestColors { get; } = { "Red", "Green", "Blue", "Yellow", "White" };
    public string TestColor { get; set; } = "Blue";
    private string _status = "Disabled. Enter your Glow Worm controller IP address.";
    public string Status { get => _status; private set => this.RaiseAndSetIfChanged(ref _status, value); }
    public WledSerialMapping? SelectedMapping { get; set; }
    public ICommand TestColorCommand { get; }
    public ICommand AddMappingCommand { get; }
    public ICommand RemoveMappingCommand { get; }
    public ICommand ResetMappingsCommand { get; }
    public GlowWormPanelViewModel(GlowWormOptions options)
    {
        Options = options;
        Output.StatusChanged += text =>
        {
            if (Dispatcher.UIThread.CheckAccess()) Status = text;
            else Dispatcher.UIThread.Post(() => Status = text);
        };
        TestColorCommand = ReactiveCommand.Create(() => Output.TestColor(TestColor));
        AddMappingCommand = ReactiveCommand.Create(() => Options.Lighting.Mappings.Add(new WledSerialMapping
        { Start = Options.Lighting.Mappings.Count == 0 ? 1 : Options.Lighting.Mappings.Max(m => m.Start + m.Count) }));
        RemoveMappingCommand = ReactiveCommand.Create(() => { if (SelectedMapping != null) Options.Lighting.Mappings.Remove(SelectedMapping); });
        ResetMappingsCommand = ReactiveCommand.Create(() =>
        {
            if (Options.Lighting.LedCount is < 1 or > 2048) return;
            Options.Lighting.Mappings.Clear();
            var colors = new[] { "Blue", "Red", "Green", "Yellow" };
            for (var bank = 0; bank < 4; bank++)
            {
                var start = bank * Options.Lighting.LedCount / 4;
                var end = (bank + 1) * Options.Lighting.LedCount / 4;
                if (end > start) Options.Lighting.Mappings.Add(new WledSerialMapping { Start = start + 1, Count = end - start, Assignment = colors[bank] + " bank" });
            }
        });
    }
}

