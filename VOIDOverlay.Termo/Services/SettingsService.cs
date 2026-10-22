using System.Diagnostics;
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
            var settings = JsonSerializer.Deserialize<OverlaySettings>(text)
                ?? throw new JsonException("The settings document must contain an object.");
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("The settings document must contain an object.");
            }

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
        catch (JsonException exception)
        {
            Trace.WriteLine($"Settings could not be parsed; defaults were loaded. {exception.Message}");
            return new OverlaySettings();
        }
        catch (IOException exception)
        {
            Trace.WriteLine($"Settings could not be read; defaults were loaded. {exception.Message}");
            return new OverlaySettings();
        }
        catch (UnauthorizedAccessException exception)
        {
            Trace.WriteLine($"Settings could not be read; defaults were loaded. {exception.Message}");
            return new OverlaySettings();
        }
    }

    public void Save(OverlaySettings settings)
    {
        var payload = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        var temporaryPath = _settingsPath + ".tmp";
        File.WriteAllText(temporaryPath, payload);
        File.Move(temporaryPath, _settingsPath, overwrite: true);
    }
}
