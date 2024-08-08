using HueApi.Models.Clip;

namespace YALCY.ViewModels;

public partial class MainWindowViewModel
{
        private string _hueIpStatus;

    public string HueIpStatus
    {
        get => _hueIpStatus;
        set
        {
            if (_hueIpStatus == value) return;
            _hueIpStatus = value;
            OnPropertyChanged();
        }
    }

    private string _hueRegisterStatus;

    public string HueRegisterStatus
    {
        get => _hueRegisterStatus;
        set
        {
            if (_hueRegisterStatus == value) return;
            _hueRegisterStatus = value;
            OnPropertyChanged();
        }
    }

    private string _hueStreamingClientStatus;

    public string HueStreamingClientStatus
    {
        get => _hueStreamingClientStatus;
        set
        {
            if (_hueStreamingClientStatus == value) return;
            _hueStreamingClientStatus = value;
            OnPropertyChanged();
        }
    }

    private string _hueEntertainmentGroupStatus;

    public string HueEntertainmentGroupStatus
    {
        get => _hueEntertainmentGroupStatus;
        set
        {
            if (_hueEntertainmentGroupStatus == value) return;
            _hueEntertainmentGroupStatus = value;
            OnPropertyChanged();
        }
    }

    private string _hueStreamingActiveStatus;

    public string HueStreamingActiveStatus
    {
        get => _hueStreamingActiveStatus;
        set
        {
            if (_hueStreamingActiveStatus == value) return;
            _hueStreamingActiveStatus = value;
            OnPropertyChanged();
        }
    }

    private string _hueMessage;

    public string HueMessage
    {
        get => _hueMessage;
        set
        {
            if (_hueMessage != value)
            {
                _hueMessage = value;
                OnPropertyChanged();
            }
        }
    }

    private string? _hueBridgeIp;

    public string? HueBridgeIp
    {
        get => _hueBridgeIp;
        set
        {
            if (_hueBridgeIp != value)
            {
                _hueBridgeIp = value;
                OnPropertyChanged();
            }
        }
    }

    private RegisterEntertainmentResult _hueAuthResult;

    public RegisterEntertainmentResult HueAuthResult
    {
        get => _hueAuthResult;
        set
        {
            if (_hueAuthResult != value)
            {
                _hueAuthResult = value;
                OnPropertyChanged();
            }
        }
    }

    private void FeedInHueSettings()
    {
        HueRegisterStatus = "Registering Status: Currently doing nothing.";
        HueBridgeIp = SettingsManager.HueBridgeIp;

        HueAuthResult = new RegisterEntertainmentResult
        {
            Username = SettingsManager.HueAuthResultUsername,
            StreamingClientKey = SettingsManager.HueAuthResultStreamingClientKey,
            Ip = SettingsManager.HueAuthResultIp
        };
        HueStreamingClientStatus = "Streaming Client Status: Currently doing nothing.";
        HueEntertainmentGroupStatus = "Entertainment Group Status: Currently doing nothing.";
        HueStreamingActiveStatus = "Streaming Active Status: Currently doing nothing.";

    }
}
