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
    public void AllAnchorsApplyIndependentDpiAwareOffsets(
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

            var baseLeft = position switch
            {
                OverlayPosition.TopLeft or OverlayPosition.BottomLeft => left + 10,
                OverlayPosition.TopCenter or OverlayPosition.BottomCenter => (left + right - overlayWidthDip) / 2,
                _ => right - overlayWidthDip - 10
            };
            var baseTop = position is OverlayPosition.TopLeft or OverlayPosition.TopCenter or OverlayPosition.TopRight
                ? top + 10
                : bottom - overlayHeightDip - 10;
            var expectedLeft = Math.Clamp(baseLeft + horizontalOffsetDip, left, Math.Max(left, right - overlayWidthDip));
            var expectedTop = Math.Clamp(baseTop + verticalOffsetDip, top, Math.Max(top, bottom - overlayHeightDip));

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
    [MemberData(nameof(AllPositions))]
    public void ZeroOffsetsRespectTheSharedTenDipEdgeInset(OverlayPosition position)
    {
        var bounds = new PixelBounds(0, 0, 1920, 1080);
        const double width = 180;
        const double height = 42;

        var location = Calculate(bounds, 1, 1, width, height, 0, 0, position);

        if (position is OverlayPosition.TopLeft or OverlayPosition.BottomLeft)
            Assert.Equal(bounds.Left + 10, location.Left);
        else if (position is OverlayPosition.TopCenter or OverlayPosition.BottomCenter)
            Assert.Equal((bounds.Left + bounds.Right - width) / 2, location.Left);
        else
            Assert.Equal(bounds.Right - width - 10, location.Left);

        if (position is OverlayPosition.TopLeft or OverlayPosition.TopCenter or OverlayPosition.TopRight)
            Assert.Equal(bounds.Top + 10, location.Top);
        else
            Assert.Equal(bounds.Bottom - height - 10, location.Top);
    }

    public static IEnumerable<object[]> AllPositions() =>
        Enum.GetValues<OverlayPosition>().Select(position => new object[] { position });

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
    [MemberData(nameof(AllPositions))]
    public void PositiveOffsetsMoveRightAndDownFromEveryAnchor(OverlayPosition position)
    {
        var bounds = new PixelBounds(0, 0, 1000, 800);

        var baseline = Calculate(bounds, 1, 1, 100, 40, 0, 0, position);
        var offset = Calculate(bounds, 1, 1, 100, 40, 2, 3, position);

        Assert.Equal(2, offset.Left - baseline.Left);
        Assert.Equal(3, offset.Top - baseline.Top);
    }

    [Theory]
    [MemberData(nameof(AllPositions))]
    public void ExtremeOffsetsRemainVisibleWithinSelectedDisplay(OverlayPosition position)
    {
        var bounds = new PixelBounds(0, 0, 1000, 800);
        const double width = 100;
        const double height = 40;

        var location = Calculate(bounds, 1, 1, width, height, 1000, 1000, position);

        Assert.InRange(location.Left, bounds.Left, bounds.Right - width);
        Assert.InRange(location.Top, bounds.Top, bounds.Bottom - height);
    }

    [Fact]
    public void NegativeOffsetsMoveTowardTheLeftAndTopFromTheSelectedAnchor()
    {
        const double width = 100;
        const double height = 40;
        var bounds = new PixelBounds(0, 0, 1000, 800);

        var rightBottom = Calculate(bounds, 1, 1, width, height, 0, 0, OverlayPosition.BottomRight);
        var offset = Calculate(bounds, 1, 1, width, height, -20, -30, OverlayPosition.BottomRight);

        Assert.Equal(-20, offset.Left - rightBottom.Left);
        Assert.Equal(-30, offset.Top - rightBottom.Top);
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
