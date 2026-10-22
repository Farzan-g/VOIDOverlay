using System.IO;
using System.Text.Json;
using VOIDOverlay.Models;

namespace VOIDOverlay.Services;

public sealed class SettingsService
{
    private readonly string _settingsPath;

    public SettingsService(string? customPath = null)
    {
        _settingsPath = customPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VOIDOverlay",
            "settings.json");

        var folder = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }
    }

    public OverlaySettings Load()
    {
        if (!File.Exists(_settingsPath))
        {
            return new OverlaySettings();
        }

        try
        {
            var text = File.ReadAllText(_settingsPath);
            var settings = JsonSerializer.Deserialize<OverlaySettings>(text) ?? new OverlaySettings();
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            var hasHorizontalOffset = root.TryGetProperty(nameof(OverlaySettings.HorizontalOffsetX), out _);
            var hasVerticalOffset = root.TryGetProperty(nameof(OverlaySettings.VerticalOffsetY), out _);
            if (settings.EdgeMargin.HasValue)
            {
                if (!hasHorizontalOffset)
                {
                    settings.HorizontalOffsetX = settings.EdgeMargin.Value;
                }

                if (!hasVerticalOffset)
                {
                    settings.VerticalOffsetY = settings.EdgeMargin.Value;
                }
            }

            settings.EdgeMargin = null;
            return settings;
        }
        catch
        {
            return new OverlaySettings();
        }
    }

    public void Save(OverlaySettings settings)
    {
        var payload = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_settingsPath, payload);
    }
}
