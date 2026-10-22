using VOIDOverlay.Services;

namespace VOIDOverlay.Tests;

public sealed class CpuTemperatureSelectorTests
{
    [Fact]
    public void SelectPrefersCpuPackageSensor()
    {
        var candidates = new[]
        {
            Candidate("Intel Core", "Processor", "Core #0", "45"),
            Candidate("Intel Core", "Processor", "CPU Die", "52"),
            Candidate("Intel Core", "Processor", "CPU Package", "58"),
            Candidate("NVIDIA GPU", "GpuNvidia", "GPU Core", "65")
        };

        var selected = CpuTemperatureSelector.Select(candidates);

        Assert.NotNull(selected);
        Assert.Equal("CPU Package", selected.SensorName);
        Assert.Equal(58, selected.Value);
    }

    [Fact]
    public void SelectFallsBackToDieThenCoreWhenPackageIsUnavailable()
    {
        var candidates = new[]
        {
            Candidate("AMD Ryzen", "Processor", "Core #0", "45"),
            Candidate("AMD Ryzen", "Processor", "Tctl/Tdie", "52"),
            Candidate("AMD Ryzen", "Processor", "Package", null)
        };

        Assert.Equal("Tctl/Tdie", CpuTemperatureSelector.Select(candidates)?.SensorName);
    }

    [Fact]
    public void SelectUsesCoreWhenNoPackageOrDieIsReadable()
    {
        var candidates = new[]
        {
            Candidate("CPU", "Processor", "Core #1", "49"),
            Candidate("CPU", "Processor", "Core #0", "47"),
            Candidate("CPU", "Processor", "CPU Package", null),
            Candidate("NVIDIA GPU", "GpuNvidia", "GPU Core", "65")
        };

        Assert.Equal("Core #0", CpuTemperatureSelector.Select(candidates)?.SensorName);
    }

    [Fact]
    public void SelectAcceptsExplicitCpuSensorExposedByMotherboardProvider()
    {
        var candidates = new[]
        {
            Candidate("Nuvoton NCT6798", "Motherboard", "CPU", "52"),
            Candidate("Nuvoton NCT6798", "Motherboard", "System", "31"),
            Candidate("Nuvoton NCT6798", "Motherboard", "AUXTIN0", "35")
        };

        Assert.Equal("CPU", CpuTemperatureSelector.Select(candidates)?.SensorName);
        Assert.Equal(
            "hardware/sensor identity does not identify a CPU temperature source",
            CpuTemperatureSelector.GetRejectionReason(candidates[1]));
    }

    [Fact]
    public void SelectRejectsNonTemperatureAndInvalidValues()
    {
        var candidates = new[]
        {
            Candidate("CPU", "Cpu", "CPU Package", "NaN"),
            Candidate("CPU", "Cpu", "CPU Die", "220"),
            Candidate("CPU", "Cpu", "CPU Core", "51", sensorType: "Load")
        };

        Assert.Null(CpuTemperatureSelector.Select(candidates));
    }

    [Fact]
    public void UnreadableCpuSensorReportsProviderValueFailure()
    {
        var candidate = Candidate("AMD Ryzen", "Cpu", "CPU Package", null);

        Assert.Null(CpuTemperatureSelector.Select([candidate]));
        Assert.Equal("sensor value is unavailable or non-finite", CpuTemperatureSelector.GetRejectionReason(candidate));
    }

    private static CpuTemperatureCandidate Candidate(
        string hardwareName,
        string hardwareType,
        string sensorName,
        string? value,
        string sensorType = "Temperature")
    {
        return new CpuTemperatureCandidate(
            hardwareName,
            hardwareType,
            sensorType,
            sensorName,
            $"/{sensorName.Replace(' ', '-')}",
            value is null ? null : double.Parse(value, System.Globalization.CultureInfo.InvariantCulture));
    }
}
