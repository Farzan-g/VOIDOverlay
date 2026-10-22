using VOIDOverlay.Models;

namespace VOIDOverlay.Tests;

public class SettingsTests
{
    [Fact]
    public void OverlaySettings_ShouldDefaultToTopRightAndMinimalWindow()
    {
        var settings = new OverlaySettings();

        Assert.Equal(OverlayPosition.TopRight, settings.Position);
        Assert.Equal(180, settings.WindowWidth);
        Assert.Equal(42, settings.WindowHeight);
        Assert.Equal(1000, settings.RefreshIntervalMs);
        Assert.Equal(0, settings.HorizontalOffsetX);
        Assert.Equal(0, settings.VerticalOffsetY);
        Assert.Equal(OverlayEdgeReference.WorkArea, settings.EdgeReference);
        Assert.True(settings.IsLocked);
        Assert.True(settings.ClickThrough);
    }

    [Fact]
    public void SettingsService_PersistsIndependentOffsetsAndEdgeReference()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"void-overlay-settings-{Guid.NewGuid():N}");
        var path = Path.Combine(folder, "settings.json");
        try
        {
            var service = new VOIDOverlay.Services.SettingsService(path);
            service.Save(new OverlaySettings
            {
                Position = OverlayPosition.TopLeft,
                HorizontalOffsetX = -23.5,
                VerticalOffsetY = 123.25,
                EdgeReference = OverlayEdgeReference.FullScreen
            });

            var reloaded = service.Load();

            Assert.Equal(OverlayPosition.TopLeft, reloaded.Position);
            Assert.Equal(-23.5, reloaded.HorizontalOffsetX);
            Assert.Equal(123.25, reloaded.VerticalOffsetY);
            Assert.Equal(OverlayEdgeReference.FullScreen, reloaded.EdgeReference);
            Assert.DoesNotContain("EdgeMargin", File.ReadAllText(path));
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    [Fact]
    public void SettingsService_MigratesLegacyEdgeMarginToBothOffsets()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"void-overlay-settings-{Guid.NewGuid():N}");
        var path = Path.Combine(folder, "settings.json");
        try
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(path, """{"Position": 3, "EdgeMargin": 23}""");

            var reloaded = new VOIDOverlay.Services.SettingsService(path).Load();

            Assert.Equal(OverlayPosition.BottomRight, reloaded.Position);
            Assert.Equal(23, reloaded.HorizontalOffsetX);
            Assert.Equal(23, reloaded.VerticalOffsetY);
            Assert.Equal(OverlayEdgeReference.WorkArea, reloaded.EdgeReference);
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }
}
