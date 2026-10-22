using System.Text.Json.Serialization;

namespace VOIDOverlay.Models;

public enum OverlayPosition
{
    TopLeft = 0,
    TopRight = 1,
    BottomLeft = 2,
    BottomRight = 3,
    TopCenter = 4,
    BottomCenter = 5
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
    public double WindowWidth { get; set; } = 280;
    public double WindowHeight { get; set; } = 62;
    public double FontSize { get; set; } = 13;
    public double Opacity { get; set; } = 1;
    public bool BackgroundEnabled { get; set; } = true;
    public string BackgroundColor { get; set; } = "#111720";
    public double BackgroundOpacity { get; set; } = 0.5;
    public string TextColor { get; set; } = "#F2F4F7";
    public string AccentColor { get; set; } = "#4DE3D2";
    public string SecondaryAccentColor { get; set; } = "#FFAE5C";
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
    public OverlayPosition Position { get; set; } = OverlayPosition.TopCenter;
    public string Theme { get; set; } = "Dark";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? EdgeMargin { get; set; }

    public void Validate()
    {
        if (!Enum.IsDefined(Position))
        {
            Position = OverlayPosition.TopCenter;
        }

        if (!Enum.IsDefined(EdgeReference))
        {
            EdgeReference = OverlayEdgeReference.WorkArea;
        }

        HorizontalOffsetX = Math.Clamp(double.IsFinite(HorizontalOffsetX) ? HorizontalOffsetX : 0, -200, 1000);
        VerticalOffsetY = Math.Clamp(double.IsFinite(VerticalOffsetY) ? VerticalOffsetY : 0, -200, 1000);
        RefreshIntervalMs = Math.Clamp(RefreshIntervalMs, 250, 10_000);
        WindowWidth = double.IsFinite(WindowWidth) ? Math.Clamp(WindowWidth, 180, 1000) : 280;
        WindowHeight = double.IsFinite(WindowHeight) ? Math.Clamp(WindowHeight, 42, 500) : 62;
        Opacity = double.IsFinite(Opacity) ? Math.Clamp(Opacity, 0.3, 1) : 1;
        BackgroundOpacity = double.IsFinite(BackgroundOpacity) ? Math.Clamp(BackgroundOpacity, 0, 1) : 0.5;
        FontSize = double.IsFinite(FontSize) ? Math.Clamp(FontSize, 9, 24) : 13;
        BackgroundColor = ValidColorOr(BackgroundColor, "#111720");
        TextColor = ValidColorOr(TextColor, "#F2F4F7");
        AccentColor = ValidColorOr(AccentColor, "#4DE3D2");
        SecondaryAccentColor = ValidColorOr(SecondaryAccentColor, "#FFAE5C");
    }

    private static string ValidColorOr(string? value, string fallback) =>
        value is { Length: 7 } &&
        value[0] == '#' &&
        value.Skip(1).All(Uri.IsHexDigit)
            ? value.ToUpperInvariant()
            : fallback;
}
