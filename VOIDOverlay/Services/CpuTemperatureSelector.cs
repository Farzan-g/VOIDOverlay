namespace VOIDOverlay.Services;

internal sealed record CpuTemperatureCandidate(
    string HardwareName,
    string HardwareType,
    string SensorType,
    string SensorName,
    string Identifier,
    double? Value);

internal static class CpuTemperatureSelector
{
    public static CpuTemperatureCandidate? Select(IEnumerable<CpuTemperatureCandidate> candidates)
    {
        return candidates
            .Where(candidate => GetRejectionReason(candidate) is null)
            .OrderBy(candidate => GetSensorPriority(candidate.SensorName))
            .ThenByDescending(candidate => string.Equals(candidate.HardwareType, "Cpu", StringComparison.OrdinalIgnoreCase))
            .ThenBy(candidate => candidate.Identifier, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static int GetSensorPriority(string sensorName)
    {
        if (HasAny(sensorName, "package"))
        {
            return 0;
        }

        if (HasAny(sensorName, "tctl", "tdie", "die", "ccd"))
        {
            return 1;
        }

        if (HasAny(sensorName, "core max", "core average"))
        {
            return 2;
        }

        if (HasAny(sensorName, "core"))
        {
            return 3;
        }

        return 4;
    }

    public static string? GetRejectionReason(CpuTemperatureCandidate candidate)
    {
        if (!string.Equals(candidate.SensorType, "Temperature", StringComparison.OrdinalIgnoreCase))
        {
            return "sensor type is not Temperature";
        }

        if (!double.IsFinite(candidate.Value ?? double.NaN))
        {
            return "sensor value is unavailable or non-finite";
        }

        if (candidate.Value is < -50 or > 200)
        {
            return "sensor value is outside the plausible CPU temperature range";
        }

        if (string.Equals(candidate.HardwareType, "Cpu", StringComparison.OrdinalIgnoreCase) ||
            HasAny(candidate.HardwareName, "cpu", "ryzen", "intel core", "processor"))
        {
            return null;
        }

        if (string.Equals(candidate.HardwareType, "Motherboard", StringComparison.OrdinalIgnoreCase) &&
            HasAny(candidate.SensorName, "cpu", "processor", "package", "tctl", "tdie", "ccd"))
        {
            return null;
        }

        return "hardware/sensor identity does not identify a CPU temperature source";
    }

    private static bool HasAny(string value, params string[] terms) =>
        terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
}
