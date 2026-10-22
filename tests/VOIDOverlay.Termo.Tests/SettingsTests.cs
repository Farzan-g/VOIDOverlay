using VOIDOverlay.Models;

namespace VOIDOverlay.Tests;

public class SettingsTests
{
    [Fact]
    public void OverlaySettings_UsesSharedFamilyAppearanceDefaults()
    {
        var settings = new OverlaySettings();

        Assert.Equal(OverlayPosition.TopCenter, settings.Position);
        Assert.Equal(280, settings.WindowWidth);
        Assert.Equal(62, settings.WindowHeight);
        Assert.Equal(1000, settings.RefreshIntervalMs);
        Assert.Equal(13, settings.FontSize);
        Assert.False(settings.BackgroundEnabled);
        Assert.Equal("#111720", settings.BackgroundColor);
        Assert.Equal(0.5, settings.BackgroundOpacity);
        Assert.Equal("#F2F4F7", settings.TextColor);
        Assert.Equal("#4DE3D2", settings.AccentColor);
        Assert.Equal("#FFAE5C", settings.SecondaryAccentColor);
        Assert.Equal(1, settings.Opacity);
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
            foreach (var position in Enum.GetValues<OverlayPosition>())
            {
                service.Save(new OverlaySettings
                {
                    Position = position,
                    HorizontalOffsetX = -23.5,
                    VerticalOffsetY = 123.25,
                    EdgeReference = OverlayEdgeReference.FullScreen,
                    BackgroundEnabled = true
                });

                var reloaded = service.Load();

                Assert.Equal(position, reloaded.Position);
                Assert.Equal(-23.5, reloaded.HorizontalOffsetX);
                Assert.Equal(123.25, reloaded.VerticalOffsetY);
                Assert.Equal(OverlayEdgeReference.FullScreen, reloaded.EdgeReference);
                Assert.True(reloaded.BackgroundEnabled);
            }

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
    public void SettingsService_UsesTransparentBackgroundWhenSettingIsMissing()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"void-overlay-settings-{Guid.NewGuid():N}");
        var path = Path.Combine(folder, "settings.json");
        try
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(path, """{"Position": 4}""");

            var loaded = new VOIDOverlay.Services.SettingsService(path).Load();

            Assert.False(loaded.BackgroundEnabled);
            Assert.Equal(OverlayPosition.TopCenter, loaded.Position);
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

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    public void SettingsService_UsesDefaultsWhenJsonDoesNotContainAnObject(string json)
    {
        var folder = Path.Combine(Path.GetTempPath(), $"void-overlay-settings-{Guid.NewGuid():N}");
        var path = Path.Combine(folder, "settings.json");
        try
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(path, json);

            var settings = new VOIDOverlay.Services.SettingsService(path).Load();

            Assert.Equal(new OverlaySettings().Position, settings.Position);
            Assert.Equal(new OverlaySettings().RefreshIntervalMs, settings.RefreshIntervalMs);
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
    public void OverlaySettings_ValidatesSharedAppearanceAndPositionRanges()
    {
        var settings = new OverlaySettings
        {
            FontSize = 50,
            BackgroundOpacity = double.NaN,
            Opacity = 0,
            BackgroundColor = "not-a-color",
            TextColor = "#nothex",
            HorizontalOffsetX = -900,
            VerticalOffsetY = 5000
        };

        settings.Validate();

        Assert.Equal(24, settings.FontSize);
        Assert.Equal(0.5, settings.BackgroundOpacity);
        Assert.Equal(0.3, settings.Opacity);
        Assert.Equal("#111720", settings.BackgroundColor);
        Assert.Equal("#F2F4F7", settings.TextColor);
        Assert.Equal(-200, settings.HorizontalOffsetX);
        Assert.Equal(1000, settings.VerticalOffsetY);
    }
}
