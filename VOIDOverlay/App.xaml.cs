using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using Microsoft.Win32;
using VOIDOverlay.Models;
using VOIDOverlay.Services;

namespace VOIDOverlay;

public partial class App : System.Windows.Application
{
    private readonly SettingsService _settingsService = new();
    private OverlaySettings _settings = new();
    private MainWindow? _overlayWindow;
    private NotifyIcon? _notifyIcon;
    private Icon? _trayIcon;
    private Stream? _trayIconStream;
    private OverlaySettingsWindow? _settingsWindow;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var diagnosticsEnabled = e.Args.Any(argument =>
            string.Equals(argument, "--diagnostics", StringComparison.OrdinalIgnoreCase));
        if (diagnosticsEnabled)
        {
            var diagnosticsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VOIDOverlay",
                "diagnostics.log");
            Directory.CreateDirectory(Path.GetDirectoryName(diagnosticsPath)!);
            Trace.Listeners.Add(new TextWriterTraceListener(diagnosticsPath));
            Trace.AutoFlush = true;
            Trace.WriteLine($"VOIDOverlay Termo diagnostics started at {DateTimeOffset.Now:O}.");
        }

        _settings = _settingsService.Load();
        _overlayWindow = new MainWindow(_settings, diagnosticsEnabled);

        if (_settings.IsOverlayVisible)
        {
            _overlayWindow.Show();
        }
        else
        {
            _overlayWindow.Hide();
        }

        _overlayWindow.ApplyWindowPosition();
        CreateTrayIcon();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _notifyIcon?.Dispose();
        _trayIcon?.Dispose();
        _trayIconStream?.Dispose();
        _settingsService.Save(_settings);
        base.OnExit(e);
    }

    public void ShowHideOverlay()
    {
        if (_overlayWindow is null)
        {
            return;
        }

        var shouldShow = !_overlayWindow.IsVisible;
        _settings.IsOverlayVisible = shouldShow;
        _overlayWindow.SetOverlayVisible(shouldShow);
        _settingsService.Save(_settings);
    }

    public void ToggleLock()
    {
        SetLock(!_settings.IsLocked);
    }

    public void SetLock(bool isLocked)
    {
        _settings.IsLocked = isLocked;
        _overlayWindow?.ApplySettings(_settings);
        _settingsService.Save(_settings);
    }

    public void SetPosition(OverlayPosition position)
    {
        _settings.Position = position;
        _overlayWindow?.ApplySettings(_settings);
        _overlayWindow?.ApplyWindowPosition();
        _settingsService.Save(_settings);
    }

    public void SetPositionFineTuning(
        double horizontalOffsetX,
        double verticalOffsetY,
        OverlayEdgeReference edgeReference,
        bool persist = true)
    {
        _settings.HorizontalOffsetX = horizontalOffsetX;
        _settings.VerticalOffsetY = verticalOffsetY;
        _settings.EdgeReference = edgeReference;
        _overlayWindow?.ApplyWindowPosition();
        if (persist)
        {
            _settingsService.Save(_settings);
        }
    }

    public void SetClickThrough(bool enabled)
    {
        _settings.ClickThrough = enabled;
        _overlayWindow?.ApplySettings(_settings);
        _settingsService.Save(_settings);
    }

    public void ToggleStartWithWindows()
    {
        _settings.StartWithWindows = !_settings.StartWithWindows;
        UpdateStartupRegistry(_settings.StartWithWindows);
        _settingsService.Save(_settings);
    }

    public void OpenSettingsWindow()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new OverlaySettingsWindow(_settings, this);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    private void CreateTrayIcon()
    {
        var iconResource = System.Windows.Application.GetResourceStream(
            new Uri("pack://application:,,,/assets/void-overlay.ico", UriKind.Absolute))
            ?? throw new InvalidOperationException("The VOIDOverlay Termo icon resource could not be loaded.");
        _trayIconStream = iconResource.Stream;
        _trayIcon = new Icon(_trayIconStream);

        _notifyIcon = new NotifyIcon
        {
            Icon = _trayIcon,
            Visible = true,
            Text = "Termo"
        };

        var contextMenu = new ContextMenuStrip();
        var toggleItem = new ToolStripMenuItem(_settings.IsOverlayVisible ? "Hide Overlay" : "Show Overlay");
        toggleItem.Click += (_, _) => ShowHideOverlay();

        var positionMenu = new ToolStripMenuItem("Overlay Position");
        foreach (OverlayPosition position in Enum.GetValues(typeof(OverlayPosition)))
        {
            var item = new ToolStripMenuItem(position.ToString());
            item.Click += (_, _) => SetPosition(position);
            positionMenu.DropDownItems.Add(item);
        }

        var lockItem = new ToolStripMenuItem(_settings.IsLocked ? "Unlock Overlay" : "Lock Overlay");
        lockItem.Click += (_, _) => ToggleLock();

        var settingsItem = new ToolStripMenuItem("Settings");
        settingsItem.Click += (_, _) => OpenSettingsWindow();

        var startupItem = new ToolStripMenuItem(_settings.StartWithWindows ? "Disable Start with Windows" : "Enable Start with Windows");
        startupItem.Click += (_, _) => ToggleStartWithWindows();

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => Shutdown();

        contextMenu.Items.Add(toggleItem);
        contextMenu.Items.Add(positionMenu);
        contextMenu.Items.Add(lockItem);
        contextMenu.Items.Add(settingsItem);
        contextMenu.Items.Add(startupItem);
        contextMenu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = contextMenu;
        _notifyIcon.DoubleClick += (_, _) => ShowHideOverlay();

        _notifyIcon.Visible = true;
    }

    private static void UpdateStartupRegistry(bool enabled)
    {
        var appPath = Environment.ProcessPath ?? Environment.GetCommandLineArgs().FirstOrDefault() ?? "VOIDOverlay.exe";
        var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
        if (key is null)
        {
            return;
        }

        if (enabled)
        {
            key.SetValue("VOIDOverlay", $"\"{appPath}\"");
        }
        else
        {
            key.DeleteValue("VOIDOverlay", false);
        }
    }
}
