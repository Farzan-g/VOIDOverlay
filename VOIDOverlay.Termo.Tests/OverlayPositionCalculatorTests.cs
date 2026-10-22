using VOIDOverlay.Models;
using VOIDOverlay.Services;

namespace VOIDOverlay.Tests;

public sealed class OverlayPositionCalculatorTests
{
    public static IEnumerable<object[]> MonitorScenarios()
    {
        yield return [new PixelBounds(0, 0, 1920, 1080), 1d, 1d, 180d, 42d, 16d, 24d];
        yield return [new PixelBounds(0, 0, 1280, 720), 1d, 1d, 150d, 48d, 0d, 36d];
        yield return [new PixelBounds(1920, 0, 3840, 2100), 1.5d, 1.5d, 200d, 50d, 55d, 16d];
        yield return [new PixelBounds(-2560, -160, 0, 1280), 1.25d, 1.25d, 175d, 44d, 20d, 70d];
    }

    [Theory]
    [MemberData(nameof(MonitorScenarios))]
    public void AllCornersApplyIndependentDpiAwareOffsets(
        PixelBounds bounds,
        double dpiScaleX,
        double dpiScaleY,
        double overlayWidthDip,
        double overlayHeightDip,
        double horizontalOffsetDip,
        double verticalOffsetDip)
    {
        var left = bounds.Left / dpiScaleX;
        var top = bounds.Top / dpiScaleY;
        var right = bounds.Right / dpiScaleX;
        var bottom = bounds.Bottom / dpiScaleY;

        foreach (var position in Enum.GetValues<OverlayPosition>())
        {
            var location = Calculate(bounds, dpiScaleX, dpiScaleY, overlayWidthDip, overlayHeightDip,
                horizontalOffsetDip, verticalOffsetDip, position);

            var expectedLeft = position is OverlayPosition.TopLeft or OverlayPosition.BottomLeft
                ? left + horizontalOffsetDip
                : right - overlayWidthDip - horizontalOffsetDip;
            var expectedTop = position is OverlayPosition.TopLeft or OverlayPosition.TopRight
                ? top + verticalOffsetDip
                : bottom - overlayHeightDip - verticalOffsetDip;

            Assert.Equal(expectedLeft, location.Left, 6);
            Assert.Equal(expectedTop, location.Top, 6);

            var leftPx = location.Left * dpiScaleX;
            var topPx = location.Top * dpiScaleY;
            var rightPx = leftPx + overlayWidthDip * dpiScaleX;
            var bottomPx = topPx + overlayHeightDip * dpiScaleY;
            Assert.InRange(leftPx, bounds.Left - 0.001, bounds.Right + 0.001);
            Assert.InRange(rightPx, bounds.Left - 0.001, bounds.Right + 0.001);
            Assert.InRange(topPx, bounds.Top - 0.001, bounds.Bottom + 0.001);
            Assert.InRange(bottomPx, bounds.Top - 0.001, bounds.Bottom + 0.001);
        }
    }

    [Theory]
    [InlineData(OverlayPosition.TopLeft)]
    [InlineData(OverlayPosition.TopRight)]
    [InlineData(OverlayPosition.BottomLeft)]
    [InlineData(OverlayPosition.BottomRight)]
    public void ZeroOffsetsPlaceOverlayExactlyAgainstSelectedEdges(OverlayPosition position)
    {
        var bounds = new PixelBounds(0, 0, 1920, 1080);
        const double width = 180;
        const double height = 42;

        var location = Calculate(bounds, 1, 1, width, height, 0, 0, position);

        if (position is OverlayPosition.TopLeft or OverlayPosition.BottomLeft)
            Assert.Equal(bounds.Left, location.Left);
        else
            Assert.Equal(bounds.Right - width, location.Left);

        if (position is OverlayPosition.TopLeft or OverlayPosition.TopRight)
            Assert.Equal(bounds.Top, location.Top);
        else
            Assert.Equal(bounds.Bottom - height, location.Top);
    }

    [Theory]
    [InlineData(OverlayEdgeReference.FullScreen)]
    [InlineData(OverlayEdgeReference.WorkArea)]
    public void SelectsTheRequestedScreenEdgeBounds(OverlayEdgeReference edgeReference)
    {
        var fullScreen = new PixelBounds(0, 0, 1920, 1080);
        var workArea = new PixelBounds(0, 0, 1920, 1040);

        var selected = OverlayPositionCalculator.SelectEdgeBounds(fullScreen, workArea, edgeReference);

        Assert.Equal(
            edgeReference == OverlayEdgeReference.FullScreen ? fullScreen : workArea,
            selected);
    }

    [Theory]
    [InlineData(OverlayPosition.TopLeft, -10, -20, -10, -20)]
    [InlineData(OverlayPosition.TopRight, -10, -20, -10, -20)]
    [InlineData(OverlayPosition.BottomLeft, -10, -20, -10, -20)]
    [InlineData(OverlayPosition.BottomRight, -10, -20, -10, -20)]
    public void NegativeOffsetsMovePredictablyOutsideTheSelectedEdges(
        OverlayPosition position,
        double horizontalOffset,
        double verticalOffset,
        double expectedHorizontalEdgeDistance,
        double expectedVerticalEdgeDistance)
    {
        var bounds = new PixelBounds(0, 0, 1000, 800);
        const double width = 100;
        const double height = 40;

        var location = Calculate(bounds, 1, 1, width, height, horizontalOffset, verticalOffset, position);
        var actualHorizontalEdgeDistance = position is OverlayPosition.TopLeft or OverlayPosition.BottomLeft
            ? location.Left - bounds.Left
            : bounds.Right - (location.Left + width);
        var actualVerticalEdgeDistance = position is OverlayPosition.TopLeft or OverlayPosition.TopRight
            ? location.Top - bounds.Top
            : bounds.Bottom - (location.Top + height);

        Assert.Equal(expectedHorizontalEdgeDistance, actualHorizontalEdgeDistance);
        Assert.Equal(expectedVerticalEdgeDistance, actualVerticalEdgeDistance);
    }

    [Fact]
    public void RejectsInvalidDpiScaleAndNonFiniteOffsets()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Calculate(new PixelBounds(0, 0, 1000, 800), 0, 1, 180, 42, 0, 0, OverlayPosition.TopLeft));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Calculate(new PixelBounds(0, 0, 1000, 800), 1, 1, 180, 42, double.NaN, 0, OverlayPosition.TopLeft));
    }

    private static OverlayLocation Calculate(
        PixelBounds bounds,
        double dpiScaleX,
        double dpiScaleY,
        double width,
        double height,
        double x,
        double y,
        OverlayPosition position) =>
        OverlayPositionCalculator.Calculate(bounds, dpiScaleX, dpiScaleY, width, height, x, y, position);
}
