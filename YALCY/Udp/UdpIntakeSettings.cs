using System;
using System.Collections.ObjectModel;
using ReactiveUI;
using YALCY.Udp;

namespace YALCY.ViewModels;

public class ByteIndexModel : ReactiveObject
{
    private int _currentValue;
    private string _currentValueDescription;

    public string Name { get; set; }
    public string Index { get; set; }

    public int CurrentValue
    {
        get => _currentValue;
        set => this.RaiseAndSetIfChanged(ref _currentValue, value);
    }

    public string ValueDescription
    {
        get => _currentValueDescription;
        set => this.RaiseAndSetIfChanged(ref _currentValueDescription, value);
    }
}
public partial class MainWindowViewModel
{
    public static ObservableCollection<ByteIndexModel>? ByteIndexes { get; set; }

    private ushort _udpListenPort;
    public ushort UdpListenPort
    {
        get => _udpListenPort;
        set => this.RaiseAndSetIfChanged(ref _udpListenPort, value);
    }

    private void FeedInUdpSettings()
    {
        UdpListenPort = SettingsManager.UdpListenPort;
    }

    private void InitializeUdpIntakeCollections()
    {
        ByteIndexes = new ObservableCollection<ByteIndexModel>();
        var slot = 0;
        foreach (var name in Enum.GetNames<UdpIntake.ByteIndexName>())
        {
            ByteIndexes.Add(new ByteIndexModel
            {
                Name = name,
                Index = slot.ToString(),
                CurrentValue = 0,
                ValueDescription = "",
            });
            slot++;
        }
    }
}
