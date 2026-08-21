using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ChurchBooks.Accounting.Offerings;
using ChurchBooks.App.Models;

namespace ChurchBooks.App.Controls;

public sealed class OfferingChartControl : FrameworkElement
{
    public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(
        nameof(Points), typeof(IEnumerable<OfferingChartPoint>), typeof(OfferingChartControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ChartKindProperty = DependencyProperty.Register(
        nameof(ChartKind), typeof(OfferingChartKind), typeof(OfferingChartControl),
        new FrameworkPropertyMetadata(OfferingChartKind.Line, FrameworkPropertyMetadataOptions.AffectsRender));

    public IEnumerable<OfferingChartPoint>? Points
    {
        get => (IEnumerable<OfferingChartPoint>?)GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public OfferingChartKind ChartKind
    {
        get => (OfferingChartKind)GetValue(ChartKindProperty);
        set => SetValue(ChartKindProperty, value);
    }

    private static readonly Brush[] Palette =
    {
        Brushes.SteelBlue, Brushes.SeaGreen, Brushes.DarkGoldenrod, Brushes.IndianRed,
        Brushes.MediumPurple, Brushes.Teal, Brushes.SlateBlue, Brushes.Peru
    };

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var points = Points?.Where(static point => point.Amount >= 0m).ToArray() ?? Array.Empty<OfferingChartPoint>();
        var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
        dc.DrawRoundedRectangle(Brushes.Transparent, new Pen(ThemeBrush("BorderBrush", SystemColors.ControlDarkBrush), 0.5), bounds, 8, 8);
        if (points.Length == 0 || ActualWidth < 80 || ActualHeight < 80)
        {
            DrawText(dc, "No offering data for this selection.", new Point(14, 14), ThemeBrush("TextSecondaryBrush", SystemColors.GrayTextBrush), 12);
            return;
        }

        if (ChartKind is OfferingChartKind.Pie or OfferingChartKind.Donut)
            DrawPie(dc, points, ChartKind == OfferingChartKind.Donut);
        else if (ChartKind == OfferingChartKind.Bar)
            DrawHorizontalBars(dc, points);
        else
            DrawCartesian(dc, points, ChartKind);
    }

    private void DrawCartesian(DrawingContext dc, IReadOnlyList<OfferingChartPoint> points, OfferingChartKind kind)
    {
        const double left = 62, top = 18, right = 18, bottom = 44;
        var width = Math.Max(1, ActualWidth - left - right);
        var height = Math.Max(1, ActualHeight - top - bottom);
        var max = Math.Max(1m, points.Max(static point => point.Amount));
        var axisPen = new Pen(ThemeBrush("BorderStrongBrush", SystemColors.ControlDarkBrush), 1);
        dc.DrawLine(axisPen, new Point(left, top), new Point(left, top + height));
        dc.DrawLine(axisPen, new Point(left, top + height), new Point(left + width, top + height));
        DrawText(dc, max.ToString("N0", CultureInfo.CurrentCulture), new Point(4, top - 7), ThemeBrush("TextSecondaryBrush", SystemColors.GrayTextBrush), 10);
        DrawText(dc, "0", new Point(42, top + height - 7), ThemeBrush("TextSecondaryBrush", SystemColors.GrayTextBrush), 10);

        var slot = width / Math.Max(1, points.Count);
        var coordinates = new List<Point>(points.Count);
        for (var i = 0; i < points.Count; i++)
        {
            var ratio = (double)(points[i].Amount / max);
            var x = left + slot * i + slot / 2;
            var y = top + height - ratio * height;
            coordinates.Add(new Point(x, y));
            if (kind == OfferingChartKind.Column)
            {
                var barWidth = Math.Max(4, slot * 0.58);
                dc.DrawRectangle(Palette[i % Palette.Length], null, new Rect(x - barWidth / 2, y, barWidth, top + height - y));
            }
            if (points.Count <= 12)
            {
                var label = points[i].Label.Length > 12 ? points[i].Label[..12] : points[i].Label;
                DrawText(dc, label, new Point(left + slot * i + 2, top + height + 8), ThemeBrush("TextSecondaryBrush", SystemColors.GrayTextBrush), 9);
            }
        }

        if (kind is OfferingChartKind.Line or OfferingChartKind.Area)
        {
            if (kind == OfferingChartKind.Area && coordinates.Count > 1)
            {
                var geometry = new StreamGeometry();
                using (var context = geometry.Open())
                {
                    context.BeginFigure(new Point(coordinates[0].X, top + height), true, true);
                    context.LineTo(coordinates[0], true, false);
                    for (var i = 1; i < coordinates.Count; i++) context.LineTo(coordinates[i], true, false);
                    context.LineTo(new Point(coordinates[^1].X, top + height), true, false);
                }
                geometry.Freeze();
                dc.PushOpacity(0.24);
                dc.DrawGeometry(Palette[0], null, geometry);
                dc.Pop();
            }
            var pen = new Pen(Palette[0], 2.2);
            for (var i = 1; i < coordinates.Count; i++) dc.DrawLine(pen, coordinates[i - 1], coordinates[i]);
            foreach (var point in coordinates) dc.DrawEllipse(Palette[0], null, point, 3.5, 3.5);
        }
    }

    private void DrawHorizontalBars(DrawingContext dc, IReadOnlyList<OfferingChartPoint> points)
    {
        var visible = points.Take(12).ToArray();
        var max = Math.Max(1m, visible.Max(static point => point.Amount));
        var rowHeight = Math.Max(20, (ActualHeight - 20) / visible.Length);
        var labelWidth = Math.Min(150, ActualWidth * 0.34);
        for (var i = 0; i < visible.Length; i++)
        {
            var y = 10 + i * rowHeight;
            var amountWidth = (ActualWidth - labelWidth - 30) * (double)(visible[i].Amount / max);
            DrawText(dc, visible[i].Label, new Point(8, y + 2), ThemeBrush("TextPrimaryBrush", SystemColors.ControlTextBrush), 10);
            dc.DrawRoundedRectangle(Palette[i % Palette.Length], null, new Rect(labelWidth, y + 2, Math.Max(2, amountWidth), rowHeight - 7), 3, 3);
        }
    }

    private void DrawPie(DrawingContext dc, IReadOnlyList<OfferingChartPoint> points, bool donut)
    {
        var visible = points.Where(static point => point.Amount > 0m).Take(8).ToArray();
        var total = visible.Sum(static point => point.Amount);
        if (total <= 0m) { DrawText(dc, "No positive offering values to chart.", new Point(14, 14), ThemeBrush("TextSecondaryBrush", SystemColors.GrayTextBrush), 12); return; }
        var legendWidth = Math.Min(200, ActualWidth * 0.38);
        var diameter = Math.Max(40, Math.Min(ActualHeight - 24, ActualWidth - legendWidth - 28));
        var center = new Point(14 + diameter / 2, 12 + diameter / 2);
        var radius = diameter / 2;
        var start = -90.0;
        for (var i = 0; i < visible.Length; i++)
        {
            var sweep = (double)(visible[i].Amount / total) * 360.0;
            DrawSlice(dc, center, radius, start, sweep, Palette[i % Palette.Length]);
            start += sweep;
        }
        if (donut) dc.DrawEllipse(ThemeBrush("SurfaceBrush", SystemColors.WindowBrush), null, center, radius * 0.52, radius * 0.52);
        for (var i = 0; i < visible.Length; i++)
        {
            var y = 14 + i * 22;
            dc.DrawRectangle(Palette[i % Palette.Length], null, new Rect(ActualWidth - legendWidth + 4, y + 2, 12, 12));
            DrawText(dc, visible[i].Label + "  " + visible[i].Amount.ToString("N2", CultureInfo.CurrentCulture), new Point(ActualWidth - legendWidth + 22, y), ThemeBrush("TextPrimaryBrush", SystemColors.ControlTextBrush), 10);
        }
    }

    private static void DrawSlice(DrawingContext dc, Point center, double radius, double startDegrees, double sweepDegrees, Brush brush)
    {
        if (sweepDegrees >= 359.999) { dc.DrawEllipse(brush, null, center, radius, radius); return; }
        var start = Polar(center, radius, startDegrees);
        var end = Polar(center, radius, startDegrees + sweepDegrees);
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(center, true, true);
            context.LineTo(start, true, false);
            context.ArcTo(end, new Size(radius, radius), 0, sweepDegrees > 180, SweepDirection.Clockwise, true, false);
        }
        geometry.Freeze();
        dc.DrawGeometry(brush, null, geometry);
    }

    private static Point Polar(Point center, double radius, double degrees)
    {
        var radians = degrees * Math.PI / 180.0;
        return new Point(center.X + radius * Math.Cos(radians), center.Y + radius * Math.Sin(radians));
    }

    private Brush ThemeBrush(string key, Brush fallback) => TryFindResource(key) as Brush ?? fallback;

    private void DrawText(DrawingContext dc, string text, Point point, Brush brush, double size)
    {
        var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip)
        { MaxTextWidth = Math.Max(20, ActualWidth - point.X - 4), Trimming = TextTrimming.CharacterEllipsis };
        dc.DrawText(formatted, point);
    }
}
