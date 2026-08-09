using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using YALCY.Integrations;
using YALCY.Integrations.StageKit;
using YALCY.Udp;
using YALCY.Usb;
using YALCY.ViewModels;
using YALCY.Views.Components;

namespace YALCY.Integrations.HomeAssistant;

public sealed class HomeAssistantTalker : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly object _stateLock = new();
    private readonly StageSlotState[] _slotStates = Enumerable.Range(0, 8).Select(_ => new StageSlotState()).ToArray();
    private readonly SlotOutput?[] _lastSlotOutputs = new SlotOutput?[8];
    private readonly SemaphoreSlim _sendSemaphore = new(1, 1);

    private MainWindowViewModel? _mainViewModel;
    private HttpClient? _client;
    private CancellationTokenSource? _cancellationTokenSource;
    private Dictionary<int, List<string>> _slotAssignments = new();
    private List<string> _strobeAssignments = new();
    private bool _isEnabled;
    private bool _isSubscribedToStageKit;
    private bool _lastStrobeOn;
    private readonly ManualStrobeFlasher _manualStrobeFlasher = new(ex => Console.WriteLine($"Home Assistant manual strobe error: {ex.Message}"));

    public async Task EnableHomeAssistant(bool isEnabled, MainWindowViewModel? viewModel = null)
    {
        if (viewModel != null)
        {
            _mainViewModel = viewModel;
        }

        if (isEnabled)
        {
            if (_mainViewModel == null)
            {
                Console.WriteLine("HomeAssistantTalker: No ViewModel provided and none cached.");
                return;
            }

            StatusFooter.UpdateStatus("HomeAssistant", IntegrationStatus.Connecting);
            _mainViewModel.SetHomeAssistantStatus("Home Assistant status: Connecting...", string.Empty);

            try
            {
                _client?.Dispose();
                _client = CreateClient(_mainViewModel.HomeAssistantUrl, _mainViewModel.HomeAssistantAccessToken);
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();

                await TestConnectionAsync(_client, _cancellationTokenSource.Token);

                RebuildAssignments();
                SubscribeToStageKit();
                _isEnabled = true;

                var assignmentCount = _slotAssignments.Values.Sum(items => items.Count) + _strobeAssignments.Count;
                _mainViewModel.SetHomeAssistantStatus(
                    $"Home Assistant status: Connected. {assignmentCount} assigned light(s).",
                    "Only assigned Home Assistant light entities will receive YALCY commands.");
                StatusFooter.UpdateStatus("HomeAssistant", IntegrationStatus.Connected);
            }
            catch (Exception ex)
            {
                _isEnabled = false;
                UnsubscribeFromStageKit();
                _client?.Dispose();
                _client = null;

                _mainViewModel.SetHomeAssistantStatus("Home Assistant status: Connection failed.", $"Error: {ex.Message}");
                StatusFooter.UpdateStatus("HomeAssistant", IntegrationStatus.Error);
            }

            return;
        }

        _isEnabled = false;
        UnsubscribeFromStageKit();
        _manualStrobeFlasher.Stop(SendManualStrobeFlashStateAsync);
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        _client?.Dispose();
        _client = null;

        lock (_stateLock)
        {
            foreach (var state in _slotStates)
            {
                state.Clear();
            }

            Array.Clear(_lastSlotOutputs, 0, _lastSlotOutputs.Length);
            _lastStrobeOn = false;
        }

        StatusFooter.UpdateStatus("HomeAssistant", IntegrationStatus.Off);
        _mainViewModel?.SetHomeAssistantStatus("Home Assistant status: Disabled.", string.Empty);
    }

    public async Task DiscoverLightsAsync(MainWindowViewModel? viewModel = null)
    {
        if (viewModel != null)
        {
            _mainViewModel = viewModel;
        }

        if (_mainViewModel == null)
        {
            Console.WriteLine("HomeAssistantTalker: No ViewModel provided and none cached.");
            return;
        }

        var shouldUpdateFooter = _isEnabled;
        if (shouldUpdateFooter)
        {
            StatusFooter.UpdateStatus("HomeAssistant", IntegrationStatus.Connecting);
        }

        _mainViewModel.SetHomeAssistantStatus("Home Assistant status: Discovering light entities...", string.Empty);

        HttpClient? temporaryClient = null;
        try
        {
            var client = _client;
            if (client == null)
            {
                temporaryClient = CreateClient(_mainViewModel.HomeAssistantUrl, _mainViewModel.HomeAssistantAccessToken);
                client = temporaryClient;
            }

            var lights = await GetLightStatesAsync(client, CancellationToken.None);
            _mainViewModel.SetHomeAssistantLights(lights);
            RebuildAssignments();

            _mainViewModel.SetHomeAssistantStatus(
                $"Home Assistant status: Discovered {lights.Count} light entity(s).",
                "Assign only the lights YALCY should control.");

            if (shouldUpdateFooter)
            {
                StatusFooter.UpdateStatus("HomeAssistant", IntegrationStatus.Connected);
            }
        }
        catch (Exception ex)
        {
            _mainViewModel.SetHomeAssistantStatus("Home Assistant status: Discovery failed.", $"Error: {ex.Message}");
            if (shouldUpdateFooter)
            {
                StatusFooter.UpdateStatus("HomeAssistant", IntegrationStatus.Error);
            }
        }
        finally
        {
            temporaryClient?.Dispose();
        }
    }

    public void RefreshAssignments()
    {
        RebuildAssignments();
        if (!_isEnabled)
        {
            return;
        }

        QueueSendWork(async token =>
        {
            for (var i = 0; i < 8; i++)
            {
                await SendSlotStateAsync(i, token);
            }

            await SendStrobeStateAsync(_lastStrobeOn, token, force: true);
        });
    }

    private void SubscribeToStageKit()
    {
        if (_isSubscribedToStageKit)
        {
            return;
        }

        UsbDeviceMonitor.OnStageKitCommand += OnStageKitEvent;
        _isSubscribedToStageKit = true;
    }

    private void UnsubscribeFromStageKit()
    {
        if (!_isSubscribedToStageKit)
        {
            return;
        }

        UsbDeviceMonitor.OnStageKitCommand -= OnStageKitEvent;
        _isSubscribedToStageKit = false;
    }

    private void OnStageKitEvent(StageKitTalker.CommandId commandId, byte parameter)
    {
        if (!_isEnabled || _client == null)
        {
            return;
        }

        switch (commandId)
        {
            case StageKitTalker.CommandId.BlueLeds:
                UpdateColorBank(parameter, static (state, isOn) => state.Blue = isOn);
                break;

            case StageKitTalker.CommandId.GreenLeds:
                UpdateColorBank(parameter, static (state, isOn) => state.Green = isOn);
                break;

            case StageKitTalker.CommandId.YellowLeds:
                UpdateColorBank(parameter, static (state, isOn) => state.Yellow = isOn);
                break;

            case StageKitTalker.CommandId.RedLeds:
                UpdateColorBank(parameter, static (state, isOn) => state.Red = isOn);
                break;

            case StageKitTalker.CommandId.DisableAll:
                ClearAllSlots();
                QueueSendWork(async token =>
                {
                    for (var i = 0; i < 8; i++)
                    {
                        await SendSlotStateAsync(i, token);
                    }

                    _manualStrobeFlasher.Stop(SendManualStrobeFlashStateAsync);
                    await SendStrobeStateAsync(false, token);
                });
                break;

            case StageKitTalker.CommandId.StrobeSlow:
            case StageKitTalker.CommandId.StrobeMedium:
            case StageKitTalker.CommandId.StrobeFast:
            case StageKitTalker.CommandId.StrobeFastest:
                if (_mainViewModel?.HomeAssistantStrobeMode == StrobeOutputModes.ManualFlash)
                {
                    _manualStrobeFlasher.Start(
                        commandId,
                        UdpIntake.BeatsPerMinute.Value,
                        SendManualStrobeFlashStateAsync);
                }
                else
                {
                    _manualStrobeFlasher.Stop(SendManualStrobeFlashStateAsync);
                    QueueSendWork(token => SendStrobeStateAsync(true, token));
                }
                break;

            case StageKitTalker.CommandId.StrobeOff:
                _manualStrobeFlasher.Stop(SendManualStrobeFlashStateAsync);
                QueueSendWork(token => SendStrobeStateAsync(false, token));
                break;
        }
    }

    private void UpdateColorBank(byte activeSlotsMask, Action<StageSlotState, bool> update)
    {
        lock (_stateLock)
        {
            for (var i = 0; i < _slotStates.Length; i++)
            {
                update(_slotStates[i], (activeSlotsMask & (1 << i)) != 0);
            }
        }

        QueueSendWork(async token =>
        {
            for (var i = 0; i < 8; i++)
            {
                await SendSlotStateAsync(i, token);
            }
        });
    }

    private void ClearAllSlots()
    {
        lock (_stateLock)
        {
            foreach (var state in _slotStates)
            {
                state.Clear();
            }
        }
    }

    private void QueueSendWork(Func<CancellationToken, Task> work)
    {
        var cancellationTokenSource = _cancellationTokenSource;
        if (cancellationTokenSource == null)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await _sendSemaphore.WaitAsync(cancellationTokenSource.Token);
                try
                {
                    await work(cancellationTokenSource.Token);
                }
                finally
                {
                    _sendSemaphore.Release();
                }
            }
            catch (OperationCanceledException)
            {
                // Disabled while a queued Home Assistant command was waiting.
            }
            catch (Exception ex)
            {
                if (_mainViewModel != null)
                {
                    _mainViewModel.SetHomeAssistantStatus("Home Assistant status: Error while sending cue data.", $"Error: {ex.Message}");
                }

                if (_isEnabled)
                {
                    StatusFooter.UpdateStatus("HomeAssistant", IntegrationStatus.Error);
                }
            }
        }, cancellationTokenSource.Token);
    }

    private async Task SendSlotStateAsync(int slotIndex, CancellationToken cancellationToken)
    {
        if (!_slotAssignments.TryGetValue(slotIndex, out var entityIds) || entityIds.Count == 0)
        {
            return;
        }

        SlotOutput output;
        lock (_stateLock)
        {
            output = SlotOutput.FromState(_slotStates[slotIndex]);
            if (_lastSlotOutputs[slotIndex] == output)
            {
                return;
            }

            _lastSlotOutputs[slotIndex] = output;
        }

        if (output.IsOn)
        {
            await PostLightServiceAsync("turn_on", entityIds, output, cancellationToken);
        }
        else
        {
            await PostLightServiceAsync("turn_off", entityIds, null, cancellationToken);
        }
    }

    private async Task SendStrobeStateAsync(bool isOn, CancellationToken cancellationToken, bool force = false)
    {
        if (_strobeAssignments.Count == 0)
        {
            return;
        }

        lock (_stateLock)
        {
            if (!force && _lastStrobeOn == isOn)
            {
                return;
            }

            _lastStrobeOn = isOn;
        }

        if (isOn)
        {
            await PostLightServiceAsync("turn_on", _strobeAssignments, SlotOutput.White, cancellationToken);
        }
        else
        {
            await PostLightServiceAsync("turn_off", _strobeAssignments, null, cancellationToken);
        }
    }

    private async Task SendManualStrobeFlashStateAsync(bool isOn, CancellationToken cancellationToken)
    {
        if (_strobeAssignments.Count == 0)
        {
            return;
        }

        if (isOn)
        {
            await PostLightServiceAsync("turn_on", _strobeAssignments, SlotOutput.White, cancellationToken);
        }
        else
        {
            await PostLightServiceAsync("turn_off", _strobeAssignments, null, cancellationToken);
        }
    }

    private async Task PostLightServiceAsync(
        string service,
        IReadOnlyList<string> entityIds,
        SlotOutput? output,
        CancellationToken cancellationToken)
    {
        if (_client == null || entityIds.Count == 0)
        {
            return;
        }

        var payload = new Dictionary<string, object?>
        {
            ["entity_id"] = entityIds.Count == 1 ? entityIds[0] : entityIds.ToArray()
        };

        if (output.HasValue)
        {
            var color = output.Value;
            payload["rgb_color"] = new[] { color.Red, color.Green, color.Blue };
            payload["brightness"] = Math.Max(color.Red, Math.Max(color.Green, color.Blue));
            payload["transition"] = 0;
        }

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _client.PostAsync($"api/services/light/{service}", content, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private void RebuildAssignments()
    {
        var assignments = _mainViewModel?.GetHomeAssistantAssignments() ?? SettingsManager.HomeAssistantAssignments;

        var slotAssignments = new Dictionary<int, List<string>>();
        var strobeAssignments = new List<string>();

        foreach (var assignment in assignments)
        {
            if (string.IsNullOrWhiteSpace(assignment.EntityId))
            {
                continue;
            }

            var entityId = assignment.EntityId.Trim();
            var stageLight = HomeAssistantStageAssignments.Normalize(assignment.StageLight);

            if (HomeAssistantStageAssignments.TryGetSlot(stageLight, out var slotIndex))
            {
                if (!slotAssignments.TryGetValue(slotIndex, out var entityIds))
                {
                    entityIds = new List<string>();
                    slotAssignments[slotIndex] = entityIds;
                }

                if (!entityIds.Contains(entityId, StringComparer.OrdinalIgnoreCase))
                {
                    entityIds.Add(entityId);
                }
            }
            else if (string.Equals(stageLight, HomeAssistantStageAssignments.Strobe, StringComparison.OrdinalIgnoreCase) &&
                     !strobeAssignments.Contains(entityId, StringComparer.OrdinalIgnoreCase))
            {
                strobeAssignments.Add(entityId);
            }
        }

        lock (_stateLock)
        {
            _slotAssignments = slotAssignments;
            _strobeAssignments = strobeAssignments;
            Array.Clear(_lastSlotOutputs, 0, _lastSlotOutputs.Length);
        }
    }

    private static async Task TestConnectionAsync(HttpClient client, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync("api/", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<IReadOnlyList<HomeAssistantLightModel>> GetLightStatesAsync(
        HttpClient client,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync("api/states", cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var lights = new List<HomeAssistantLightModel>();
        foreach (var element in document.RootElement.EnumerateArray())
        {
            if (!element.TryGetProperty("entity_id", out var entityIdElement))
            {
                continue;
            }

            var entityId = entityIdElement.GetString();
            if (string.IsNullOrWhiteSpace(entityId) ||
                !entityId.StartsWith("light.", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var friendlyName = entityId;
            if (element.TryGetProperty("attributes", out var attributes) &&
                attributes.TryGetProperty("friendly_name", out var friendlyNameElement))
            {
                friendlyName = friendlyNameElement.GetString() ?? entityId;
            }

            var state = element.TryGetProperty("state", out var stateElement)
                ? stateElement.GetString() ?? string.Empty
                : string.Empty;

            lights.Add(new HomeAssistantLightModel(entityId, friendlyName, state));
        }

        return lights
            .OrderBy(light => light.FriendlyName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(light => light.EntityId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static HttpClient CreateClient(string? baseUrl, string? accessToken)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("Enter your Home Assistant URL.");
        }

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("Enter a Home Assistant long-lived access token.");
        }

        var normalizedUrl = NormalizeBaseUrl(baseUrl);
        var client = new HttpClient
        {
            BaseAddress = normalizedUrl,
            Timeout = TimeSpan.FromSeconds(8)
        };

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Trim());
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private static Uri NormalizeBaseUrl(string baseUrl)
    {
        var trimmed = baseUrl.Trim().TrimEnd('/');
        if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = "http://" + trimmed;
        }

        if (trimmed.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^4];
        }

        if (!Uri.TryCreate(trimmed + "/", UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("Home Assistant URL is not valid.");
        }

        return uri;
    }

    public void Dispose()
    {
        UnsubscribeFromStageKit();
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _client?.Dispose();
        _sendSemaphore.Dispose();
    }

    private sealed class StageSlotState
    {
        public bool Red { get; set; }
        public bool Green { get; set; }
        public bool Blue { get; set; }
        public bool Yellow { get; set; }

        public void Clear()
        {
            Red = false;
            Green = false;
            Blue = false;
            Yellow = false;
        }
    }

    private readonly record struct SlotOutput(int Red, int Green, int Blue)
    {
        public static SlotOutput White => new(255, 255, 255);

        public bool IsOn => Red > 0 || Green > 0 || Blue > 0;

        public static SlotOutput FromState(StageSlotState state)
        {
            return new SlotOutput(
                state.Red || state.Yellow ? 255 : 0,
                state.Green || state.Yellow ? 255 : 0,
                state.Blue ? 255 : 0);
        }
    }
}
