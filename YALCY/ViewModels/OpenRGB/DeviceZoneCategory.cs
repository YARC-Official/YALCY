using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using OpenRGB.NET;
using ReactiveUI;
using YALCY.Integrations.OpenRGB;

namespace YALCY.ViewModels.OpenRGB;

public class DeviceZoneCategory : ReactiveObject
{
    public Device Device { get; set; } = null!;
    public Zone Zone { get; set; } = null!;
    public int ZoneIndex { get; set; }
    public ICommand IdentifyZoneCommand { get; }
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
            OnPropertyChanged(nameof(Category));
        }
    }

    private void RemoveFromCategoryList(int category)
    {
        if (_viewModel == null) return;

        var key = GetZoneKey();

        lock (_viewModel.OpenRgbTalker.Lock)
        {
            switch (category)
            {
                case 0:
                    _viewModel.OpenRgbTalker.OffZones.Remove(key);
                    break;

                case 1:
                    _viewModel.OpenRgbTalker.LightPodZones.Remove(key);
                    _viewModel.OpenRgbTalker.LightPodZoneStates.Remove(key);
                    break;

                case 2:
                    _viewModel.OpenRgbTalker.StrobeZones.Remove(key);
                    break;

                case 3:
                    _viewModel.OpenRgbTalker.FoggerZones.Remove(key);
                    break;

                case 4:
                    _viewModel.OpenRgbTalker.LightPodZones.Remove(key);
                    _viewModel.OpenRgbTalker.LightPodZoneStates.Remove(key);
                    _viewModel.OpenRgbTalker.StrobeZones.Remove(key);
                    _viewModel.OpenRgbTalker.LightPodStrobeZones.Remove(key);
                    break;
            }
        }

        Task.Run(() =>
        {
            try
            {
                _viewModel.OpenRgbTalker.TurnOffZoneLeds(Device, ZoneIndex, Zone);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"TurnOffZoneLeds error: {ex.Message}");
            }
        });
    }

    private void UpdateDeviceCategoryList(int category)
    {
        if (_viewModel == null) return;

        var key = GetZoneKey();
        var zoneInfo = new ZoneInfo { Device = Device, ZoneIndex = ZoneIndex, Zone = Zone };

        lock (_viewModel.OpenRgbTalker.Lock)
        {
            switch (category)
            {
                case 0:
                    _viewModel.OpenRgbTalker.OffZones[key] = zoneInfo;
                    break;

                case 1:
                    _viewModel.OpenRgbTalker.LightPodZones[key] = zoneInfo;
                    if (!_viewModel.OpenRgbTalker.LightPodZoneStates.ContainsKey(key))
                    {
                        // Initialize the light pod state for this zone
                        int ledCount = (int)Zone.LedCount;
                        _viewModel.OpenRgbTalker.LightPodZoneStates[key] = new Color[ledCount];
                    }
                    break;

                case 2:
                    _viewModel.OpenRgbTalker.StrobeZones[key] = zoneInfo;
                    break;

                case 3:
                    _viewModel.OpenRgbTalker.FoggerZones[key] = zoneInfo;
                    break;

                case 4:
                    _viewModel.OpenRgbTalker.LightPodZones[key] = zoneInfo;
                    _viewModel.OpenRgbTalker.StrobeZones[key] = zoneInfo;
                    _viewModel.OpenRgbTalker.LightPodStrobeZones.Add(key);
                    if (!_viewModel.OpenRgbTalker.LightPodZoneStates.ContainsKey(key))
                    {
                        int ledCount = (int)Zone.LedCount;
                        _viewModel.OpenRgbTalker.LightPodZoneStates[key] = new Color[ledCount];
                    }
                    break;
            }
        }
    }

    private string GetZoneKey()
    {
        return $"{Device.Index}_{ZoneIndex}";
    }

    public string GetZoneKeyProperty => GetZoneKey();

    private bool _isCustomizingLeds;
    public bool IsCustomizingLeds
    {
        get => _isCustomizingLeds;
        set => this.RaiseAndSetIfChanged(ref _isCustomizingLeds, value);
    }

    private int _selectedPaintAreaIndex = -1;
    public int SelectedPaintAreaIndex
    {
        get => _selectedPaintAreaIndex;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedPaintAreaIndex, value);
            this.RaisePropertyChanged(nameof(SelectedPaintAreaName));
            this.RaisePropertyChanged(nameof(IsOffSelected));
            this.RaisePropertyChanged(nameof(IsBlueSelected));
            this.RaisePropertyChanged(nameof(IsRedSelected));
            this.RaisePropertyChanged(nameof(IsGreenSelected));
            this.RaisePropertyChanged(nameof(IsYellowSelected));
            this.RaisePropertyChanged(nameof(HasSubGroup));
            this.RaisePropertyChanged(nameof(ActiveSubChannelNumber));
            this.RaisePropertyChanged(nameof(IsSub1Active));
            this.RaisePropertyChanged(nameof(IsSub2Active));
            this.RaisePropertyChanged(nameof(IsSub3Active));
            this.RaisePropertyChanged(nameof(IsSub4Active));
            this.RaisePropertyChanged(nameof(IsSub5Active));
            this.RaisePropertyChanged(nameof(IsSub6Active));
            this.RaisePropertyChanged(nameof(IsSub7Active));
            this.RaisePropertyChanged(nameof(IsSub8Active));
            this.RaisePropertyChanged(nameof(OffBg));
            this.RaisePropertyChanged(nameof(OffFg));
            this.RaisePropertyChanged(nameof(OffBorder));
            this.RaisePropertyChanged(nameof(BlueBg));
            this.RaisePropertyChanged(nameof(BlueFg));
            this.RaisePropertyChanged(nameof(BlueBorder));
            this.RaisePropertyChanged(nameof(RedBg));
            this.RaisePropertyChanged(nameof(RedFg));
            this.RaisePropertyChanged(nameof(RedBorder));
            this.RaisePropertyChanged(nameof(GreenBg));
            this.RaisePropertyChanged(nameof(GreenFg));
            this.RaisePropertyChanged(nameof(GreenBorder));
            this.RaisePropertyChanged(nameof(YellowBg));
            this.RaisePropertyChanged(nameof(YellowFg));
            this.RaisePropertyChanged(nameof(YellowBorder));
            this.RaisePropertyChanged(nameof(Sub1Bg));
            this.RaisePropertyChanged(nameof(Sub1Fg));
            this.RaisePropertyChanged(nameof(Sub2Bg));
            this.RaisePropertyChanged(nameof(Sub2Fg));
            this.RaisePropertyChanged(nameof(Sub3Bg));
            this.RaisePropertyChanged(nameof(Sub3Fg));
            this.RaisePropertyChanged(nameof(Sub4Bg));
            this.RaisePropertyChanged(nameof(Sub4Fg));
            this.RaisePropertyChanged(nameof(Sub5Bg));
            this.RaisePropertyChanged(nameof(Sub5Fg));
            this.RaisePropertyChanged(nameof(Sub6Bg));
            this.RaisePropertyChanged(nameof(Sub6Fg));
            this.RaisePropertyChanged(nameof(Sub7Bg));
            this.RaisePropertyChanged(nameof(Sub7Fg));
            this.RaisePropertyChanged(nameof(Sub8Bg));
            this.RaisePropertyChanged(nameof(Sub8Fg));
        }
    }

    public string SelectedPaintAreaName => StageKitAreaOption.GetDisplayName(_selectedPaintAreaIndex);

    public bool IsOffSelected => _selectedPaintAreaIndex < 0;
    public bool IsBlueSelected => _selectedPaintAreaIndex >= 0 && _selectedPaintAreaIndex <= 7;
    public bool IsRedSelected => _selectedPaintAreaIndex >= 8 && _selectedPaintAreaIndex <= 15;
    public bool IsGreenSelected => _selectedPaintAreaIndex >= 16 && _selectedPaintAreaIndex <= 23;
    public bool IsYellowSelected => _selectedPaintAreaIndex >= 24 && _selectedPaintAreaIndex <= 31;
    public bool HasSubGroup => !IsOffSelected;

    public string OffBg => IsOffSelected ? "#FFFFFF" : "#111325";
    public string OffFg => IsOffSelected ? "#000000" : "#94A3B8";
    public string OffBorder => IsOffSelected ? "#FFFFFF" : "#2B2F4E";

    public string BlueBg => IsBlueSelected ? "#FFFFFF" : "#1E3A8A";
    public string BlueFg => IsBlueSelected ? "#000000" : "#93C5FD";
    public string BlueBorder => IsBlueSelected ? "#FFFFFF" : "#3B82F6";

    public string RedBg => IsRedSelected ? "#FFFFFF" : "#881337";
    public string RedFg => IsRedSelected ? "#000000" : "#FCA5A5";
    public string RedBorder => IsRedSelected ? "#FFFFFF" : "#EF4444";

    public string GreenBg => IsGreenSelected ? "#FFFFFF" : "#064E3B";
    public string GreenFg => IsGreenSelected ? "#000000" : "#6EE7B7";
    public string GreenBorder => IsGreenSelected ? "#FFFFFF" : "#10B981";

    public string YellowBg => IsYellowSelected ? "#FFFFFF" : "#78350F";
    public string YellowFg => IsYellowSelected ? "#000000" : "#FDE047";
    public string YellowBorder => IsYellowSelected ? "#FFFFFF" : "#F59E0B";

    public string SubBarBorderColor => _selectedPaintAreaIndex switch
    {
        >= 0 and <= 7 => "#3B82F6",
        >= 8 and <= 15 => "#EF4444",
        >= 16 and <= 23 => "#10B981",
        >= 24 and <= 31 => "#F59E0B",
        _ => "#252A4A"
    };

    public int ActiveSubChannelNumber => _selectedPaintAreaIndex switch
    {
        >= 0 and <= 7 => _selectedPaintAreaIndex + 1,
        >= 8 and <= 15 => _selectedPaintAreaIndex - 7,
        >= 16 and <= 23 => _selectedPaintAreaIndex - 15,
        >= 24 and <= 31 => _selectedPaintAreaIndex - 23,
        _ => 0
    };

    public bool IsSub1Active => ActiveSubChannelNumber == 1;
    public bool IsSub2Active => ActiveSubChannelNumber == 2;
    public bool IsSub3Active => ActiveSubChannelNumber == 3;
    public bool IsSub4Active => ActiveSubChannelNumber == 4;
    public bool IsSub5Active => ActiveSubChannelNumber == 5;
    public bool IsSub6Active => ActiveSubChannelNumber == 6;
    public bool IsSub7Active => ActiveSubChannelNumber == 7;
    public bool IsSub8Active => ActiveSubChannelNumber == 8;

    public string Sub1Bg => IsSub1Active ? "#FFFFFF" : "#111325";
    public string Sub1Fg => IsSub1Active ? "#000000" : SubBarBorderColor;
    public string Sub2Bg => IsSub2Active ? "#FFFFFF" : "#111325";
    public string Sub2Fg => IsSub2Active ? "#000000" : SubBarBorderColor;
    public string Sub3Bg => IsSub3Active ? "#FFFFFF" : "#111325";
    public string Sub3Fg => IsSub3Active ? "#000000" : SubBarBorderColor;
    public string Sub4Bg => IsSub4Active ? "#FFFFFF" : "#111325";
    public string Sub4Fg => IsSub4Active ? "#000000" : SubBarBorderColor;
    public string Sub5Bg => IsSub5Active ? "#FFFFFF" : "#111325";
    public string Sub5Fg => IsSub5Active ? "#000000" : SubBarBorderColor;
    public string Sub6Bg => IsSub6Active ? "#FFFFFF" : "#111325";
    public string Sub6Fg => IsSub6Active ? "#000000" : SubBarBorderColor;
    public string Sub7Bg => IsSub7Active ? "#FFFFFF" : "#111325";
    public string Sub7Fg => IsSub7Active ? "#000000" : SubBarBorderColor;
    public string Sub8Bg => IsSub8Active ? "#FFFFFF" : "#111325";
    public string Sub8Fg => IsSub8Active ? "#000000" : SubBarBorderColor;

    public ObservableCollection<MatrixRowViewModel> MatrixRows { get; } = new();
    public ObservableCollection<MatrixCellViewModel> LinearCells { get; } = new();

    private bool _isMatrixLayout;
    public bool IsMatrixLayout
    {
        get => _isMatrixLayout;
        set => this.RaiseAndSetIfChanged(ref _isMatrixLayout, value);
    }

    public ICommand ToggleCustomizeLedsCommand { get; }
    public ICommand SetDefaultCommand { get; }
    public ICommand AutoDivideCommand { get; }
    public ICommand AutoSequentialCommand { get; }
    public ICommand SetAllBlueCommand { get; }
    public ICommand SetAllRedCommand { get; }
    public ICommand SetAllGreenCommand { get; }
    public ICommand SetAllYellowCommand { get; }
    public ICommand SelectPaintColorCommand { get; }
    public ICommand SelectPaintGroupCommand { get; }
    public ICommand SelectSubChannelCommand { get; }
    public ICommand PaintCellCommand { get; }

    public DeviceZoneCategory(Device device, Zone zone, int zoneIndex, int initialCategory, MainWindowViewModel? viewModel = null)
    {
        Device = device;
        Zone = zone;
        ZoneIndex = zoneIndex;
        _category = initialCategory;
        _viewModel = viewModel;
        IdentifyZoneCommand = ReactiveCommand.Create(() => _viewModel?.OpenRgbTalker.IdentifyZone(Device, ZoneIndex, Zone));

        ToggleCustomizeLedsCommand = ReactiveCommand.Create(() => IsCustomizingLeds = !IsCustomizingLeds);
        SetDefaultCommand = ReactiveCommand.Create(ApplyStageKitRadialDefault);
        AutoDivideCommand = ReactiveCommand.Create(ApplyAutoDivideInterleaved);
        AutoSequentialCommand = ReactiveCommand.Create(ApplyAutoSequential);
        SetAllBlueCommand = ReactiveCommand.Create(() => ApplyColorPreset(0));
        SetAllRedCommand = ReactiveCommand.Create(() => ApplyColorPreset(8));
        SetAllGreenCommand = ReactiveCommand.Create(() => ApplyColorPreset(16));
        SetAllYellowCommand = ReactiveCommand.Create(() => ApplyColorPreset(24));
        SelectPaintColorCommand = ReactiveCommand.Create<object>(param =>
        {
            if (param is int iVal)
                SelectedPaintAreaIndex = iVal;
            else if (param != null && int.TryParse(param.ToString(), out int parsedVal))
                SelectedPaintAreaIndex = parsedVal;
        });
        SelectPaintGroupCommand = ReactiveCommand.Create<string>(group => SelectPaintGroup(group));
        SelectSubChannelCommand = ReactiveCommand.Create<object>(param => SelectSubChannel(param));
        PaintCellCommand = ReactiveCommand.Create<MatrixCellViewModel>(cell => PaintCell(cell));

        InitializeLedItems();
        UpdateDeviceCategoryList(_category);
    }

    public void SelectPaintGroup(string group)
    {
        switch (group?.ToUpperInvariant())
        {
            case "OFF":
                SelectedPaintAreaIndex = -1;
                break;
            case "BLUE":
                SelectedPaintAreaIndex = 0;
                break;
            case "RED":
                SelectedPaintAreaIndex = 8;
                break;
            case "GREEN":
                SelectedPaintAreaIndex = 16;
                break;
            case "YELLOW":
                SelectedPaintAreaIndex = 24;
                break;
        }
    }

    public void SelectSubChannel(object param)
    {
        if (param != null && int.TryParse(param.ToString(), out int subNum) && subNum >= 1 && subNum <= 8)
        {
            int offset = subNum - 1;
            if (IsBlueSelected) SelectedPaintAreaIndex = 0 + offset;
            else if (IsRedSelected) SelectedPaintAreaIndex = 8 + offset;
            else if (IsGreenSelected) SelectedPaintAreaIndex = 16 + offset;
            else if (IsYellowSelected) SelectedPaintAreaIndex = 24 + offset;
        }
    }

    public void PaintCell(MatrixCellViewModel cell)
    {
        if (cell == null || !cell.HasKey) return;
        cell.AreaIndex = SelectedPaintAreaIndex;
        SaveCustomLedMapping();
    }

    private void InitializeLedItems()
    {
        MatrixRows.Clear();
        LinearCells.Clear();
        int ledCount = (int)Zone.LedCount;
        int numAreas = 32;
        int start = OpenRgbTalker.GetZoneStartLedIndex(Device, ZoneIndex);

        if (Zone.MatrixMap != null && Zone.MatrixMap.Height > 0 && Zone.MatrixMap.Width > 0 && Zone.MatrixMap.Matrix != null)
        {
            IsMatrixLayout = true;
            var map = Zone.MatrixMap;
            int totalKeys = 0;
            for (uint r = 0; r < map.Height; r++)
                for (uint c = 0; c < map.Width; c++)
                    if (map.Matrix[r, c] != uint.MaxValue) totalKeys++;

            bool[,] visited = new bool[map.Height, map.Width];
            int keyCounter = 0;
            for (uint r = 0; r < map.Height; r++)
            {
                var rowVm = new MatrixRowViewModel(r);
                for (uint c = 0; c < map.Width; c++)
                {
                    if (visited[r, c]) continue;

                    uint val = map.Matrix[r, c];
                    int ledIdx = (val != uint.MaxValue) ? (int)val : -1;
                    int colSpan = 1;
                    int rowSpan = 1;

                    if (ledIdx >= 0)
                    {
                        while (c + colSpan < map.Width && map.Matrix[r, c + colSpan] == val)
                        {
                            visited[r, c + colSpan] = true;
                            colSpan++;
                        }

                        if (r + 1 < map.Height && map.Matrix[r + 1, c] == val)
                        {
                            visited[r + 1, c] = true;
                            rowSpan = 2;
                        }
                    }

                    int devLedIndex = (ledIdx >= 0) ? start + ledIdx : -1;
                    string ledName = (ledIdx >= 0 && Device.Leds != null && devLedIndex >= 0 && devLedIndex < Device.Leds.Length) ? Device.Leds[devLedIndex].Name : string.Empty;

                    var cell = new MatrixCellViewModel(r, c, ledIdx, ledName, -1, colSpan, rowSpan, SaveCustomLedMapping);
                    rowVm.Cells.Add(cell);

                    c += (uint)(colSpan - 1);
                }
                MatrixRows.Add(rowVm);
            }
        }
        else
        {
            IsMatrixLayout = false;
            for (int i = 0; i < ledCount; i++)
            {
                int devLedIndex = start + i;
                string ledName = (Device.Leds != null && devLedIndex < Device.Leds.Length) ? Device.Leds[devLedIndex].Name : $"LED #{i + 1}";

                var cell = new MatrixCellViewModel(0, (uint)i, i, ledName, -1, 1, 1, SaveCustomLedMapping);
                LinearCells.Add(cell);
            }
        }
        ApplyStageKitRadialDefault();
    }

    public void SaveCustomLedMapping()
    {
        if (_viewModel == null) return;
        var key = GetZoneKey();

        if (IsMatrixLayout)
        {
            int maxLed = -1;
            foreach (var row in MatrixRows)
            {
                foreach (var cell in row.Cells)
                {
                    if (cell.LedIndex > maxLed) maxLed = cell.LedIndex;
                }
            }

            if (maxLed < 0) return;
            int[] mapping = new int[maxLed + 1];
            Array.Fill(mapping, -1);
            foreach (var row in MatrixRows)
            {
                foreach (var cell in row.Cells)
                {
                    if (cell.LedIndex >= 0 && cell.LedIndex < mapping.Length)
                    {
                        mapping[cell.LedIndex] = cell.AreaIndex;
                    }
                }
            }
            lock (_viewModel.OpenRgbTalker.Lock)
            {
                _viewModel.OpenRgbTalker.CustomLedMappings[key] = mapping;
            }
        }
        else
        {
            var mapping = LinearCells.Select(c => c.AreaIndex).ToArray();
            lock (_viewModel.OpenRgbTalker.Lock)
            {
                _viewModel.OpenRgbTalker.CustomLedMappings[key] = mapping;
            }
        }
        _viewModel.OpenRgbTalker.ResetLightPodColors();
    }

    public void ApplyStageKitRadialDefault()
    {
        if (IsMatrixLayout)
        {
            var validCells = new List<(MatrixCellViewModel cell, double dist, double angle)>();

            double minRow = double.MaxValue, maxRow = double.MinValue;
            double minCol = double.MaxValue, maxCol = double.MinValue;

            foreach (var r in MatrixRows)
            {
                foreach (var c in r.Cells)
                {
                    if (c.HasKey)
                    {
                        if (c.Row < minRow) minRow = c.Row;
                        if (c.Row > maxRow) maxRow = c.Row;
                        if (c.Column < minCol) minCol = c.Column;
                        if (c.Column > maxCol) maxCol = c.Column;
                    }
                }
            }

            double centerRow = (minRow + maxRow) / 2.0;
            double centerCol = (minCol + maxCol) / 2.0;

            foreach (var r in MatrixRows)
            {
                foreach (var c in r.Cells)
                {
                    if (c.HasKey)
                    {
                        double dr = c.Row - centerRow;
                        double dc = c.Column - centerCol;
                        double dist = Math.Sqrt(dr * dr + dc * dc);
                        double angle = Math.Atan2(dr, dc);
                        validCells.Add((c, dist, angle));
                    }
                }
            }

            if (validCells.Count > 0)
            {
                var ring0 = new List<(MatrixCellViewModel cell, double angle)>(); // Blue (0..7)
                var ring1 = new List<(MatrixCellViewModel cell, double angle)>(); // Green (16..23)
                var ring2 = new List<(MatrixCellViewModel cell, double angle)>(); // Red (8..15)
                var ring3 = new List<(MatrixCellViewModel cell, double angle)>(); // Yellow (24..31)

                var sortedByDist = validCells.OrderBy(x => x.dist).ToList();
                int count = sortedByDist.Count;

                int q0 = count / 4;
                int q1 = count / 2;
                int q2 = (count * 3) / 4;

                for (int i = 0; i < count; i++)
                {
                    var item = sortedByDist[i];
                    if (i < q0) ring0.Add((item.cell, item.angle));
                    else if (i < q1) ring1.Add((item.cell, item.angle));
                    else if (i < q2) ring2.Add((item.cell, item.angle));
                    else ring3.Add((item.cell, item.angle));
                }

                AssignRingSubChannels(ring0, 0);   // Blue 1..8
                AssignRingSubChannels(ring1, 16);  // Green 1..8
                AssignRingSubChannels(ring2, 8);   // Red 1..8
                AssignRingSubChannels(ring3, 24);  // Yellow 1..8
            }
        }
        else
        {
            ApplyAutoDivideInterleaved();
        }

        SaveCustomLedMapping();
    }

    private static void AssignRingSubChannels(List<(MatrixCellViewModel cell, double angle)> ring, int baseAreaOffset)
    {
        if (ring.Count == 0) return;

        var sortedRing = ring.OrderBy(x => x.angle).ToList();

        for (int i = 0; i < sortedRing.Count; i++)
        {
            int subChannel = (int)Math.Floor((double)i * 8.0 / sortedRing.Count);
            if (subChannel > 7) subChannel = 7;
            sortedRing[i].cell.AreaIndex = baseAreaOffset + subChannel;
        }
    }

    public void ApplyAutoDivideInterleaved()
    {
        int totalCells = IsMatrixLayout
            ? MatrixRows.Sum(r => r.Cells.Count(c => c.HasKey))
            : LinearCells.Count;

        if (totalCells == 0) return;

        const int numColors = 4;
        const int itemsPerColor = 8;
        int ledsPerColor = (int)Math.Ceiling((double)totalCells / numColors);

        Random rng = new Random();
        var colorIndices = new Dictionary<int, List<int>>();

        for (int color = 0; color < numColors; color++)
        {
            if (ledsPerColor >= itemsPerColor)
            {
                // Each sub-channel (0..7) used equally as possible
                var indices = new List<int>();
                int baseRep = ledsPerColor / itemsPerColor;
                int remRep = ledsPerColor % itemsPerColor;

                for (int sub = 0; sub < itemsPerColor; sub++)
                {
                    int rep = baseRep + (sub < remRep ? 1 : 0);
                    for (int r = 0; r < rep; r++) indices.Add(sub);
                }

                colorIndices[color] = indices.OrderBy(_ => rng.Next()).ToList();
            }
            else
            {
                // Pick 'ledsPerColor' unique indices randomly out of 0..7, then sort to preserve B1->B8 order
                colorIndices[color] = Enumerable.Range(0, itemsPerColor)
                                                .OrderBy(x => rng.Next())
                                                .Take(ledsPerColor)
                                                .OrderBy(x => x)
                                                .ToList();
            }
        }

        // Apply indices to the layout
        if (IsMatrixLayout)
        {
            int count = 0;
            foreach (var r in MatrixRows)
            {
                foreach (var c in r.Cells)
                {
                    if (!c.HasKey) continue;

                    int group = count % numColors;
                    int colorPos = (count / numColors) % colorIndices[group].Count;

                    int subIndex = colorIndices[group][colorPos];
                    c.AreaIndex = (group * itemsPerColor) + subIndex;

                    count++;
                }
            }
        }
        else
        {
            for (int i = 0; i < totalCells; i++)
            {
                int group = i % numColors;
                int colorPos = (i / numColors) % colorIndices[group].Count;

                int subIndex = colorIndices[group][colorPos];
                LinearCells[i].AreaIndex = (group * itemsPerColor) + subIndex;
            }
        }

        SaveCustomLedMapping();
    }

    public void ApplyAutoSequential()
    {
        int totalCells = IsMatrixLayout
            ? MatrixRows.Sum(r => r.Cells.Count(c => c.HasKey))
            : LinearCells.Count;

        if (totalCells == 0) return;

        if (IsMatrixLayout)
        {
            int count = 0;
            foreach (var r in MatrixRows)
            {
                foreach (var c in r.Cells)
                {
                    if (!c.HasKey) continue;
                    int areaIndex = (int)Math.Floor((double)count * 32.0 / totalCells);
                    if (areaIndex > 31) areaIndex = 31;
                    c.AreaIndex = areaIndex;
                    count++;
                }
            }
        }
        else
        {
            for (int i = 0; i < totalCells; i++)
            {
                int areaIndex = (int)Math.Floor((double)i * 32.0 / totalCells);
                if (areaIndex > 31) areaIndex = 31;
                LinearCells[i].AreaIndex = areaIndex;
            }
        }

        SaveCustomLedMapping();
    }

    private void ApplyColorPreset(int baseAreaOffset)
    {
        if (IsMatrixLayout)
        {
            int count = 0;
            foreach (var r in MatrixRows)
            {
                foreach (var c in r.Cells)
                {
                    if (!c.HasKey) continue;
                    c.AreaIndex = baseAreaOffset + (count % 8);
                    count++;
                }
            }
        }
        else
        {
            for (int i = 0; i < LinearCells.Count; i++)
            {
                LinearCells[i].AreaIndex = baseAreaOffset + (i % 8);
            }
        }
        SaveCustomLedMapping();
    }

    public void SetViewModel(MainWindowViewModel viewModel)
    {
        _viewModel = viewModel;
        SaveCustomLedMapping();
        UpdateDeviceCategoryList(_category);
    }

    protected virtual void OnPropertyChanged(string propertyName)
    {
        this.RaisePropertyChanged(propertyName);
    }
}
