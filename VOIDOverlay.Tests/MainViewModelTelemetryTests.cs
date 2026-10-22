using VOIDOverlay.Models;
using VOIDOverlay.Services;
using VOIDOverlay.ViewModels;

namespace VOIDOverlay.Tests;

public sealed class MainViewModelTelemetryTests
{
    [Fact]
    public void ChangingCpuAndGpuReadingsUpdatesValuesAndMatchingGraphHistory()
    {
        using var fixture = new ViewModelFixture();
        var notifications = new List<string>();
        fixture.ViewModel.PropertyChanged += (_, args) => notifications.Add(args.PropertyName ?? string.Empty);

        fixture.Monitor.Publish(new HardwareReading
        {
            CpuTemperatureC = 54.5,
            GpuTemperatureC = 61.25,
            TimestampUtc = DateTime.UtcNow
        });
        fixture.Monitor.Publish(new HardwareReading
        {
            CpuTemperatureC = 57.5,
            GpuTemperatureC = 63.25,
            TimestampUtc = DateTime.UtcNow
        });

        Assert.Equal(57.5, fixture.ViewModel.CpuTemperatureC);
        Assert.Equal(63.25, fixture.ViewModel.GpuTemperatureC);
        Assert.Equal("57.5°C", fixture.ViewModel.CpuTemperatureText);
        Assert.Equal("63.3°C", fixture.ViewModel.GpuTemperatureText);
        Assert.Equal(new double?[] { 54.5, 57.5 }, fixture.ViewModel.CpuHistory);
        Assert.Equal(new double?[] { 61.25, 63.25 }, fixture.ViewModel.GpuHistory);
        Assert.Contains(nameof(MainViewModel.CpuTemperatureText), notifications);
        Assert.Contains(nameof(MainViewModel.GpuTemperatureText), notifications);
        Assert.Contains(nameof(MainViewModel.CpuHistory), notifications);
        Assert.Contains(nameof(MainViewModel.GpuHistory), notifications);
    }

    [Fact]
    public void CpuTelemetryUpdatesWhenGpuTemperatureIsUnavailable()
    {
        using var fixture = new ViewModelFixture();
        fixture.Monitor.Publish(new HardwareReading { CpuTemperatureC = 50, GpuTemperatureC = null });
        fixture.Monitor.Publish(new HardwareReading { CpuTemperatureC = 55, GpuTemperatureC = null });

        Assert.Equal("55.0°C", fixture.ViewModel.CpuTemperatureText);
        Assert.Equal("N/A", fixture.ViewModel.GpuTemperatureText);
        Assert.Equal(new double?[] { 50, 55 }, fixture.ViewModel.CpuHistory);
        Assert.Equal(new double?[] { null, null }, fixture.ViewModel.GpuHistory);
    }

    [Fact]
    public void GpuTelemetryUpdatesWhenCpuTemperatureIsUnavailable()
    {
        using var fixture = new ViewModelFixture();
        fixture.Monitor.Publish(new HardwareReading { CpuTemperatureC = null, GpuTemperatureC = 60 });
        fixture.Monitor.Publish(new HardwareReading { CpuTemperatureC = null, GpuTemperatureC = 65 });

        Assert.Equal("N/A", fixture.ViewModel.CpuTemperatureText);
        Assert.Equal("65.0°C", fixture.ViewModel.GpuTemperatureText);
        Assert.Equal(new double?[] { null, null }, fixture.ViewModel.CpuHistory);
        Assert.Equal(new double?[] { 60, 65 }, fixture.ViewModel.GpuHistory);
    }

    [Fact]
    public void UnavailableAndInvalidReadingsRemainVisibleAsNaAndDoNotAddGraphSamples()
    {
        using var fixture = new ViewModelFixture();

        fixture.Monitor.Publish(new HardwareReading
        {
            CpuTemperatureC = double.PositiveInfinity,
            GpuTemperatureC = null,
            TimestampUtc = DateTime.UtcNow
        });

        Assert.Equal("N/A", fixture.ViewModel.CpuTemperatureText);
        Assert.Equal("N/A", fixture.ViewModel.GpuTemperatureText);
        Assert.Equal(new double?[] { null }, fixture.ViewModel.CpuHistory);
        Assert.Equal(new double?[] { null }, fixture.ViewModel.GpuHistory);
    }

    [Fact]
    public void SamplingUsesConfiguredIntervalAndHistoryRemainsBounded()
    {
        using var fixture = new ViewModelFixture(new OverlaySettings
        {
            RefreshIntervalMs = 750,
            HistorySeconds = 1
        });

        Assert.Equal(750, fixture.Monitor.LastStartedInterval);
        fixture.Monitor.Publish(new HardwareReading { CpuTemperatureC = 40 });
        fixture.Monitor.Publish(new HardwareReading { CpuTemperatureC = 41 });
        fixture.Monitor.Publish(new HardwareReading { CpuTemperatureC = 42 });

        Assert.Equal(new double?[] { 41d, 42d }, fixture.ViewModel.CpuHistory);
    }

    [Fact]
    public void RepeatedSamplingKeepsHistoryBoundedAndUnavailablePeriodsAsGaps()
    {
        using var fixture = new ViewModelFixture(new OverlaySettings
        {
            RefreshIntervalMs = 1000,
            HistorySeconds = 2
        });

        for (var index = 0; index < 500; index++)
        {
            fixture.Monitor.Publish(new HardwareReading
            {
                CpuTemperatureC = index % 2 == 0 ? index : null,
                GpuTemperatureC = 60 + index
            });
        }

        Assert.Equal(2, fixture.ViewModel.CpuHistory.Count);
        Assert.Equal(new double?[] { 498d, null }, fixture.ViewModel.CpuHistory);
        Assert.Equal(2, fixture.ViewModel.GpuHistory.Count);
        Assert.Equal(new double?[] { 558d, 559d }, fixture.ViewModel.GpuHistory);
    }

    private sealed class ViewModelFixture : IDisposable
    {
        private readonly string _settingsPath = Path.Combine(
            Path.GetTempPath(),
            $"void-overlay-test-{Guid.NewGuid():N}",
            "settings.json");

        public ViewModelFixture(OverlaySettings? settings = null)
        {
            Monitor = new FakeHardwareMonitorService();
            var settingsService = new SettingsService(_settingsPath);
            if (settings is not null)
            {
                settingsService.Save(settings);
            }

            ViewModel = new MainViewModel(Monitor, settingsService);
        }

        public FakeHardwareMonitorService Monitor { get; }
        public MainViewModel ViewModel { get; }

        public void Dispose()
        {
            ViewModel.Dispose();
            var folder = Path.GetDirectoryName(_settingsPath);
            if (folder is not null && Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    private sealed class FakeHardwareMonitorService : IHardwareMonitorService
    {
        private HardwareReading _latest = new();

        public event EventHandler<HardwareReading>? ReadingUpdated;
        public int LastStartedInterval { get; private set; }

        public HardwareReading GetLatestReading() => _latest;

        public void Start(int intervalMs = 1000) => LastStartedInterval = intervalMs;

        public void Stop()
        {
        }

        public IReadOnlyList<SensorStatus> GetAvailableSensors() => Array.Empty<SensorStatus>();

        public void Publish(HardwareReading reading)
        {
            _latest = reading;
            ReadingUpdated?.Invoke(this, reading);
        }

        public void Dispose()
        {
        }
    }
}
