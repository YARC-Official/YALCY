using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Avalonia.Media;
using Avalonia.Threading;
using ReactiveUI;
using YALCY.Integrations.HomeAssistant;

namespace YALCY.ViewModels;

public partial class MainWindowViewModel
{
    private string? _homeAssistantUrl;
    private string? _homeAssistantAccessToken;
    private string _homeAssistantStatus = string.Empty;
    private string _homeAssistantMessage = string.Empty;
    private string _homeAssistantEntityToAdd = string.Empty;
    private List<HomeAssistantAssignmentSetting> _savedHomeAssistantAssignments = new();

    public ICommand DiscoverHomeAssistantLightsCommand { get; set; } = null!;
    public ICommand AddHomeAssistantEntityCommand { get; set; } = null!;
    public ObservableCollection<HomeAssistantLightViewModel> HomeAssistantLights { get; private set; } = new();

    public string? HomeAssistantUrl
    {
        get => _homeAssistantUrl;
        set
        {
            if (_homeAssistantUrl == value) return;
            _homeAssistantUrl = value;
            OnPropertyChanged();
        }
    }

    public string? HomeAssistantAccessToken
    {
        get => _homeAssistantAccessToken;
        set
        {
            if (_homeAssistantAccessToken == value) return;
            _homeAssistantAccessToken = value;
            OnPropertyChanged();
        }
    }

    public string HomeAssistantStatus
    {
        get => _homeAssistantStatus;
        set
        {
            if (_homeAssistantStatus == value) return;
            _homeAssistantStatus = value;
            OnPropertyChanged();
        }
    }

    public string HomeAssistantMessage
    {
        get => _homeAssistantMessage;
        set
        {
            if (_homeAssistantMessage == value) return;
            _homeAssistantMessage = value;
            OnPropertyChanged();
        }
    }

    public string HomeAssistantEntityToAdd
    {
        get => _homeAssistantEntityToAdd;
        set => this.RaiseAndSetIfChanged(ref _homeAssistantEntityToAdd, value);
    }

    private void FeedInHomeAssistantSettings()
    {
        HomeAssistantUrl = SettingsManager.HomeAssistantUrl;
        HomeAssistantAccessToken = SettingsManager.HomeAssistantAccessToken;
        HomeAssistantStatus = "Home Assistant status: Currently doing nothing.";
        HomeAssistantMessage = string.Empty;
        _savedHomeAssistantAssignments = CloneHomeAssistantAssignments(SettingsManager.HomeAssistantAssignments);
    }

    private void InitializeHomeAssistantCollections()
    {
        HomeAssistantLights = new ObservableCollection<HomeAssistantLightViewModel>();
        if (_savedHomeAssistantAssignments.Count == 0)
        {
            return;
        }

        var savedAssignments = _savedHomeAssistantAssignments
                     .Where(assignment => !string.IsNullOrWhiteSpace(assignment.EntityId))
                     .OrderBy(assignment => assignment.EntityId, StringComparer.OrdinalIgnoreCase)
                     .ToList();

        for (var i = 0; i < savedAssignments.Count; i++)
        {
            var assignment = savedAssignments[i];
            HomeAssistantLights.Add(new HomeAssistantLightViewModel(
                new HomeAssistantLightModel(assignment.EntityId, assignment.EntityId, string.Empty),
                assignment.StageLight,
                OnHomeAssistantAssignmentChanged,
                IsAlternateRow(i)));
        }
    }

    public IReadOnlyList<HomeAssistantAssignmentSetting> GetHomeAssistantAssignments()
    {
        if (HomeAssistantLights.Count > 0)
        {
            return BuildHomeAssistantAssignments(HomeAssistantLights);
        }

        return CloneHomeAssistantAssignments(_savedHomeAssistantAssignments);
    }

    public void SetHomeAssistantLights(IReadOnlyList<HomeAssistantLightModel> lights)
    {
        var currentAssignments = GetHomeAssistantAssignments()
            .Where(assignment => !string.IsNullOrWhiteSpace(assignment.EntityId))
            .GroupBy(assignment => assignment.EntityId.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().StageLight, StringComparer.OrdinalIgnoreCase);

        var lightModels = lights.ToList();
        var discoveredIds = new HashSet<string>(lightModels.Select(light => light.EntityId), StringComparer.OrdinalIgnoreCase);
        foreach (var savedAssignment in currentAssignments)
        {
            if (!discoveredIds.Contains(savedAssignment.Key))
            {
                lightModels.Add(new HomeAssistantLightModel(savedAssignment.Key, savedAssignment.Key, string.Empty));
            }
        }

        var visualLights = lightModels
            .OrderBy(light => light.FriendlyName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(light => light.EntityId, StringComparer.OrdinalIgnoreCase)
            .Select((light, index) =>
            {
                currentAssignments.TryGetValue(light.EntityId, out var stageLight);
                return new HomeAssistantLightViewModel(
                    light,
                    stageLight,
                    OnHomeAssistantAssignmentChanged,
                    IsAlternateRow(index));
            })
            .ToList();

        void UpdateCollection()
        {
            HomeAssistantLights.Clear();
            foreach (var light in visualLights)
            {
                HomeAssistantLights.Add(light);
            }

            _savedHomeAssistantAssignments = BuildHomeAssistantAssignments(HomeAssistantLights);
        }

        try
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                UpdateCollection();
            }
            else
            {
                Dispatcher.UIThread.InvokeAsync(UpdateCollection);
            }
        }
        catch (InvalidOperationException)
        {
            UpdateCollection();
        }
    }

    public void SetHomeAssistantStatus(string status, string message)
    {
        void UpdateStatus()
        {
            HomeAssistantStatus = status;
            HomeAssistantMessage = message;
        }

        try
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                UpdateStatus();
            }
            else
            {
                Dispatcher.UIThread.InvokeAsync(UpdateStatus);
            }
        }
        catch (InvalidOperationException)
        {
            UpdateStatus();
        }
    }

    private void AddHomeAssistantEntity()
    {
        var entityId = HomeAssistantEntityToAdd?.Trim();
        if (string.IsNullOrWhiteSpace(entityId))
        {
            return;
        }

        if (!entityId.StartsWith("light.", StringComparison.OrdinalIgnoreCase))
        {
            HomeAssistantMessage = "Home Assistant message: Entity IDs should start with light.";
            return;
        }

        if (HomeAssistantLights.Any(light => string.Equals(light.EntityId, entityId, StringComparison.OrdinalIgnoreCase)))
        {
            HomeAssistantMessage = "Home Assistant message: That entity is already in the list.";
            return;
        }

        HomeAssistantLights.Add(new HomeAssistantLightViewModel(
            new HomeAssistantLightModel(entityId, entityId, string.Empty),
            HomeAssistantStageAssignments.Unassigned,
            OnHomeAssistantAssignmentChanged,
            IsAlternateRow(HomeAssistantLights.Count)));
        HomeAssistantEntityToAdd = string.Empty;
        OnHomeAssistantAssignmentChanged();
    }

    private void OnHomeAssistantAssignmentChanged()
    {
        _savedHomeAssistantAssignments = BuildHomeAssistantAssignments(HomeAssistantLights);
        HomeAssistantTalker.RefreshAssignments();
    }

    private static List<HomeAssistantAssignmentSetting> BuildHomeAssistantAssignments(IEnumerable<HomeAssistantLightViewModel> lights)
    {
        return lights
            .Where(light => !string.IsNullOrWhiteSpace(light.EntityId))
            .Select(light => new HomeAssistantAssignmentSetting
            {
                EntityId = light.EntityId.Trim(),
                StageLight = HomeAssistantStageAssignments.Normalize(light.SelectedStageLight)
            })
            .ToList();
    }

    private static List<HomeAssistantAssignmentSetting> CloneHomeAssistantAssignments(IEnumerable<HomeAssistantAssignmentSetting> assignments)
    {
        return assignments
            .Where(assignment => !string.IsNullOrWhiteSpace(assignment.EntityId))
            .Select(assignment => new HomeAssistantAssignmentSetting
            {
                EntityId = assignment.EntityId.Trim(),
                StageLight = HomeAssistantStageAssignments.Normalize(assignment.StageLight)
            })
            .ToList();
    }

    private static bool IsAlternateRow(int index)
    {
        return index % 2 == 1;
    }
}

public sealed class HomeAssistantLightViewModel : ReactiveObject
{
    private static readonly IBrush RowBackgroundBrush = new SolidColorBrush(Color.Parse("#10121A"));
    private static readonly IBrush AlternateRowBackgroundBrush = new SolidColorBrush(Color.Parse("#171B28"));

    private readonly Action? _onAssignmentChanged;
    private string _selectedStageLight;

    internal HomeAssistantLightViewModel(
        HomeAssistantLightModel light,
        string? selectedStageLight,
        Action? onAssignmentChanged,
        bool isAlternateRow)
    {
        EntityId = light.EntityId;
        FriendlyName = light.FriendlyName;
        State = string.IsNullOrWhiteSpace(light.State) ? "Unknown" : light.State;
        DisplayName = string.Equals(FriendlyName, EntityId, StringComparison.OrdinalIgnoreCase)
            ? EntityId
            : $"{FriendlyName} ({EntityId})";
        RowBackground = isAlternateRow ? AlternateRowBackgroundBrush : RowBackgroundBrush;
        _selectedStageLight = HomeAssistantStageAssignments.Normalize(selectedStageLight);
        _onAssignmentChanged = onAssignmentChanged;
    }

    public string EntityId { get; }
    public string FriendlyName { get; }
    public string State { get; }
    public string DisplayName { get; }
    public IBrush RowBackground { get; }
    public IReadOnlyList<string> AvailableStageLights => HomeAssistantStageAssignments.AllOptions;

    public string SelectedStageLight
    {
        get => _selectedStageLight;
        set
        {
            var normalized = HomeAssistantStageAssignments.Normalize(value);
            if (_selectedStageLight == normalized)
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _selectedStageLight, normalized);
            _onAssignmentChanged?.Invoke();
        }
    }
}
