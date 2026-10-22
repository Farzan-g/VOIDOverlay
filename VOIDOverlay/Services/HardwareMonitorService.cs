using System.Diagnostics;
using System.Globalization;
using System.Security.Principal;
using LibreHardwareMonitor.Hardware;
using VOIDOverlay.Models;

namespace VOIDOverlay.Services;

public interface IHardwareMonitorService : IDisposable
{
    event EventHandler<HardwareReading>? ReadingUpdated;
    HardwareReading GetLatestReading();
    void Start(int intervalMs = 1000);
    void Stop();
    IReadOnlyList<SensorStatus> GetAvailableSensors();
}

public sealed class HardwareMonitorService : IHardwareMonitorService
{
    private readonly Computer _computer;
    private readonly object _pollLock = new();
    private readonly System.Timers.Timer _timer = new(1000) { AutoReset = true, Enabled = false };
    private readonly bool _diagnosticsEnabled;
    private HardwareReading _latest = new();
    private string? _selectedCpuSensor;
    private string? _selectedGpuSensor;
    private bool _isRunning;
    private bool _isInitializing;
    private bool _startRequested;
    private int _pollInProgress;

    public HardwareMonitorService(bool diagnosticsEnabled = false)
    {
        _diagnosticsEnabled = diagnosticsEnabled;
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsMotherboardEnabled = true,
            IsControllerEnabled = false,
            IsNetworkEnabled = false,
            IsStorageEnabled = false
        };
        _timer.Elapsed += (_, _) => Poll();
    }

    public event EventHandler<HardwareReading>? ReadingUpdated;

    public HardwareReading GetLatestReading()
    {
        lock (_pollLock)
        {
            return _latest;
        }
    }

    public void Start(int intervalMs = 1000)
    {
        var interval = Math.Clamp(intervalMs, 250, 10_000);
        lock (_pollLock)
        {
            if (_isRunning && Math.Abs(_timer.Interval - interval) < 0.1)
            {
                return;
            }

            _timer.Stop();
            _timer.Interval = interval;
            if (_isRunning)
            {
                _timer.Start();
                return;
            }

            if (_isInitializing)
            {
                return;
            }

            _startRequested = true;
            _isInitializing = true;
        }

        _ = Task.Run(InitializeAndStartPolling);
    }

    public void Stop()
    {
        lock (_pollLock)
        {
            _startRequested = false;
            _timer.Stop();
            if (!_isRunning)
            {
                return;
            }

            _computer.Close();
            _isRunning = false;
        }
    }

    public IReadOnlyList<SensorStatus> GetAvailableSensors()
    {
        lock (_pollLock)
        {
            return EnumerateSensorsCore().ToList();
        }
    }

    public void Dispose()
    {
        Stop();
        _timer.Dispose();
    }

    private void Poll()
    {
        if (Interlocked.Exchange(ref _pollInProgress, 1) != 0)
        {
            return;
        }

        try
        {
            HardwareReading reading;
            try
            {
                lock (_pollLock)
                {
                    if (!_isRunning)
                    {
                        return;
                    }

                    var updateVisitor = new UpdateVisitor();
                    _computer.Accept(updateVisitor);
                    reading = BuildReading(updateVisitor.FailedHardwareIds);
                    _latest = reading;
                }

                Trace.WriteLine(
                    $"Hardware sample {reading.TimestampUtc:O}: CPU={FormatDiagnosticValue(reading.CpuTemperatureC)}, " +
                    $"GPU={FormatDiagnosticValue(reading.GpuTemperatureC)}.");
            }
            catch (Exception exception)
            {
                Trace.WriteLine($"Hardware polling failed; the monitor will retry next cycle: {exception}");
                var unavailableReading = new HardwareReading { TimestampUtc = DateTime.UtcNow };
                lock (_pollLock)
                {
                    _latest = unavailableReading;
                }

                try
                {
                    ReadingUpdated?.Invoke(this, unavailableReading);
                }
                catch (Exception subscriberException)
                {
                    Trace.WriteLine($"A hardware-reading subscriber failed: {subscriberException}");
                }

                return;
            }

            try
            {
                ReadingUpdated?.Invoke(this, reading);
            }
            catch (Exception exception)
            {
                Trace.WriteLine($"A hardware-reading subscriber failed: {exception}");
            }
        }
        finally
        {
            Volatile.Write(ref _pollInProgress, 0);
        }
    }

    private void InitializeAndStartPolling()
    {
        try
        {
            lock (_pollLock)
            {
                if (!_startRequested)
                {
                    _isInitializing = false;
                    return;
                }

                _computer.Open();
                _isRunning = true;
                _isInitializing = false;
                Trace.WriteLine(
                    $"LibreHardwareMonitor {typeof(Computer).Assembly.GetName().Version}; " +
                    $"process elevated={IsProcessElevated()}.");
            }
        }
        catch (Exception exception)
        {
            lock (_pollLock)
            {
                if (_isRunning)
                {
                    try
                    {
                        _computer.Close();
                    }
                    catch (Exception closeException)
                    {
                        Trace.WriteLine($"Hardware monitor cleanup after initialization failure also failed: {closeException}");
                    }
                }

                _isRunning = false;
                _isInitializing = false;
            }

            Trace.WriteLine($"Hardware monitor failed to initialize: {exception}");
            return;
        }

        Poll();
        lock (_pollLock)
        {
            if (_isRunning)
            {
                LogDiscoveredSensors();
            }

            if (_isRunning && _startRequested)
            {
                _timer.Start();
            }
        }
    }

    private HardwareReading BuildReading(IReadOnlySet<string> failedHardwareIds)
    {
        var reading = new HardwareReading { TimestampUtc = DateTime.UtcNow };
        var allHardware = GetHardwareTree().ToArray();
        var cpuHardware = allHardware.Where(IsCpuDevice).ToArray();
        var updatedCpuHardware = cpuHardware
            .Where(hardware => !failedHardwareIds.Contains(hardware.Identifier.ToString()))
            .ToArray();
        var gpuHardware = allHardware
            .Where(hardware => IsGpu(hardware.HardwareType) && !failedHardwareIds.Contains(hardware.Identifier.ToString()))
            .OrderBy(hardware => hardware.HardwareType == HardwareType.GpuNvidia ? 0 : 1)
            .ToArray();

        var cpuCandidates = CreateCpuCandidates(allHardware, failedHardwareIds);
        var cpuTemperature = CpuTemperatureSelector.Select(cpuCandidates);
        var gpuTemperature = SelectGpuTemperature(gpuHardware);
        LogCpuSelection(cpuTemperature, cpuCandidates, failedHardwareIds);
        LogSelection(ref _selectedGpuSensor, "GPU", gpuTemperature);
        reading.CpuTemperatureC = cpuTemperature?.Value;
        reading.GpuTemperatureC = ValidValue(gpuTemperature?.Sensor.Value);

        var cpuLoad = updatedCpuHardware.SelectMany(GetSensors)
            .Where(sensor => sensor.SensorType == SensorType.Load)
            .Select(sensor => ValidValue(sensor.Value))
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();
        var gpuSensors = gpuHardware.SelectMany(GetSensors).ToArray();
        var gpuLoad = gpuSensors.Where(sensor => sensor.SensorType == SensorType.Load)
            .Select(sensor => ValidValue(sensor.Value))
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();
        var cpuClocks = updatedCpuHardware.SelectMany(GetSensors)
            .Where(sensor => sensor.SensorType == SensorType.Clock)
            .Select(sensor => ValidValue(sensor.Value))
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();
        var gpuClocks = gpuSensors.Where(sensor => sensor.SensorType == SensorType.Clock)
            .Select(sensor => ValidValue(sensor.Value))
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();

        if (cpuLoad.Length > 0) reading.CpuLoadPercent = cpuLoad.Average();
        if (gpuLoad.Length > 0) reading.GpuLoadPercent = gpuLoad.Average();
        if (cpuClocks.Length > 0) reading.CpuClockGHz = cpuClocks.Max() / 1000.0;
        if (gpuClocks.Length > 0) reading.GpuClockMHz = gpuClocks.Max();

        SetMemoryValues(reading);
        return reading;
    }

    private static bool IsCpuDevice(IHardware hardware) =>
        hardware.HardwareType == HardwareType.Cpu ||
        hardware.Name.Contains("CPU", StringComparison.OrdinalIgnoreCase) ||
        hardware.Name.Contains("Ryzen", StringComparison.OrdinalIgnoreCase) ||
        hardware.Name.Contains("Intel Core", StringComparison.OrdinalIgnoreCase);

    private static CpuTemperatureCandidate[] CreateCpuCandidates(
        IEnumerable<IHardware> hardwareDevices,
        IReadOnlySet<string>? failedHardwareIds = null)
    {
        return hardwareDevices
            .SelectMany(hardware => GetSensors(hardware)
                .Where(sensor => sensor.SensorType == SensorType.Temperature ||
                                 hardware.HardwareType is HardwareType.Cpu or HardwareType.Motherboard)
                .Select(sensor => new CpuTemperatureCandidate(
                    hardware.Name,
                    hardware.HardwareType.ToString(),
                    sensor.SensorType.ToString(),
                    sensor.Name,
                    sensor.Identifier.ToString(),
                    failedHardwareIds?.Contains(hardware.Identifier.ToString()) == true
                        ? null
                        : ValidValue(sensor.Value))))
            .ToArray();
    }

    private (IHardware Hardware, ISensor Sensor)? SelectGpuTemperature(IEnumerable<IHardware> hardwareDevices)
    {
        foreach (var hardware in hardwareDevices)
        {
            var sensors = GetSensors(hardware)
                .Where(IsValidTemperature)
                .Where(IsGpuCoreTemperature)
                .ToArray();
            var selected = sensors
                .OrderByDescending(sensor => HasAny(sensor, "gpu core", "gpu temperature"))
                .ThenBy(sensor => sensor.Index)
                .FirstOrDefault();
            if (selected is not null)
            {
                return (hardware, selected);
            }
        }

        return null;
    }

    private void SetMemoryValues(HardwareReading reading)
    {
        var memorySensors = GetHardwareTree()
            .Where(hardware => hardware.HardwareType == HardwareType.Memory)
            .SelectMany(GetSensors)
            .Where(sensor => sensor.SensorType is SensorType.Data or SensorType.SmallData)
            .ToArray();
        var used = memorySensors.FirstOrDefault(sensor => sensor.Name.Contains("Used", StringComparison.OrdinalIgnoreCase));
        var total = memorySensors.FirstOrDefault(sensor =>
            sensor.Name.Contains("Total", StringComparison.OrdinalIgnoreCase) ||
            sensor.Name.Contains("Available", StringComparison.OrdinalIgnoreCase));

        var usedValue = ValidValue(used?.Value);
        var totalValue = ValidValue(total?.Value);
        if (usedValue.HasValue && totalValue.HasValue)
        {
            reading.RamUsedGb = usedValue.Value / 1024.0;
            reading.RamTotalGb = totalValue.Value / 1024.0;
        }
    }

    private void LogDiscoveredSensors()
    {
        foreach (var hardware in GetHardwareTree())
        {
            Trace.WriteLine($"Hardware discovered: {hardware.HardwareType} '{hardware.Name}' ({hardware.Identifier}).");
            foreach (var sensor in GetSensors(hardware))
            {
                Trace.WriteLine(
                    $"  Sensor: type={sensor.SensorType}, name='{sensor.Name}', id={sensor.Identifier}, " +
                    $"value={FormatDiagnosticValue(ValidValue(sensor.Value))}.");
            }
        }

        LogCpuDiagnostics(GetHardwareTree().ToArray(), CreateCpuCandidates(GetHardwareTree()), null);
    }

    private IEnumerable<SensorStatus> EnumerateSensorsCore()
    {
        foreach (var hardware in GetHardwareTree())
        {
            foreach (var sensor in GetSensors(hardware))
            {
                var value = ValidValue(sensor.Value);
                yield return new SensorStatus
                {
                    Name = $"{hardware.Name} / {sensor.Name}",
                    Value = value?.ToString("0.##", CultureInfo.InvariantCulture) ?? "N/A",
                    IsAvailable = value.HasValue,
                    Details = $"{sensor.SensorType} ({sensor.Identifier})"
                };
            }
        }
    }

    private static IEnumerable<ISensor> GetSensors(IHardware hardware)
    {
        foreach (var sensor in hardware.Sensors)
        {
            yield return sensor;
        }
    }

    private IEnumerable<IHardware> GetHardwareTree()
    {
        foreach (var hardware in _computer.Hardware)
        {
            foreach (var nested in EnumerateHardware(hardware))
                yield return nested;
        }
    }

    private static IEnumerable<IHardware> EnumerateHardware(IHardware hardware)
    {
        yield return hardware;
        foreach (var child in hardware.SubHardware)
        {
            foreach (var nested in EnumerateHardware(child))
                yield return nested;
        }
    }

    private static bool IsGpu(HardwareType type) =>
        type is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel;

    private static bool IsValidTemperature(ISensor sensor) =>
        sensor.SensorType == SensorType.Temperature &&
        ValidValue(sensor.Value) is >= -50 and <= 200;

    private static bool IsGpuCoreTemperature(ISensor sensor)
    {
        if (HasAny(sensor, "hot spot", "hotspot", "junction", "memory", "vram", "board"))
        {
            return false;
        }

        return HasAny(sensor, "gpu core", "gpu temperature", "core", "temperature") ||
               sensor.Identifier.ToString().Contains("/temperature/0", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasAny(ISensor sensor, params string[] terms) =>
        terms.Any(term => sensor.Name.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static double? ValidValue(float? value) =>
        value.HasValue && float.IsFinite(value.Value) ? value.Value : null;

    private static string FormatDiagnosticValue(double? value) =>
        value?.ToString("0.0", CultureInfo.InvariantCulture) ?? "N/A";

    private static bool IsProcessElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static void LogSelection(ref string? previous, string label, (IHardware Hardware, ISensor Sensor)? selection)
    {
        var current = selection is null
            ? "unavailable"
            : $"{selection.Value.Hardware.Name} / {selection.Value.Sensor.Name} " +
              $"[{selection.Value.Sensor.Identifier}]";
        if (!string.Equals(previous, current, StringComparison.Ordinal))
        {
            Trace.WriteLine($"{label} temperature sensor selected: {current}.");
            previous = current;
        }
    }

    private void LogCpuSelection(
        CpuTemperatureCandidate? selection,
        IReadOnlyList<CpuTemperatureCandidate> candidates,
        IReadOnlySet<string> failedHardwareIds)
    {
        var current = selection is null
            ? "unavailable"
            : $"{selection.HardwareName} / {selection.SensorName} [{selection.Identifier}]";
        if (!string.Equals(_selectedCpuSensor, current, StringComparison.Ordinal))
        {
            Trace.WriteLine($"CPU temperature sensor selected: {current}.");
            if (selection is not null)
            {
                Trace.WriteLine("CPU selection policy: package, then die/Tctl/Tdie, then core aggregate, then core; no readings are averaged.");
            }
            _selectedCpuSensor = current;
            LogCpuDiagnostics(GetHardwareTree().ToArray(), candidates, selection, failedHardwareIds);
        }

        if (_diagnosticsEnabled)
        {
            LogCpuDiagnostics(GetHardwareTree().ToArray(), candidates, selection, failedHardwareIds);
        }
    }

    private static void LogCpuDiagnostics(
        IReadOnlyList<IHardware> devices,
        IReadOnlyList<CpuTemperatureCandidate> candidates,
        CpuTemperatureCandidate? selected,
        IReadOnlySet<string>? failedHardwareIds = null)
    {
        var cpuDevices = devices.Where(IsCpuDevice).ToArray();
        var motherboardDevices = devices.Where(hardware => hardware.HardwareType == HardwareType.Motherboard).ToArray();
        Trace.WriteLine(
            $"CPU temperature diagnostic: cpuDevices={cpuDevices.Length}, motherboardDevices={motherboardDevices.Length}, " +
            $"temperatureCandidates={candidates.Count(candidate => candidate.SensorType == SensorType.Temperature.ToString())}, " +
            $"readableCpuCandidates={candidates.Count(candidate => CpuTemperatureSelector.GetRejectionReason(candidate) is null)}, " +
            $"selected={(selected is null ? "none" : $"{selected.SensorName} [{selected.Identifier}]")}.");

        foreach (var device in cpuDevices.Concat(motherboardDevices).Distinct())
        {
            Trace.WriteLine($"CPU-related hardware: name='{device.Name}', type={device.HardwareType}, id={device.Identifier}.");
            var updateFailed = failedHardwareIds?.Contains(device.Identifier.ToString()) == true;
            foreach (var sensor in GetSensors(device))
            {
                var value = ValidValue(sensor.Value);
                var candidate = new CpuTemperatureCandidate(
                    device.Name,
                    device.HardwareType.ToString(),
                    sensor.SensorType.ToString(),
                    sensor.Name,
                    sensor.Identifier.ToString(),
                    value);
                var state = updateFailed
                    ? "hardware update failed; current measurement unavailable"
                    : CpuTemperatureSelector.GetRejectionReason(candidate) ?? "eligible CPU temperature";
                Trace.WriteLine(
                    $"CPU sensor: hardware='{device.Name}', hardwareType={device.HardwareType}, " +
                    $"sensorType={sensor.SensorType}, id={sensor.Identifier}, name='{sensor.Name}', " +
                    $"value={(updateFailed ? "N/A (not refreshed)" : FormatDiagnosticValue(value))}, decision={state}.");
            }
        }

        if (cpuDevices.Length == 0 && motherboardDevices.Length == 0)
        {
            Trace.WriteLine("CPU temperature diagnostic: no CPU or motherboard hardware was discovered.");
        }
    }

    private sealed class UpdateVisitor : IVisitor
    {
        public HashSet<string> FailedHardwareIds { get; } = new(StringComparer.OrdinalIgnoreCase);

        public void VisitComputer(IComputer computer)
        {
            foreach (var hardware in computer.Hardware)
            {
                VisitHardware(hardware);
            }
        }

        public void VisitHardware(IHardware hardware)
        {
            try
            {
                hardware.Update();
            }
            catch (Exception exception)
            {
                FailedHardwareIds.Add(hardware.Identifier.ToString());
                Trace.WriteLine(
                    $"Hardware update failed for '{hardware.Name}' (type={hardware.HardwareType}, " +
                    $"id={hardware.Identifier}): {exception}");
            }

            foreach (var subHardware in hardware.SubHardware)
            {
                VisitHardware(subHardware);
            }
        }

        public void VisitSensor(ISensor sensor)
        {
        }

        public void VisitParameter(IParameter parameter)
        {
        }
    }

}
