namespace VOIDOverlay.Models;

public sealed class SensorStatus
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = "N/A";
    public bool IsAvailable { get; set; }
    public string? Details { get; set; }
}
