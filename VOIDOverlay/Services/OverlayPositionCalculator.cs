using VOIDOverlay.Models;

namespace VOIDOverlay.Services;

public readonly record struct PixelBounds(double Left, double Top, double Right, double Bottom);
public readonly record struct OverlayLocation(double Left, double Top);

public static class OverlayPositionCalculator
{
    public static PixelBounds SelectEdgeBounds(
        PixelBounds fullScreenBounds,
        PixelBounds workAreaBounds,
        OverlayEdgeReference edgeReference) =>
        edgeReference switch
        {
            OverlayEdgeReference.FullScreen => fullScreenBounds,
            OverlayEdgeReference.WorkArea => workAreaBounds,
            _ => throw new ArgumentOutOfRangeException(nameof(edgeReference))
        };

    public static OverlayLocation Calculate(
        PixelBounds monitorBounds,
        double dpiScaleX,
        double dpiScaleY,
        double overlayWidthDip,
        double overlayHeightDip,
        double horizontalOffsetDip,
        double verticalOffsetDip,
        OverlayPosition position)
    {
        if (!double.IsFinite(dpiScaleX) || dpiScaleX <= 0 ||
            !double.IsFinite(dpiScaleY) || dpiScaleY <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dpiScaleX), "DPI scales must be finite and positive.");
        }

        if (!double.IsFinite(horizontalOffsetDip) || !double.IsFinite(verticalOffsetDip))
        {
            throw new ArgumentOutOfRangeException(nameof(horizontalOffsetDip), "Offsets must be finite values.");
        }

        var left = monitorBounds.Left / dpiScaleX;
        var top = monitorBounds.Top / dpiScaleY;
        var right = monitorBounds.Right / dpiScaleX;
        var bottom = monitorBounds.Bottom / dpiScaleY;

        return position switch
        {
            OverlayPosition.TopLeft => new(left + horizontalOffsetDip, top + verticalOffsetDip),
            OverlayPosition.TopRight => new(right - overlayWidthDip - horizontalOffsetDip, top + verticalOffsetDip),
            OverlayPosition.BottomLeft => new(left + horizontalOffsetDip, bottom - overlayHeightDip - verticalOffsetDip),
            OverlayPosition.BottomRight => new(right - overlayWidthDip - horizontalOffsetDip, bottom - overlayHeightDip - verticalOffsetDip),
            _ => throw new ArgumentOutOfRangeException(nameof(position))
        };
    }
}
