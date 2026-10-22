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

        const double edgeMarginDip = 10;
        var baseLeft = position switch
        {
            OverlayPosition.TopLeft or OverlayPosition.BottomLeft => left + edgeMarginDip,
            OverlayPosition.TopCenter or OverlayPosition.BottomCenter => (left + right - overlayWidthDip) / 2,
            OverlayPosition.TopRight or OverlayPosition.BottomRight => right - overlayWidthDip - edgeMarginDip,
            _ => throw new ArgumentOutOfRangeException(nameof(position))
        };
        var baseTop = position switch
        {
            OverlayPosition.TopLeft or OverlayPosition.TopCenter or OverlayPosition.TopRight => top + edgeMarginDip,
            OverlayPosition.BottomLeft or OverlayPosition.BottomCenter or OverlayPosition.BottomRight => bottom - overlayHeightDip - edgeMarginDip,
            _ => throw new ArgumentOutOfRangeException(nameof(position))
        };
        var maxLeft = Math.Max(left, right - overlayWidthDip);
        var maxTop = Math.Max(top, bottom - overlayHeightDip);
        return new(
            Math.Clamp(baseLeft + horizontalOffsetDip, left, maxLeft),
            Math.Clamp(baseTop + verticalOffsetDip, top, maxTop));
    }
}
