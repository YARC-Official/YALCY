using System;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Avalonia;
using ReactiveUI;

namespace YALCY.ViewModels;

public abstract class DataTypes
{
    public interface IDmxChannelSetting
    {
        string Label { get; set; }
        int[]? Channel { get; set; }
    }

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

    public class EnableSetting : ReactiveObject
    {
        private string _label;
        private bool _isEnabled;
        private readonly string _onString;
        private readonly string _offString;
        private readonly Func<bool, Task> _onSettingChanged;

        [DataMember]
        public string Label
        {
            get => _label;
            set => this.RaiseAndSetIfChanged(ref _label, value);
        }

        [DataMember]
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                this.RaiseAndSetIfChanged(ref _isEnabled, value);
                this.RaisePropertyChanged(nameof(ToggleButtonContent));
                _onSettingChanged?.Invoke(value);
            }
        }

        private string _toolTip;

        [JsonIgnore]
        public string ToolTip
        {
            get => _toolTip;
            set => this.RaiseAndSetIfChanged(ref _toolTip, value);
        }

        [JsonIgnore]
        public string ToggleButtonContent => IsEnabled ? _onString : _offString;

        public EnableSetting(string label, bool isEnabled, string onString, string offString, Func<bool, Task>onSettingChanged, string toolTip)
        {
            Label = label;
            IsEnabled = isEnabled;
            _onString = onString;
            _offString = offString;
            _onSettingChanged = onSettingChanged;
            ToolTip = toolTip;
        }
    }

    public class DmxSingleSetting : ReactiveObject
    {
        private string _label;
        private int _value;

        [DataMember]
        public string Label
        {
            get => _label;
            set => this.RaiseAndSetIfChanged(ref _label, value);
        }

        [DataMember]
        public int Value
        {
            get => _value;
            set => this.RaiseAndSetIfChanged(ref _value, value);
        }

        public DmxSingleSetting(string label, int value)
        {
            Label = label;
            Value = value;
        }
    }

    public class DmxChannelSetting : ReactiveObject , IDmxChannelSetting
    {
        private string _label;
        private int[]? _channel;

        [DataMember]
        public string Label
        {
            get => _label;
            set => this.RaiseAndSetIfChanged(ref _label, value);
        }

        [DataMember]
        public int[]? Channel
        {
            get => _channel;
            set => this.RaiseAndSetIfChanged(ref _channel, value);
        }

        public DmxChannelSetting(string label, params int[]? channels)
        {
            Label = label;
            Channel = channels;
        }
    }

    public class DmxDimmerChannelSetting : ReactiveObject , IDmxChannelSetting
    {
        private string _label;
        private int[]? _channel;

        [DataMember]
        public string Label
        {
            get => _label;
            set => this.RaiseAndSetIfChanged(ref _label, value);
        }

        [DataMember]
        public int[]? Channel
        {
            get => _channel;
            set
            {
                this.RaiseAndSetIfChanged(ref _channel, value);
                // Safely access the MainViewModel instance
                var app = (App)Application.Current!;
                if (app.MainViewModel != null)
                {
                    //set previous channel value to 0
                    foreach (var t in _channel)
                    {
                        app.MainViewModel.DmxTalker?.SetChannelToValue(t, 0);
                    }

                    //set the new channel value to the previous value
                    app.MainViewModel.DmxTalker?.UpdateMasterDimmers();
                }
            }
        }

        public DmxDimmerChannelSetting(string label, params int[]? channels)
        {
            Label = label;
            Channel = channels;
        }
    }

    public class DmxDimmerValueSetting : ReactiveObject, IDmxChannelSetting
    {
        private string _label;
        private int[]? _channel;

        [DataMember]
        public string Label
        {
            get => _label;
            set => this.RaiseAndSetIfChanged(ref _label, value);
        }

        [DataMember]
        public int[]? Channel
        {
            get => _channel;
            set
            {
                this.RaiseAndSetIfChanged(ref _channel, value);

                // Safely access the MainViewModel instance
                var app = (App)Application.Current!;
                if (app.MainViewModel != null)
                {
                    app.MainViewModel.DmxTalker?.UpdateMasterDimmers();
                }
            }
        }

        public DmxDimmerValueSetting(string label, params int[]? channels)
        {
            Label = label;
            Channel = channels;
        }
    }
}
