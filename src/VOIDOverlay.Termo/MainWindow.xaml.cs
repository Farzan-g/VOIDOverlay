using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using VOIDOverlay.Models;
using VOIDOverlay.Services;
using VOIDOverlay.ViewModels;

namespace VOIDOverlay;

public partial class MainWindow : Window
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_LAYERED = 0x80000;
    private const int WS_EX_TRANSPARENT = 0x20;

    public MainWindow(OverlaySettings? settings = null, bool diagnosticsEnabled = false)
    {
        InitializeComponent();
        var monitorService = new HardwareMonitorService(diagnosticsEnabled);
        var settingsService = new SettingsService();
        DataContext = new MainViewModel(monitorService, settingsService, diagnosticsEnabled);

        if (settings is not null)
        {
            ((MainViewModel)DataContext).ApplySettings(settings);
        }

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        SourceInitialized += MainWindow_SourceInitialized;
    }

    public void ApplySettings(OverlaySettings settings)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.ApplySettings(settings);
        }

        ApplyAppearance(settings);
        ApplyWindowPosition();
        UpdateClickThroughState();
    }

    public void SetOverlayVisible(bool visible)
    {
        if (visible)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.Settings.IsOverlayVisible = true;
            }

            Show();
            Activate();
        }
        else
        {
            if (DataContext is MainViewModel vm)
            {
                vm.Settings.IsOverlayVisible = false;
            }

            Hide();
        }
    }

    public void ApplyWindowPosition()
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        var settings = vm.Settings;
        Width = settings.WindowWidth;
        Height = settings.WindowHeight;
        UpdateLayout();

        var targetScreen = GetTargetScreen();
        var screenBounds = targetScreen.Bounds;
        var workArea = targetScreen.WorkingArea;
        var edgeBounds = OverlayPositionCalculator.SelectEdgeBounds(
            ToPixelBounds(screenBounds),
            ToPixelBounds(workArea),
            settings.EdgeReference);
        var dpi = PresentationSource.FromVisual(this) is HwndSource source
            ? GetDpiForWindow(source.Handle)
            : 96u;
        var dpiScaleX = dpi / 96d;
        var dpiScaleY = dpi / 96d;
        var overlayWidth = ActualWidth > 0 ? ActualWidth : Width;
        var overlayHeight = ActualHeight > 0 ? ActualHeight : Height;
        var location = OverlayPositionCalculator.Calculate(
            edgeBounds,
            dpiScaleX,
            dpiScaleY,
            overlayWidth,
            overlayHeight,
            settings.HorizontalOffsetX,
            settings.VerticalOffsetY,
            settings.Position);

        Left = location.Left;
        Top = location.Top;
        Dispatcher.BeginInvoke(
            new Action(() => TracePosition(targetScreen, edgeBounds, dpiScaleX, overlayWidth, overlayHeight)),
            System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            ApplyAppearance(vm.Settings);
        }

        ApplyWindowPosition();
        UpdateClickThroughState();
        if (!((MainViewModel)DataContext).Settings.IsOverlayVisible)
        {
            Hide();
        }
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        ApplyWindowPosition();
        UpdateClickThroughState();
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.SaveSettings();
            vm.Dispose();
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm && !vm.Settings.IsLocked)
        {
            UpdateClickThroughState();
            DragMove();
        }
    }

    private void Window_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    private void UpdateClickThroughState()
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        if (PresentationSource.FromVisual(this) is not HwndSource hwndSource)
        {
            return;
        }

        var style = GetWindowLong(hwndSource.Handle, GWL_EXSTYLE);
        var layered = style | WS_EX_LAYERED;
        var transparent = vm.Settings.ClickThrough ? layered | WS_EX_TRANSPARENT : layered & ~WS_EX_TRANSPARENT;
        SetWindowLong(hwndSource.Handle, GWL_EXSTYLE, transparent);
    }

    private void ApplyAppearance(OverlaySettings settings)
    {
        OverlaySurface.Background = ParseBrush(settings.BackgroundColor, settings.BackgroundOpacity, settings.BackgroundEnabled);
        OverlaySurface.BorderBrush = System.Windows.Media.Brushes.Transparent;
        OverlaySurface.BorderThickness = new Thickness(0);
        Opacity = settings.Opacity;
        Topmost = settings.AlwaysOnTop;
        CpuTemperatureTextBlock.FontSize = settings.FontSize;
        GpuTemperatureTextBlock.FontSize = settings.FontSize;
        CpuTemperatureTextBlock.Foreground = ParseBrush(settings.AccentColor, 1);
        GpuTemperatureTextBlock.Foreground = ParseBrush(settings.SecondaryAccentColor, 1);
        CpuLabel.Foreground = ParseBrush(settings.TextColor, 1);
        GpuLabel.Foreground = ParseBrush(settings.TextColor, 1);
        CpuChart.StrokeBrush = ParseBrush(settings.AccentColor, 1);
        GpuChart.StrokeBrush = ParseBrush(settings.SecondaryAccentColor, 1);
    }

    private static System.Windows.Media.Brush ParseBrush(string color, double opacity, bool enabled = true)
    {
        if (!enabled)
        {
            return System.Windows.Media.Brushes.Transparent;
        }

        var brush = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
        brush.Opacity = opacity;
        brush.Freeze();
        return brush;
    }

    private Screen GetTargetScreen()
    {
        var screen = PresentationSource.FromVisual(this) is HwndSource hwndSource
            ? Screen.FromHandle(hwndSource.Handle)
            : Screen.PrimaryScreen ?? Screen.AllScreens.FirstOrDefault();
        return screen ?? throw new InvalidOperationException("No monitor detected.");
    }

    private static PixelBounds ToPixelBounds(System.Drawing.Rectangle bounds) =>
        new(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);

    private void TracePosition(Screen screen, PixelBounds edgeBounds, double dpiScale, double widthDip, double heightDip)
    {
        var physicalRect = new NativeRect();
        var hwnd = new WindowInteropHelper(this).Handle;
        var rectAvailable = hwnd != IntPtr.Zero && GetWindowRect(hwnd, out physicalRect);
        var settings = ((MainViewModel)DataContext).Settings;
        Trace.WriteLine(
            $"Overlay positioning: monitor='{screen.DeviceName}', edgeReference={settings.EdgeReference}, edgeBoundsPx={edgeBounds}, dpiScale={dpiScale:0.###}, " +
            $"overlayDip={widthDip:0.##}x{heightDip:0.##}, offsetsDip=({settings.HorizontalOffsetX:0.##},{settings.VerticalOffsetY:0.##}), " +
            $"position={settings.Position}, WPF=({Left:0.##},{Top:0.##}), " +
            $"windowPx={(rectAvailable ? physicalRect.ToString() : "unavailable")}.");
    }

    private static uint GetDpiForWindow(IntPtr hwnd) => hwnd == IntPtr.Zero ? 96 : GetDpiForWindowNative(hwnd);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetDpiForWindow")]
    private static extern uint GetDpiForWindowNative(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public override readonly string ToString() => $"({Left},{Top})-({Right},{Bottom})";
    }
}
