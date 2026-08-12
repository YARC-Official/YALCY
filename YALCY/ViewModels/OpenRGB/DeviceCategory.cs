using System;
using System.Threading.Tasks;
using OpenRGB.NET;
using ReactiveUI;

namespace YALCY.ViewModels.OpenRGB;

// Keep the old DeviceCategory for backward compatibility
public class DeviceCategory : ReactiveObject
{
    public Device Device { get; set; } = null!;
    private int _category;
    private MainWindowViewModel? _viewModel;

    public int Category
    {
        get => _category;
        set
        {
            if (_category == value) return;
            RemoveFromCategoryList(_category);
            _category = value;
            UpdateDeviceCategoryList(_category);
            this.RaisePropertyChanged(nameof(Category));
        }
    }

    public DeviceCategory(Device device, int initialCategory, MainWindowViewModel? viewModel = null)
    {
        Device = device;
        _category = initialCategory;
        _viewModel = viewModel;
        UpdateDeviceCategoryList(_category);
    }

    public void SetViewModel(MainWindowViewModel viewModel)
    {
        _viewModel = viewModel;
        UpdateDeviceCategoryList(_category);
    }

    private void RemoveFromCategoryList(int category)
    {
        if (_viewModel == null) return;

        lock (_viewModel.OpenRgbTalker.Lock)
        {
            switch (category)
            {
                case 0:
                    _viewModel.OpenRgbTalker.OffList.Remove(Device);
                    break;

                case 1:
                    _viewModel.OpenRgbTalker.LightPodList.Remove(Device);
                    _viewModel.OpenRgbTalker.LightPodStates.Remove(Device.Index);
                    break;

                case 2:
                    _viewModel.OpenRgbTalker.StrobeList.Remove(Device);
                    break;

                case 3:
                    _viewModel.OpenRgbTalker.FoggerList.Remove(Device);
                    break;

                case 4:
                    _viewModel.OpenRgbTalker.LightPodList.Remove(Device);
                    _viewModel.OpenRgbTalker.LightPodStates.Remove(Device.Index);
                    _viewModel.OpenRgbTalker.StrobeList.Remove(Device);
                    _viewModel.OpenRgbTalker.LightPodStrobeDevices.Remove(Device.Index);
                    break;
            }
        }

        Task.Run(() =>
        {
            try
            {
                _viewModel.OpenRgbTalker.TurnOffDeviceLeds(Device);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"TurnOffDeviceLeds error: {ex.Message}");
            }
        });
    }

    private void UpdateDeviceCategoryList(int category)
    {
        if (_viewModel == null) return;

        lock (_viewModel.OpenRgbTalker.Lock)
        {
            // Add the device to the correct list based on the category
            switch (category)
            {
                case 0:
                    _viewModel.OpenRgbTalker.OffList.Add(Device);
                    break;

                case 1:
                    _viewModel.OpenRgbTalker.LightPodList.Add(Device);
                    if (!_viewModel.OpenRgbTalker.LightPodStates.ContainsKey(Device.Index))
                    {
                        // Initialize the light pod state for this device
                        _viewModel.OpenRgbTalker.LightPodStates[Device.Index] = new Color[Device.Leds.Length];
                    }
                    break;

                case 2:
                    _viewModel.OpenRgbTalker.StrobeList.Add(Device);
                    break;

                case 3:
                    _viewModel.OpenRgbTalker.FoggerList.Add(Device);
                    break;

                case 4:
                    _viewModel.OpenRgbTalker.LightPodList.Add(Device);
                    _viewModel.OpenRgbTalker.StrobeList.Add(Device);
                    _viewModel.OpenRgbTalker.LightPodStrobeDevices.Add(Device.Index);
                    if (!_viewModel.OpenRgbTalker.LightPodStates.ContainsKey(Device.Index))
                    {
                        _viewModel.OpenRgbTalker.LightPodStates[Device.Index] = new Color[Device.Leds.Length];
                    }
                    break;
            }
        }
    }
}
