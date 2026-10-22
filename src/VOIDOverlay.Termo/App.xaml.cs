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
    private ToolStripMenuItem? _visibilityMenuItem;
    private ToolStripMenuItem? _lockMenuItem;
    private ToolStripMenuItem? _startupMenuItem;

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
                "Termo",
                "logs",
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
        _settingsWindow?.CloseForExit();
        _overlayWindow?.Close();
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
        UpdateTrayMenu();
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
        UpdateTrayMenu();
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
        SetStartWithWindows(!_settings.StartWithWindows);
    }

    public void SetStartWithWindows(bool enabled)
    {
        UpdateStartupRegistry(enabled);
        _settings.StartWithWindows = enabled;
        UpdateTrayMenu();
        SaveSettings();
    }

    public void ApplySettings(OverlaySettings settings)
    {
        _settings = settings;
        _overlayWindow?.ApplySettings(settings);
        UpdateTrayMenu();
    }

    public void OpenSettingsWindow()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Show();
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new OverlaySettingsWindow(_settings, this);
        _settingsWindow.Show();
    }

    public void SaveSettings()
    {
        try
        {
            _settingsService.Save(_settings);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Trace.WriteLine($"Unable to save Termo settings. {exception}");
        }
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
            Text = "VOID Overlay Termo"
        };

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(new ToolStripMenuItem("VOID Overlay Termo") { Enabled = false });
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("Open Settings...", null, (_, _) => OpenSettingsWindow());
        _visibilityMenuItem = new ToolStripMenuItem();
        _visibilityMenuItem.Click += (_, _) => ShowHideOverlay();
        contextMenu.Items.Add(_visibilityMenuItem);
        _lockMenuItem = new ToolStripMenuItem("Lock Position") { CheckOnClick = true };
        _lockMenuItem.CheckedChanged += (_, _) => SetLock(_lockMenuItem.Checked);
        contextMenu.Items.Add(_lockMenuItem);
        _startupMenuItem = new ToolStripMenuItem("Start with Windows") { CheckOnClick = true };
        _startupMenuItem.CheckedChanged += (_, _) =>
        {
            try
            {
                SetStartWithWindows(_startupMenuItem.Checked);
            }
            catch (Exception exception) when (
                exception is UnauthorizedAccessException or InvalidOperationException or System.Security.SecurityException)
            {
                Trace.WriteLine($"Unable to update Termo startup registration: {exception}");
                _startupMenuItem.Checked = _settings.StartWithWindows;
            }
        };
        contextMenu.Items.Add(_startupMenuItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("About VOID Overlay Termo", null, (_, _) =>
            System.Windows.MessageBox.Show(
                "VOID Overlay Termo\nCPU and GPU temperature overlay.",
                "About VOID Overlay Termo",
                MessageBoxButton.OK,
                MessageBoxImage.Information));
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("Exit", null, (_, _) => Shutdown());

        _notifyIcon.ContextMenuStrip = contextMenu;
        _notifyIcon.DoubleClick += (_, _) => ShowHideOverlay();
        UpdateTrayMenu();
    }

    private void UpdateTrayMenu()
    {
        if (_visibilityMenuItem is not null)
        {
            _visibilityMenuItem.Text = _overlayWindow?.IsVisible == true ? "Hide Overlay" : "Show Overlay";
        }

        if (_lockMenuItem is not null)
        {
            _lockMenuItem.Checked = _settings.IsLocked;
        }

        if (_startupMenuItem is not null)
        {
            _startupMenuItem.Checked = _settings.StartWithWindows;
        }
    }

    private static void UpdateStartupRegistry(bool enabled)
    {
        var appPath = Environment.ProcessPath ?? Environment.GetCommandLineArgs().FirstOrDefault() ?? "VOIDOverlay.Termo.exe";
        var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
        if (key is null)
        {
            throw new InvalidOperationException("The current-user startup registry key could not be opened.");
        }

        using (key)
        {
            if (enabled)
            {
                key.SetValue("VOIDOverlayTermo", $"\"{appPath}\"");
            }
            else
            {
                key.DeleteValue("VOIDOverlayTermo", false);
            }
        }
    }
}
