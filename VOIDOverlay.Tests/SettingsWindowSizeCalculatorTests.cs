using VOIDOverlay.Services;

namespace VOIDOverlay.Tests;

public sealed class SettingsWindowSizeCalculatorTests
{
    [Theory]
    [InlineData(1920, 1040, 1, 380, 460)]
    [InlineData(1600, 840, 1.25, 380, 460)]
    [InlineData(1200, 600, 1.5, 380, 400)]
    [InlineData(700, 480, 1.5, 380, 320)]
    public void WindowSizeFitsAvailableWorkAreaAtCommonDpiScales(
        double widthPixels,
        double heightPixels,
        double dpiScale,
        double expectedWidth,
        double expectedHeight)
    {
        var size = SettingsWindowSizeCalculator.Calculate(widthPixels, heightPixels, dpiScale);

        Assert.Equal(expectedWidth, size.Width);
        Assert.Equal(expectedHeight, size.Height);
        Assert.True(size.Width <= size.MaxWidth);
        Assert.True(size.Height <= size.MaxHeight);
        Assert.True(size.MinWidth <= size.Width);
        Assert.True(size.MinHeight <= size.Height);
    }

    [Fact]
    public void RejectsInvalidDpiScale()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SettingsWindowSizeCalculator.Calculate(1920, 1040, 0));
    }
}
