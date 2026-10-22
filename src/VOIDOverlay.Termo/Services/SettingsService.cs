using System.Diagnostics;
using System.IO;
using System.Text.Json;
using VOIDOverlay.Models;

namespace VOIDOverlay.Services;

public sealed class SettingsService
{
    private readonly string _settingsPath;
    private readonly string? _legacySettingsPath;

    public SettingsService(string? customPath = null)
    {
        _settingsPath = customPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VOIDOverlay",
            "Termo",
            "settings.json");
        _legacySettingsPath = customPath is null
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VOIDOverlay",
                "settings.json")
            : null;

        var folder = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }
    }

    public OverlaySettings Load()
    {
        var sourcePath = File.Exists(_settingsPath)
            ? _settingsPath
            : _legacySettingsPath is not null && File.Exists(_legacySettingsPath)
                ? _legacySettingsPath
                : null;
        if (sourcePath is null)
        {
            return new OverlaySettings();
        }

        try
        {
            var text = File.ReadAllText(sourcePath);
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
            if (settings.WindowWidth == 180 && settings.WindowHeight == 42)
            {
                settings.WindowWidth = 280;
                settings.WindowHeight = 62;
            }

            settings.Validate();
            if (!string.Equals(sourcePath, _settingsPath, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    Save(settings);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    Trace.WriteLine($"Legacy settings were loaded but could not be migrated. {exception.Message}");
                }
            }

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
        settings.Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        var payload = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        var temporaryPath = _settingsPath + ".tmp";
        File.WriteAllText(temporaryPath, payload);
        File.Move(temporaryPath, _settingsPath, overwrite: true);
    }
}
