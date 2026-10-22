using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Media;
using WpfControl = System.Windows.Controls.Control;

namespace VOIDOverlay.Controls;

public sealed class TemperatureHistoryChart : WpfControl
{
    public static readonly DependencyProperty SeriesProperty =
        DependencyProperty.Register(nameof(Series), typeof(IEnumerable), typeof(TemperatureHistoryChart), new PropertyMetadata(Array.Empty<double>(), OnSeriesChanged));

    public static readonly DependencyProperty StrokeBrushProperty =
        DependencyProperty.Register(nameof(StrokeBrush), typeof(System.Windows.Media.Brush), typeof(TemperatureHistoryChart), new PropertyMetadata(new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 130, 231, 255)), OnStrokeBrushChanged));

    public IEnumerable Series
    {
        get => (IEnumerable)GetValue(SeriesProperty);
        set => SetValue(SeriesProperty, value);
    }

    public System.Windows.Media.Brush StrokeBrush
    {
        get => (System.Windows.Media.Brush)GetValue(StrokeBrushProperty);
        set => SetValue(StrokeBrushProperty, value);
    }

    private static void OnSeriesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var chart = (TemperatureHistoryChart)d;
        if (e.OldValue is INotifyCollectionChanged oldCollection)
        {
            oldCollection.CollectionChanged -= chart.OnSeriesCollectionChanged;
        }

        if (e.NewValue is INotifyCollectionChanged newCollection)
        {
            newCollection.CollectionChanged += chart.OnSeriesCollectionChanged;
        }

        chart.InvalidateVisual();
    }

    private static void OnStrokeBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((TemperatureHistoryChart)d).InvalidateVisual();
    }

    private void OnSeriesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (Dispatcher.CheckAccess())
        {
            InvalidateVisual();
        }
        else
        {
            Dispatcher.BeginInvoke(new Action(InvalidateVisual));
        }
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var values = ToDoubleList(Series);
        var validValues = values.Where(value => value.HasValue).Select(value => value!.Value).ToArray();
        if (validValues.Length == 0)
        {
            return;
        }

        var min = validValues.Min();
        var max = validValues.Max();
        if (Math.Abs(max - min) < 0.001)
        {
            min -= 1;
            max += 1;
        }

        var width = ActualWidth;
        var height = ActualHeight;
        var margin = 1d;
        var chartWidth = Math.Max(1, width - (margin * 2));
        var chartHeight = Math.Max(1, height - (margin * 2));

        var path = new StreamGeometry();
        var hasLine = false;
        using (var stream = path.Open())
        {
            var hasCurrentFigure = false;
            for (var index = 0; index < values.Count; index++)
            {
                if (!values[index].HasValue)
                {
                    hasCurrentFigure = false;
                    continue;
                }

                var value = values[index]!.Value;
                var x = margin + (index / Math.Max(1, values.Count - 1d)) * chartWidth;
                var y = height - margin - ((value - min) / Math.Max(0.0001, max - min)) * chartHeight;
                var point = new System.Windows.Point(x, y);
                if (!hasCurrentFigure)
                {
                    stream.BeginFigure(point, false, false);
                    hasCurrentFigure = true;
                    continue;
                }

                stream.LineTo(point, true, false);
                hasLine = true;
            }
        }

        if (hasLine)
        {
            path.Freeze();
            drawingContext.DrawGeometry(null, new System.Windows.Media.Pen(StrokeBrush, 1.1), path);
        }
    }

    private static List<double?> ToDoubleList(IEnumerable source)
    {
        var values = new List<double?>();

        foreach (var item in source)
        {
            values.Add(item is double value && double.IsFinite(value) ? value : null);
        }

        return values;
    }
}
