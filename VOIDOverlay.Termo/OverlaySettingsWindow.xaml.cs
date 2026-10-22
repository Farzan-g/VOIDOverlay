using System.Windows;
using System.Windows.Interop;
using System.Windows.Forms;
using System.Globalization;
using VOIDOverlay.Models;
using VOIDOverlay.Services;

namespace VOIDOverlay;

public partial class OverlaySettingsWindow : Window
{
    private readonly OverlaySettings _settings;
    private readonly App _app;
    private bool _isInitializing = true;
    private bool _isSynchronizingOffsets;

    public OverlaySettingsWindow(OverlaySettings settings, App app)
    {
        InitializeComponent();
        _settings = settings;
        _app = app;

        PositionComboBox.ItemsSource = Enum.GetValues(typeof(OverlayPosition));
        PositionComboBox.SelectedItem = settings.Position;
        HorizontalOffsetSlider.Value = settings.HorizontalOffsetX;
        VerticalOffsetSlider.Value = settings.VerticalOffsetY;
        HorizontalOffsetTextBox.Text = FormatOffset(settings.HorizontalOffsetX);
        VerticalOffsetTextBox.Text = FormatOffset(settings.VerticalOffsetY);
        EdgeReferenceComboBox.SelectedIndex = settings.EdgeReference == OverlayEdgeReference.FullScreen ? 0 : 1;
        RefreshIntervalSlider.Value = settings.RefreshIntervalMs;
        LockCheckBox.IsChecked = settings.IsLocked;
        ClickThroughCheckBox.IsChecked = settings.ClickThrough;
        StartupCheckBox.IsChecked = settings.StartWithWindows;
        _isInitializing = false;
        Owner = System.Windows.Application.Current.MainWindow;
        ConfigureForOwnerScreen();
    }

    private void ConfigureForOwnerScreen()
    {
        var ownerHandle = Owner is null ? IntPtr.Zero : new WindowInteropHelper(Owner).Handle;
        var screen = ownerHandle == IntPtr.Zero
            ? System.Windows.Forms.Screen.PrimaryScreen
            : System.Windows.Forms.Screen.FromHandle(ownerHandle);
        if (screen is null)
        {
            return;
        }

        var dpi = ownerHandle == IntPtr.Zero ? 96u : GetDpiForWindow(ownerHandle);
        var scale = dpi / 96d;
        var size = SettingsWindowSizeCalculator.Calculate(
            screen.WorkingArea.Width,
            screen.WorkingArea.Height,
            scale);

        MaxWidth = size.MaxWidth;
        MaxHeight = size.MaxHeight;
        MinWidth = size.MinWidth;
        MinHeight = size.MinHeight;
        Width = size.Width;
        Height = size.Height;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryParseOffset(HorizontalOffsetTextBox.Text, out _) ||
            !TryParseOffset(VerticalOffsetTextBox.Text, out _))
        {
            (TryParseOffset(HorizontalOffsetTextBox.Text, out _)
                ? VerticalOffsetTextBox
                : HorizontalOffsetTextBox).Focus();
            return;
        }

        _settings.Position = (OverlayPosition)PositionComboBox.SelectedItem!;
        _settings.RefreshIntervalMs = (int)RefreshIntervalSlider.Value;
        _settings.IsLocked = LockCheckBox.IsChecked == true;
        _settings.ClickThrough = ClickThroughCheckBox.IsChecked == true;

        if (StartupCheckBox.IsChecked == true && !_settings.StartWithWindows)
        {
            _settings.StartWithWindows = true;
            _app.ToggleStartWithWindows();
        }
        else if (StartupCheckBox.IsChecked != true && _settings.StartWithWindows)
        {
            _settings.StartWithWindows = false;
            _app.ToggleStartWithWindows();
        }

        CommitPositionSettings(persist: true);
        _app.SetClickThrough(_settings.ClickThrough);
        _app.SetLock(_settings.IsLocked);
        Close();
    }

    private void OffsetSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isInitializing || _isSynchronizingOffsets)
        {
            return;
        }

        _isSynchronizingOffsets = true;
        try
        {
            if (ReferenceEquals(sender, HorizontalOffsetSlider))
            {
                HorizontalOffsetTextBox.Text = FormatOffset(HorizontalOffsetSlider.Value);
            }
            else
            {
                VerticalOffsetTextBox.Text = FormatOffset(VerticalOffsetSlider.Value);
            }
        }
        finally
        {
            _isSynchronizingOffsets = false;
        }

        CommitPositionSettings();
    }

    private void OffsetTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_isInitializing || _isSynchronizingOffsets || sender is not System.Windows.Controls.TextBox textBox)
        {
            return;
        }

        if (!TryParseOffset(textBox.Text, out var value))
        {
            textBox.ToolTip = "Enter a finite value from -200 to 1000 DIPs.";
            return;
        }

        textBox.ToolTip = null;
        _isSynchronizingOffsets = true;
        try
        {
            if (ReferenceEquals(textBox, HorizontalOffsetTextBox))
            {
                HorizontalOffsetSlider.Value = value;
            }
            else
            {
                VerticalOffsetSlider.Value = value;
            }
        }
        finally
        {
            _isSynchronizingOffsets = false;
        }

        CommitPositionSettings();
    }

    private void OffsetTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox textBox ||
            TryParseOffset(textBox.Text, out _))
        {
            return;
        }

        textBox.Text = ReferenceEquals(textBox, HorizontalOffsetTextBox)
            ? FormatOffset(HorizontalOffsetSlider.Value)
            : FormatOffset(VerticalOffsetSlider.Value);
        textBox.ToolTip = null;
    }

    private void EdgeReferenceComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_isInitializing)
        {
            CommitPositionSettings();
        }
    }

    private void CommitPositionSettings(bool persist = false)
    {
        if (_isInitializing || PositionComboBox.SelectedItem is not OverlayPosition position)
        {
            return;
        }

        var edgeReference = EdgeReferenceComboBox.SelectedIndex == 0
            ? OverlayEdgeReference.FullScreen
            : OverlayEdgeReference.WorkArea;
        _settings.Position = position;
        _settings.HorizontalOffsetX = HorizontalOffsetSlider.Value;
        _settings.VerticalOffsetY = VerticalOffsetSlider.Value;
        _settings.EdgeReference = edgeReference;
        _app.SetPositionFineTuning(
            _settings.HorizontalOffsetX,
            _settings.VerticalOffsetY,
            _settings.EdgeReference,
            persist);
    }

    private static bool TryParseOffset(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) &&
        double.IsFinite(value) &&
        value >= -200 &&
        value <= 1000;

    private static string FormatOffset(double value) => value.ToString("G", CultureInfo.CurrentCulture);

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetDpiForWindow")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);
}
