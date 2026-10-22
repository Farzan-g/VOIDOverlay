using System.Text.Json.Serialization;

namespace VOIDOverlay.Models;

public enum OverlayPosition
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight
}

public enum OverlayEdgeReference
{
    FullScreen,
    WorkArea
}

public sealed class OverlaySettings
{
    public double WindowLeft { get; set; } = 0;
    public double WindowTop { get; set; } = 0;
    public double WindowWidth { get; set; } = 180;
    public double WindowHeight { get; set; } = 42;
    public double Opacity { get; set; } = 0.96;
    public bool AlwaysOnTop { get; set; } = true;
    public bool ClickThrough { get; set; } = true;
    public bool IsCompactMode { get; set; } = true;
    public bool IsOverlayVisible { get; set; } = true;
    public bool IsLocked { get; set; } = true;
    public bool StartMinimized { get; set; } = false;
    public bool StartWithWindows { get; set; } = false;
    public int RefreshIntervalMs { get; set; } = 1000;
    public int HistorySeconds { get; set; } = 90;
    public double HorizontalOffsetX { get; set; }
    public double VerticalOffsetY { get; set; }
    public OverlayEdgeReference EdgeReference { get; set; } = OverlayEdgeReference.WorkArea;
    public OverlayPosition Position { get; set; } = OverlayPosition.TopRight;
    public string Theme { get; set; } = "Dark";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? EdgeMargin { get; set; }
}
