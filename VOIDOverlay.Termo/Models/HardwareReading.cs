namespace VOIDOverlay.Models;

public sealed class HardwareReading
{
    public double? CpuTemperatureC { get; set; }
    public double? GpuTemperatureC { get; set; }
    public double? CpuLoadPercent { get; set; }
    public double? GpuLoadPercent { get; set; }
    public double? CpuClockGHz { get; set; }
    public double? GpuClockMHz { get; set; }
    public double? RamUsedGb { get; set; }
    public double? RamTotalGb { get; set; }
    public double? VramUsedGb { get; set; }
    public double? VramTotalGb { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    public bool HasCpuTemperature => CpuTemperatureC.HasValue;
    public bool HasGpuTemperature => GpuTemperatureC.HasValue;
    public bool HasCpuLoad => CpuLoadPercent.HasValue;
    public bool HasGpuLoad => GpuLoadPercent.HasValue;
    public bool HasCpuClock => CpuClockGHz.HasValue;
    public bool HasGpuClock => GpuClockMHz.HasValue;
    public bool HasRam => RamUsedGb.HasValue && RamTotalGb.HasValue;
    public bool HasVram => VramUsedGb.HasValue && VramTotalGb.HasValue;
}
