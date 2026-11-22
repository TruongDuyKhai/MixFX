using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Soundboard.App.Views.Controls;

/// <summary>
/// Draws the peak envelope with the trim region and fade ramps, and lets the user drag the two trim
/// markers (§5.4). Pure view: positions come in and go out through two-way bound properties (ms).
/// </summary>
public sealed class WaveformView : FrameworkElement
{
    public static readonly DependencyProperty PeaksProperty = DependencyProperty.Register(
        nameof(Peaks), typeof(IReadOnlyList<float>), typeof(WaveformView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty DurationMsProperty = DependencyProperty.Register(
        nameof(DurationMs), typeof(double), typeof(WaveformView),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrimStartMsProperty = DependencyProperty.Register(
        nameof(TrimStartMs), typeof(double), typeof(WaveformView),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty TrimEndMsProperty = DependencyProperty.Register(
        nameof(TrimEndMs), typeof(double), typeof(WaveformView),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty FadeInMsProperty = DependencyProperty.Register(
        nameof(FadeInMs), typeof(double), typeof(WaveformView),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FadeOutMsProperty = DependencyProperty.Register(
        nameof(FadeOutMs), typeof(double), typeof(WaveformView),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MinTrimmedMsProperty = DependencyProperty.Register(
        nameof(MinTrimmedMs), typeof(double), typeof(WaveformView), new FrameworkPropertyMetadata(0d));

    /// <summary>How close (px) the pointer must be to grab a marker.</summary>
    private const double GrabDistance = 8;

    private const double MarkerWidth = 2;
    private const double HandleSize = 10;

    private static readonly Brush BackgroundBrush = Frozen(new SolidColorBrush(Color.FromRgb(0x1B, 0x1B, 0x1F)));
    private static readonly Pen WavePen = Frozen(new Pen(Frozen(new SolidColorBrush(Color.FromRgb(0x9E, 0x86, 0xFF))), 1));
    private static readonly Brush OutsideTrimBrush = Frozen(new SolidColorBrush(Color.FromArgb(0xB0, 0x10, 0x10, 0x14)));
    private static readonly Brush MarkerBrush = Frozen(new SolidColorBrush(Color.FromRgb(0xFF, 0xB3, 0x00)));
    private static readonly Pen FadePen = Frozen(new Pen(Frozen(new SolidColorBrush(Color.FromRgb(0x43, 0xE6, 0x6B))), 1.5));
    private static readonly Pen CenterPen = Frozen(new Pen(Frozen(new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x3B))), 1));

    private Marker _dragging = Marker.None;

    private enum Marker
    {
        None,
        Start,
        End,
    }

    public IReadOnlyList<float>? Peaks
    {
        get => (IReadOnlyList<float>?)GetValue(PeaksProperty);
        set => SetValue(PeaksProperty, value);
    }

    public double DurationMs
    {
        get => (double)GetValue(DurationMsProperty);
        set => SetValue(DurationMsProperty, value);
    }

    public double TrimStartMs
    {
        get => (double)GetValue(TrimStartMsProperty);
        set => SetValue(TrimStartMsProperty, value);
    }

    public double TrimEndMs
    {
        get => (double)GetValue(TrimEndMsProperty);
        set => SetValue(TrimEndMsProperty, value);
    }

    public double FadeInMs
    {
        get => (double)GetValue(FadeInMsProperty);
        set => SetValue(FadeInMsProperty, value);
    }

    public double FadeOutMs
    {
        get => (double)GetValue(FadeOutMsProperty);
        set => SetValue(FadeOutMsProperty, value);
    }

    public double MinTrimmedMs
    {
        get => (double)GetValue(MinTrimmedMsProperty);
        set => SetValue(MinTrimmedMsProperty, value);
    }

    protected override void OnRender(DrawingContext context)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        context.DrawRectangle(BackgroundBrush, null, new Rect(0, 0, width, height));

        var middle = height / 2;
        context.DrawLine(CenterPen, new Point(0, middle), new Point(width, middle));
        if (DurationMs <= 0 || width <= 0)
        {
            return;
        }

        DrawPeaks(context, width, middle);

        var start = ToX(TrimStartMs);
        var end = ToX(TrimEndMs);
        context.DrawRectangle(OutsideTrimBrush, null, new Rect(0, 0, Math.Max(0, start), height));
        context.DrawRectangle(OutsideTrimBrush, null, new Rect(end, 0, Math.Max(0, width - end), height));

        if (FadeInMs > 0)
        {
            context.DrawLine(FadePen, new Point(start, height), new Point(ToX(TrimStartMs + FadeInMs), 0));
        }

        if (FadeOutMs > 0)
        {
            context.DrawLine(FadePen, new Point(ToX(TrimEndMs - FadeOutMs), 0), new Point(end, height));
        }

        DrawMarker(context, start, height);
        DrawMarker(context, end, height);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (DurationMs <= 0)
        {
            return;
        }

        // A click away from both markers moves the nearer one there, which is quicker than aiming.
        var x = e.GetPosition(this).X;
        _dragging = Math.Abs(x - ToX(TrimStartMs)) <= Math.Abs(x - ToX(TrimEndMs)) ? Marker.Start : Marker.End;
        CaptureMouse();
        MoveMarker(x);
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var x = e.GetPosition(this).X;
        if (_dragging != Marker.None)
        {
            MoveMarker(x);
            return;
        }

        var nearMarker = Math.Abs(x - ToX(TrimStartMs)) <= GrabDistance || Math.Abs(x - ToX(TrimEndMs)) <= GrabDistance;
        Cursor = nearMarker ? Cursors.SizeWE : Cursors.Arrow;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        _dragging = Marker.None;
        ReleaseMouseCapture();
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        _dragging = Marker.None;
    }

    private static T Frozen<T>(T freezable)
        where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }

    private static void DrawMarker(DrawingContext context, double x, double height)
    {
        context.DrawRectangle(MarkerBrush, null, new Rect(x - (MarkerWidth / 2), 0, MarkerWidth, height));
        context.DrawRectangle(MarkerBrush, null, new Rect(x - (HandleSize / 2), 0, HandleSize, HandleSize));
    }

    private void DrawPeaks(DrawingContext context, double width, double middle)
    {
        var peaks = Peaks;
        if (peaks is null || peaks.Count == 0)
        {
            return;
        }

        var geometry = new StreamGeometry();
        using (var figures = geometry.Open())
        {
            var columns = (int)Math.Ceiling(width);
            for (var column = 0; column < columns; column++)
            {
                // Each pixel column shows the loudest bucket it covers, so short transients stay visible.
                var first = (int)((long)column * peaks.Count / columns);
                var last = Math.Max(first + 1, (int)((long)(column + 1) * peaks.Count / columns));
                var peak = 0f;
                for (var i = first; i < last && i < peaks.Count; i++)
                {
                    peak = MathF.Max(peak, peaks[i]);
                }

                var half = Math.Min(1, peak) * middle;
                figures.BeginFigure(new Point(column + 0.5, middle - half), false, false);
                figures.LineTo(new Point(column + 0.5, middle + half), true, false);
            }
        }

        geometry.Freeze();
        context.DrawGeometry(null, WavePen, geometry);
    }

    private double ToX(double ms) => Math.Clamp(ms / DurationMs, 0, 1) * ActualWidth;

    private void MoveMarker(double x)
    {
        var ms = Math.Clamp(x / Math.Max(1, ActualWidth), 0, 1) * DurationMs;
        if (_dragging == Marker.Start)
        {
            TrimStartMs = Math.Round(Math.Clamp(ms, 0, Math.Max(0, TrimEndMs - MinTrimmedMs)));
        }
        else if (_dragging == Marker.End)
        {
            TrimEndMs = Math.Round(Math.Clamp(ms, Math.Min(DurationMs, TrimStartMs + MinTrimmedMs), DurationMs));
        }
    }
}
