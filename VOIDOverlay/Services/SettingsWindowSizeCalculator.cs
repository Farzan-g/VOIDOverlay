namespace VOIDOverlay.Services;

public readonly record struct SettingsWindowSize(
    double Width,
    double Height,
    double MinWidth,
    double MinHeight,
    double MaxWidth,
    double MaxHeight);

public static class SettingsWindowSizeCalculator
{
    public static SettingsWindowSize Calculate(double workAreaWidthPixels, double workAreaHeightPixels, double dpiScale)
    {
        if (!double.IsFinite(dpiScale) || dpiScale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dpiScale), "DPI scale must be finite and positive.");
        }

        var availableWidth = workAreaWidthPixels / dpiScale;
        var availableHeight = workAreaHeightPixels / dpiScale;
        return new SettingsWindowSize(
            Width: Math.Min(380, availableWidth),
            Height: Math.Min(460, availableHeight),
            MinWidth: Math.Min(320, availableWidth),
            MinHeight: Math.Min(280, availableHeight),
            MaxWidth: availableWidth,
            MaxHeight: availableHeight);
    }
}
