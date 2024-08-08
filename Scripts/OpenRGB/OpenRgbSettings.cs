

using ReactiveUI;

namespace YALCY.ViewModels;

public partial class MainWindowViewModel
{
    private string _openRgbStatus;
    public string OpenRgbStatus
    {
        get => _openRgbStatus;
        set
        {
            if (_openRgbStatus == value) return;
            _openRgbStatus = value;
            OnPropertyChanged();
        }
    }

    private string? _openRgbServerIp;

    public string? OpenRgbServerIp
    {
        get => _openRgbServerIp;
        set
        {
            if (_openRgbServerIp != value)
            {
                _openRgbServerIp = value;
                OnPropertyChanged();
            }
        }
    }

    private ushort _openRgbServerPort;
    public ushort OpenRgbServerPort
    {
        get => _openRgbServerPort;
        set => this.RaiseAndSetIfChanged(ref _openRgbServerPort, value);
    }

    private void FeedInOpenRgbSettings()
    {
        OpenRgbStatus = "OpenRGB Status: Currently doing nothing.";
    }
}
