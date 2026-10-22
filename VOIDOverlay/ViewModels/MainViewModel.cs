using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VOIDOverlay.Models;
using VOIDOverlay.Services;

namespace VOIDOverlay.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IHardwareMonitorService _hardwareMonitor;
    private readonly SettingsService _settingsService;
    private readonly bool _diagnosticsEnabled;
    private readonly ObservableCollection<double?> _cpuHistory = new();
    private readonly ObservableCollection<double?> _gpuHistory = new();
    private OverlaySettings _settings;
    private double? _cpuTemperatureC;
    private double? _gpuTemperatureC;
    private double? _cpuLoadPercent;
    private double? _gpuLoadPercent;
    private double? _cpuClockGHz;
    private double? _gpuClockMHz;
    private DateTime _lastSampleUtc;

    public MainViewModel()
        : this(new HardwareMonitorService(), new SettingsService())
    {
    }

    public MainViewModel(
        IHardwareMonitorService hardwareMonitor,
        SettingsService settingsService,
        bool diagnosticsEnabled = false)
    {
        _hardwareMonitor = hardwareMonitor;
        _settingsService = settingsService;
        _diagnosticsEnabled = diagnosticsEnabled;
        _settings = _settingsService.Load();

        _hardwareMonitor.ReadingUpdated += OnReadingUpdated;
        ToggleClickThroughCommand = new RelayCommand(ToggleClickThrough);
        ToggleCompactModeCommand = new RelayCommand(ToggleCompactMode);
        RefreshCommand = new RelayCommand(RefreshNow);
        SaveSettingsCommand = new RelayCommand(SaveSettings);
        _hardwareMonitor.Start(_settings.RefreshIntervalMs);
    }

    public ObservableCollection<SensorStatus> Sensors { get; } = new();

    public OverlaySettings Settings
    {
        get => _settings;
        set => SetProperty(ref _settings, value);
    }

    public double? CpuTemperatureC { get => _cpuTemperatureC; private set => SetProperty(ref _cpuTemperatureC, value); }
    public double? GpuTemperatureC { get => _gpuTemperatureC; private set => SetProperty(ref _gpuTemperatureC, value); }
    public double? CpuLoadPercent { get => _cpuLoadPercent; private set => SetProperty(ref _cpuLoadPercent, value); }
    public double? GpuLoadPercent { get => _gpuLoadPercent; private set => SetProperty(ref _gpuLoadPercent, value); }
    public double? CpuClockGHz { get => _cpuClockGHz; private set => SetProperty(ref _cpuClockGHz, value); }
    public double? GpuClockMHz { get => _gpuClockMHz; private set => SetProperty(ref _gpuClockMHz, value); }
    public DateTime LastSampleUtc { get => _lastSampleUtc; private set => SetProperty(ref _lastSampleUtc, value); }

    public string CpuTemperatureText => FormatTemperature(CpuTemperatureC);
    public string GpuTemperatureText => FormatTemperature(GpuTemperatureC);
    public string CpuUsageText => FormatPercent(CpuLoadPercent);
    public string GpuUsageText => FormatPercent(GpuLoadPercent);
    public string CpuClockText => FormatClock(CpuClockGHz);
    public string GpuClockText => FormatClock(GpuClockMHz, isMHz: true);
    public string RamText => FormatRamUsage();
    public string VramText => FormatVramUsage();
    public bool IsCompactMode { get => Settings.IsCompactMode; set { if (Settings.IsCompactMode == value) return; Settings.IsCompactMode = value; OnPropertyChanged(); } }

    public RelayCommand ToggleClickThroughCommand { get; }
    public RelayCommand ToggleCompactModeCommand { get; }
    public RelayCommand RefreshCommand { get; }
    public RelayCommand SaveSettingsCommand { get; }

    public IReadOnlyList<double?> CpuHistory => _cpuHistory;
    public IReadOnlyList<double?> GpuHistory => _gpuHistory;

    public void ApplySettings(OverlaySettings settings)
    {
        Settings = settings;
        _hardwareMonitor.Start(Settings.RefreshIntervalMs);
        OnPropertyChanged(nameof(CpuTemperatureText));
        OnPropertyChanged(nameof(GpuTemperatureText));
        OnPropertyChanged(nameof(CpuUsageText));
        OnPropertyChanged(nameof(GpuUsageText));
        OnPropertyChanged(nameof(CpuClockText));
        OnPropertyChanged(nameof(GpuClockText));
        OnPropertyChanged(nameof(RamText));
        OnPropertyChanged(nameof(VramText));
    }

    public void SaveSettings()
    {
        _settingsService.Save(Settings);
    }

    public void Dispose()
    {
        _hardwareMonitor.ReadingUpdated -= OnReadingUpdated;
        _hardwareMonitor.Dispose();
    }

    private void OnReadingUpdated(object? sender, HardwareReading reading)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(new Action(() => ApplyReading(reading)));
            return;
        }

        ApplyReading(reading);
    }

    private void ApplyReading(HardwareReading reading)
    {
        CpuTemperatureC = reading.CpuTemperatureC;
        GpuTemperatureC = reading.GpuTemperatureC;
        CpuLoadPercent = reading.CpuLoadPercent;
        GpuLoadPercent = reading.GpuLoadPercent;
        CpuClockGHz = reading.CpuClockGHz;
        GpuClockMHz = reading.GpuClockMHz;
        LastSampleUtc = reading.TimestampUtc;

        _cpuHistory.Add(ValidTemperature(reading.CpuTemperatureC));
        _gpuHistory.Add(ValidTemperature(reading.GpuTemperatureC));

        var intervalMs = Math.Clamp(Settings.RefreshIntervalMs, 250, 10_000);
        var maxHistory = Math.Max(2, (int)Math.Ceiling(Settings.HistorySeconds * 1000d / intervalMs));
        TrimHistory(_cpuHistory, maxHistory);
        TrimHistory(_gpuHistory, maxHistory);

        OnPropertyChanged(nameof(CpuTemperatureText));
        OnPropertyChanged(nameof(GpuTemperatureText));
        OnPropertyChanged(nameof(CpuUsageText));
        OnPropertyChanged(nameof(GpuUsageText));
        OnPropertyChanged(nameof(CpuClockText));
        OnPropertyChanged(nameof(GpuClockText));
        OnPropertyChanged(nameof(RamText));
        OnPropertyChanged(nameof(VramText));
        OnPropertyChanged(nameof(LastSampleUtc));
        OnPropertyChanged(nameof(CpuHistory));
        OnPropertyChanged(nameof(GpuHistory));
        if (_diagnosticsEnabled)
        {
            Trace.WriteLine(
                $"ViewModel applied sample {reading.TimestampUtc:O}: CPU={CpuTemperatureText}, " +
                $"GPU={GpuTemperatureText}, CPU graph samples={_cpuHistory.Count}, GPU graph samples={_gpuHistory.Count}.");
        }
    }

    private static void TrimHistory(ObservableCollection<double?> history, int maxHistory)
    {
        while (history.Count > maxHistory)
        {
            history.RemoveAt(0);
        }
    }

    private static double? ValidTemperature(double? value) =>
        value.HasValue && double.IsFinite(value.Value) ? value : null;

    private void ToggleClickThrough()
    {
        Settings.ClickThrough = !Settings.ClickThrough;
        OnPropertyChanged(nameof(Settings));
    }

    private void ToggleCompactMode()
    {
        IsCompactMode = !IsCompactMode;
        OnPropertyChanged(nameof(IsCompactMode));
    }

    private void RefreshNow()
    {
        var reading = _hardwareMonitor.GetLatestReading();
        OnReadingUpdated(this, reading);

        Sensors.Clear();
        foreach (var sensor in _hardwareMonitor.GetAvailableSensors())
        {
            Sensors.Add(sensor);
        }
    }

    private static string FormatTemperature(double? value)
    {
        return !value.HasValue || !double.IsFinite(value.Value) ? "N/A" : $"{value.Value:0.0}°C";
    }

    private static string FormatPercent(double? value)
    {
        return !value.HasValue || !double.IsFinite(value.Value) ? "N/A" : $"{value.Value:0.0}%";
    }

    private static string FormatClock(double? value, bool isMHz = false)
    {
        if (!value.HasValue || !double.IsFinite(value.Value))
        {
            return "N/A";
        }

        return isMHz ? $"{value.Value:0.0} MHz" : $"{value.Value:0.0} GHz";
    }

    private string FormatRamUsage()
    {
        var reading = _hardwareMonitor.GetLatestReading();
        if (!reading.RamUsedGb.HasValue || !reading.RamTotalGb.HasValue)
        {
            return "RAM N/A";
        }

        return $"RAM {reading.RamUsedGb.Value:0.0}/{reading.RamTotalGb.Value:0.0} GB";
    }

    private string FormatVramUsage()
    {
        var reading = _hardwareMonitor.GetLatestReading();
        if (!reading.VramUsedGb.HasValue || !reading.VramTotalGb.HasValue)
        {
            return "VRAM N/A";
        }

        return $"VRAM {reading.VramUsedGb.Value:0.0}/{reading.VramTotalGb.Value:0.0} GB";
    }
}
