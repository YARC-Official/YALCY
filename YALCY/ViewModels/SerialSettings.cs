using System.Collections.ObjectModel;
using Avalonia.Threading;
using HidSharp;

namespace YALCY.ViewModels;

public partial class MainWindowViewModel
{
    private string _serialMessage;

    public string SerialMessage
    {
        get => _serialMessage;
        set
        {
            if (_serialMessage != value)
            {
                _serialMessage = value;
                OnPropertyChanged();
            }
        }
    }
}

public class SerialDeviceViewModel
{
    private readonly SerialDevice _device;

    public SerialDeviceViewModel(SerialDevice device)
    {
        _device = device;
    }
    public string DevicePath => _device.DevicePath;
    public string FriendlyName => _device.GetFriendlyName(); // Expose as a property
    public string FileSystemName => _device.GetFileSystemName();
}
