using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Controls.Shapes;
using Avalonia.Threading;
using System;
using System.Collections.Generic;

namespace YALCY.Views.Components;

public enum IntegrationStatus
{
    Off,        // Gray - disabled
    Connecting, // Yellow - connecting
    Connected,  // Green - active and connected
    Error       // Red - active but with problem
}

public partial class StatusFooter : UserControl
{
    // Static events that integrations can raise to update status colors
    public static event Action<string, string>? StatusColorChanged;
    
    // Dictionary to store current status colors
    private static readonly Dictionary<string, string> _statusColors = new()
    {
        { "UDP", "#808080" },
        { "StageKit", "#808080" },
        { "DMX", "#808080" },
        { "RB3E", "#808080" },
        { "Serial", "#808080" },
        { "Hue", "#808080" },
        { "OpenRGB", "#808080" }
    };

    // References to the status ellipses
    private Ellipse? _udpStatusEllipse;
    private Ellipse? _stageKitStatusEllipse;
    private Ellipse? _dmxStatusEllipse;
    private Ellipse? _rb3eStatusEllipse;
    private Ellipse? _serialStatusEllipse;
    private Ellipse? _hueStatusEllipse;
    private Ellipse? _openRgbStatusEllipse;

    public StatusFooter()
    {
        InitializeComponent();
        
        // Get references to the ellipses
        _udpStatusEllipse = this.FindControl<Ellipse>("UdpStatusEllipse");
        _stageKitStatusEllipse = this.FindControl<Ellipse>("StageKitStatusEllipse");
        _dmxStatusEllipse = this.FindControl<Ellipse>("DmxStatusEllipse");
        _rb3eStatusEllipse = this.FindControl<Ellipse>("Rb3eStatusEllipse");
        _serialStatusEllipse = this.FindControl<Ellipse>("SerialStatusEllipse");
        _hueStatusEllipse = this.FindControl<Ellipse>("HueStatusEllipse");
        _openRgbStatusEllipse = this.FindControl<Ellipse>("OpenRgbStatusEllipse");
        
        // Subscribe to status change events
        StatusColorChanged += OnStatusColorChanged;
        
        // Initialize with default colors
        UpdateAllStatusColors();
    }

    private void OnStatusColorChanged(string integrationName, string color)
    {
        // Verificar se a nova cor é diferente da atual para evitar mudanças desnecessárias
        if (_statusColors.TryGetValue(integrationName, out string currentColor) && currentColor == color)
        {
            return; // Cor já é a mesma, não precisa mudar
        }
        
        _statusColors[integrationName] = color;
        
        // Update the corresponding ellipse color on UI thread
        var ellipse = GetEllipseForIntegration(integrationName);
        if (ellipse != null)
        {
            // Only invoke if we're not already on UI thread
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    ellipse.Fill = new SolidColorBrush(Color.Parse(color));
                });
            }
            else
            {
                // We're already on UI thread, update directly
                ellipse.Fill = new SolidColorBrush(Color.Parse(color));
            }
        }
    }

    private Ellipse? GetEllipseForIntegration(string integrationName)
    {
        return integrationName.ToUpper() switch
        {
            "UDP" => _udpStatusEllipse,
            "STAGEKIT" => _stageKitStatusEllipse,
            "DMX" => _dmxStatusEllipse,
            "RB3E" => _rb3eStatusEllipse,
            "SERIAL" => _serialStatusEllipse,
            "HUE" => _hueStatusEllipse,
            "OPENRGB" => _openRgbStatusEllipse,
            _ => null
        };
    }

    private void UpdateAllStatusColors()
    {
        // Initialize all status colors to default (gray)
        foreach (var kvp in _statusColors)
        {
            StatusColorChanged?.Invoke(kvp.Key, kvp.Value);
        }
    }

    // Static methods that integrations can call directly
    public static void UpdateStatus(string integrationName, IntegrationStatus status)
    {
        string color = GetStatusColor(status);
        
        // Verificar se a nova cor é diferente da atual para evitar chamadas desnecessárias
        if (_statusColors.TryGetValue(integrationName, out string currentColor) && currentColor == color)
        {
            return; // Cor já é a mesma, não precisa mudar
        }
        
        StatusColorChanged?.Invoke(integrationName, color);
    }

    public static void ToggleStatus(string integrationName, bool isEnabled)
    {
        string color = GetStatusColor(isEnabled ? IntegrationStatus.Connecting : IntegrationStatus.Off);
        
        // Verificar se a nova cor é diferente da atual para evitar chamadas desnecessárias
        if (_statusColors.TryGetValue(integrationName, out string currentColor) && currentColor == color)
        {
            return; // Cor já é a mesma, não precisa mudar
        }
        
        StatusColorChanged?.Invoke(integrationName, color); 
    }

    public static void SetStatusColor(string integrationName, string color)
    {
        // Verificar se a nova cor é diferente da atual para evitar chamadas desnecessárias
        if (_statusColors.TryGetValue(integrationName, out string currentColor) && currentColor == color)
        {
            return; // Cor já é a mesma, não precisa mudar
        }
        
        StatusColorChanged?.Invoke(integrationName, color);
    }

    private static string GetStatusColor(IntegrationStatus status)
    {
        return status switch
        {
            IntegrationStatus.Off => "#808080",        // Gray - disabled
            IntegrationStatus.Connecting => "#FFFF00", // Yellow - connecting
            IntegrationStatus.Connected => "#00FF00",  // Green - active and connected
            IntegrationStatus.Error => "#FF0000",      // Red - active but with problem
            _ => "#808080"                            // Default to gray
        };
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        StatusColorChanged -= OnStatusColorChanged;
        base.OnDetachedFromVisualTree(e);
    }
}