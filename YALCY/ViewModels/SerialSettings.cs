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
