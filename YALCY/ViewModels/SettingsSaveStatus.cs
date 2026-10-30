using System;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Input;
using Newtonsoft.Json;
using ReactiveUI;
using YALCY.Diagnostics;
using YALCY.Integrations.WLED;

namespace YALCY.ViewModels;

public partial class MainWindowViewModel
{
    private string? _savedSettingsFingerprint;
    private bool _hasUnsavedChanges;
    private bool _hasSaveError;
    private string _saveStatus = "No unsaved changes";
    private string _saveDetail = "Settings also save when you quit YALCY. Hiding to the tray does not save.";
    public bool HasUnsavedChanges { get => _hasUnsavedChanges; private set => this.RaiseAndSetIfChanged(ref _hasUnsavedChanges, value); }
    public bool HasSaveError { get => _hasSaveError; private set => this.RaiseAndSetIfChanged(ref _hasSaveError, value); }
    public string SaveStatus { get => _saveStatus; private set => this.RaiseAndSetIfChanged(ref _saveStatus, value); }
    public string SaveDetail { get => _saveDetail; private set => this.RaiseAndSetIfChanged(ref _saveDetail, value); }
    public ICommand SaveSettingsCommand { get; private set; } = null!;

    private void InitializeSettingsSaveStatus()
    {
        _savedSettingsFingerprint = SettingsFingerprint();
        SaveSettingsCommand = ReactiveCommand.Create(() => SaveSettingsWithFeedback(() => SettingsManager.SaveSettings(this)));
    }

    // Compare the same persisted values used by saving, including nested mappings and
    // WLED's separate file. Live device status, previews, and selection do not count.
    private string SettingsFingerprint()
    {
        var json = JsonConvert.SerializeObject(new
        {
            Main = SettingsManager.CaptureSettings(this), Wled = WledSettingsManager.Capture(this)
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }

    internal void RefreshSettingsSaveStatus()
    {
        if (_savedSettingsFingerprint == null) return;
        bool dirty;
        try { dirty = SettingsFingerprint() != _savedSettingsFingerprint; }
        catch
        {
            HasUnsavedChanges = true;
            HasSaveError = true;
            SaveStatus = "Settings need attention";
            SaveDetail = "Some settings could not be read. Review your entries, then try Save settings.";
            return;
        }
        if (HasUnsavedChanges == dirty) return;
        HasUnsavedChanges = dirty;
        if (!HasSaveError) SaveStatus = dirty ? "Unsaved changes" : "No unsaved changes";
    }

    internal void MarkSettingsSaved()
    {
        _savedSettingsFingerprint = SettingsFingerprint();
        HasUnsavedChanges = false;
        HasSaveError = false;
        SaveStatus = $"Settings saved at {DateTime.Now:t}";
        SaveDetail = "Settings also save when you quit YALCY. Hiding to the tray does not save.";
    }

    internal void SaveSettingsWithFeedback(Action save)
    {
        try
        {
            save();
            MarkSettingsSaved();
            AppLog.Write(LogLevel.Information, "Settings", "Settings saved successfully.");
        }
        catch (Exception ex)
        {
            HasSaveError = true;
            RefreshSettingsSaveStatus();
            SaveStatus = "Settings could not be saved";
            SaveDetail = "Your changes remain in this session. Check disk space and settings-folder access, then try Save settings again.";
            AppLog.Write(LogLevel.Error, "Settings", $"Could not save settings ({ex.GetType().Name}).");
        }
    }
}
