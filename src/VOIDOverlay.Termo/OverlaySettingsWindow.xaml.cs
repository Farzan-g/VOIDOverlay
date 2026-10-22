using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using VOIDOverlay.Models;

namespace VOIDOverlay;

public partial class OverlaySettingsWindow : Window
{
    private readonly OverlaySettings _settings;
    private readonly App _app;
    private readonly DispatcherTimer _saveTimer;
    private bool _initialized;
    private bool _synchronizingOffsets;
    private bool _allowClose;

    public OverlaySettingsWindow(OverlaySettings settings, App app)
    {
        InitializeComponent();
        _settings = settings;
        _app = app;
        Owner = System.Windows.Application.Current.MainWindow;
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            _app.SaveSettings();
        };
        PositionComboBox.ItemsSource = Enum.GetValues<OverlayPosition>();
        LoadSettings();
        WireChanges();
        Closing += OnClosing;
    }

    public void CloseForExit()
    {
        _allowClose = true;
        _saveTimer.Stop();
        Close();
    }

    private void LoadSettings()
    {
        _initialized = false;
        StartupCheck.IsChecked = _settings.StartWithWindows;
        TopmostCheck.IsChecked = _settings.AlwaysOnTop;
        LockedCheck.IsChecked = _settings.IsLocked;
        ClickThroughCheck.IsChecked = _settings.ClickThrough;
        StartVisibleCheck.IsChecked = _settings.IsOverlayVisible;
        BackgroundEnabledCheck.IsChecked = _settings.BackgroundEnabled;
        BackgroundColorBox.Text = _settings.BackgroundColor;
        BackgroundOpacitySlider.Value = _settings.BackgroundOpacity;
        OverlayOpacitySlider.Value = _settings.Opacity;
        TextColorBox.Text = _settings.TextColor;
        AccentColorBox.Text = _settings.AccentColor;
        SecondaryAccentColorBox.Text = _settings.SecondaryAccentColor;
        FontSizeSlider.Value = _settings.FontSize;
        PositionComboBox.SelectedItem = _settings.Position;
        EdgeReferenceComboBox.SelectedIndex =
            _settings.EdgeReference == OverlayEdgeReference.WorkArea ? 0 : 1;
        OffsetXSlider.Value = _settings.HorizontalOffsetX;
        OffsetYSlider.Value = _settings.VerticalOffsetY;
        OffsetXBox.Text = FormatOffset(_settings.HorizontalOffsetX);
        OffsetYBox.Text = FormatOffset(_settings.VerticalOffsetY);
        IntervalComboBox.SelectedValue = _settings.RefreshIntervalMs.ToString(CultureInfo.InvariantCulture);
        PositionControls.IsEnabled = !_settings.IsLocked;
        _initialized = true;
    }

    private void WireChanges()
    {
        StartupCheck.Checked += SettingsChanged;
        StartupCheck.Unchecked += SettingsChanged;
        TopmostCheck.Checked += SettingsChanged;
        TopmostCheck.Unchecked += SettingsChanged;
        LockedCheck.Checked += SettingsChanged;
        LockedCheck.Unchecked += SettingsChanged;
        ClickThroughCheck.Checked += SettingsChanged;
        ClickThroughCheck.Unchecked += SettingsChanged;
        StartVisibleCheck.Checked += SettingsChanged;
        StartVisibleCheck.Unchecked += SettingsChanged;
        BackgroundEnabledCheck.Checked += SettingsChanged;
        BackgroundEnabledCheck.Unchecked += SettingsChanged;
        BackgroundColorBox.LostFocus += SettingsChanged;
        BackgroundOpacitySlider.ValueChanged += SettingsChanged;
        OverlayOpacitySlider.ValueChanged += SettingsChanged;
        TextColorBox.LostFocus += SettingsChanged;
        AccentColorBox.LostFocus += SettingsChanged;
        SecondaryAccentColorBox.LostFocus += SettingsChanged;
        FontSizeSlider.ValueChanged += SettingsChanged;
        PositionComboBox.SelectionChanged += SettingsChanged;
        EdgeReferenceComboBox.SelectionChanged += SettingsChanged;
        IntervalComboBox.SelectionChanged += SettingsChanged;
        OffsetXSlider.ValueChanged += OffsetSliderChanged;
        OffsetYSlider.ValueChanged += OffsetSliderChanged;
        OffsetXBox.LostFocus += OffsetTextChanged;
        OffsetYBox.LostFocus += OffsetTextChanged;
    }

    private void SettingsChanged(object sender, RoutedEventArgs e) => ApplySettings();

    private void OffsetSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_initialized || _synchronizingOffsets)
        {
            return;
        }

        _synchronizingOffsets = true;
        try
        {
            if (ReferenceEquals(sender, OffsetXSlider))
            {
                OffsetXBox.Text = FormatOffset(OffsetXSlider.Value);
            }
            else
            {
                OffsetYBox.Text = FormatOffset(OffsetYSlider.Value);
            }
        }
        finally
        {
            _synchronizingOffsets = false;
        }

        ApplySettings();
    }

    private void OffsetTextChanged(object sender, RoutedEventArgs e)
    {
        if (!_initialized || _synchronizingOffsets)
        {
            return;
        }

        var textBox = (System.Windows.Controls.TextBox)sender;
        if (!TryParseOffset(textBox.Text, out var offset))
        {
            textBox.ToolTip = "Enter a finite value from -200 to 1000 DIPs.";
            return;
        }

        textBox.ToolTip = null;
        _synchronizingOffsets = true;
        try
        {
            if (ReferenceEquals(textBox, OffsetXBox))
            {
                OffsetXSlider.Value = offset;
            }
            else
            {
                OffsetYSlider.Value = offset;
            }
        }
        finally
        {
            _synchronizingOffsets = false;
        }

        ApplySettings();
    }

    private void ApplySettings()
    {
        if (!_initialized)
        {
            return;
        }

        if (!TryReadAppearanceColor(BackgroundColorBox, out var backgroundColor) ||
            !TryReadAppearanceColor(TextColorBox, out var textColor) ||
            !TryReadAppearanceColor(AccentColorBox, out var accentColor) ||
            !TryReadAppearanceColor(SecondaryAccentColorBox, out var secondaryColor))
        {
            return;
        }

        if (!TryParseOffset(OffsetXBox.Text, out var offsetX) ||
            !TryParseOffset(OffsetYBox.Text, out var offsetY))
        {
            return;
        }

        var startWithWindows = StartupCheck.IsChecked == true;
        if (startWithWindows != _settings.StartWithWindows)
        {
            try
            {
                _app.SetStartWithWindows(startWithWindows);
            }
            catch (Exception exception) when (
                exception is UnauthorizedAccessException or InvalidOperationException or System.Security.SecurityException)
            {
                System.Diagnostics.Trace.WriteLine($"Unable to update Termo startup registration: {exception}");
                StartupCheck.IsChecked = _settings.StartWithWindows;
            }
        }

        _settings.AlwaysOnTop = TopmostCheck.IsChecked == true;
        _settings.IsLocked = LockedCheck.IsChecked == true;
        _settings.ClickThrough = ClickThroughCheck.IsChecked == true;
        _settings.IsOverlayVisible = StartVisibleCheck.IsChecked == true;
        _settings.BackgroundEnabled = BackgroundEnabledCheck.IsChecked == true;
        _settings.BackgroundColor = backgroundColor;
        _settings.BackgroundOpacity = BackgroundOpacitySlider.Value;
        _settings.Opacity = OverlayOpacitySlider.Value;
        _settings.TextColor = textColor;
        _settings.AccentColor = accentColor;
        _settings.SecondaryAccentColor = secondaryColor;
        _settings.FontSize = FontSizeSlider.Value;
        if (PositionComboBox.SelectedItem is OverlayPosition position)
        {
            _settings.Position = position;
        }

        _settings.EdgeReference = EdgeReferenceComboBox.SelectedIndex == 0
            ? OverlayEdgeReference.WorkArea
            : OverlayEdgeReference.FullScreen;
        _settings.HorizontalOffsetX = offsetX;
        _settings.VerticalOffsetY = offsetY;
        if (IntervalComboBox.SelectedItem is ComboBoxItem interval &&
            int.TryParse(interval.Tag?.ToString(), out var milliseconds))
        {
            _settings.RefreshIntervalMs = milliseconds;
        }

        PositionControls.IsEnabled = !_settings.IsLocked;
        _settings.Validate();
        _app.ApplySettings(_settings);
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private static bool TryReadAppearanceColor(System.Windows.Controls.TextBox textBox, out string value)
    {
        value = textBox.Text.Trim();
        if (value.Length == 7 &&
            value[0] == '#' &&
            value.Skip(1).All(Uri.IsHexDigit))
        {
            value = value.ToUpperInvariant();
            textBox.ToolTip = null;
            return true;
        }

        textBox.ToolTip = "Enter a six-digit hex color such as #4DE3D2.";
        textBox.Focus();
        return false;
    }

    private static bool TryParseOffset(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) &&
        double.IsFinite(value) &&
        value is >= -200 and <= 1000;

    private static string FormatOffset(double value) => value.ToString("G", CultureInfo.CurrentCulture);

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        var startupEnabled = _settings.StartWithWindows;
        var defaults = new OverlaySettings { StartWithWindows = startupEnabled };
        CopySettings(defaults, _settings);
        LoadSettings();
        ApplySettings();
    }

    private void Done_Click(object sender, RoutedEventArgs e)
    {
        _saveTimer.Stop();
        _app.SaveSettings();
        Hide();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _saveTimer.Stop();
        if (!_allowClose)
        {
            _app.SaveSettings();
            e.Cancel = true;
            Hide();
        }
    }

    private static void CopySettings(OverlaySettings source, OverlaySettings destination)
    {
        destination.WindowLeft = source.WindowLeft;
        destination.WindowTop = source.WindowTop;
        destination.WindowWidth = source.WindowWidth;
        destination.WindowHeight = source.WindowHeight;
        destination.Opacity = source.Opacity;
        destination.BackgroundEnabled = source.BackgroundEnabled;
        destination.BackgroundColor = source.BackgroundColor;
        destination.BackgroundOpacity = source.BackgroundOpacity;
        destination.TextColor = source.TextColor;
        destination.AccentColor = source.AccentColor;
        destination.SecondaryAccentColor = source.SecondaryAccentColor;
        destination.FontSize = source.FontSize;
        destination.AlwaysOnTop = source.AlwaysOnTop;
        destination.ClickThrough = source.ClickThrough;
        destination.IsCompactMode = source.IsCompactMode;
        destination.IsOverlayVisible = source.IsOverlayVisible;
        destination.IsLocked = source.IsLocked;
        destination.StartMinimized = source.StartMinimized;
        destination.StartWithWindows = source.StartWithWindows;
        destination.RefreshIntervalMs = source.RefreshIntervalMs;
        destination.HistorySeconds = source.HistorySeconds;
        destination.HorizontalOffsetX = source.HorizontalOffsetX;
        destination.VerticalOffsetY = source.VerticalOffsetY;
        destination.EdgeReference = source.EdgeReference;
        destination.Position = source.Position;
        destination.Theme = source.Theme;
        destination.EdgeMargin = source.EdgeMargin;
    }
}
