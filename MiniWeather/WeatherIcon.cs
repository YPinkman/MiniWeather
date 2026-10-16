using System.Windows;
using System.Windows.Media;

namespace MiniWeather;

public sealed class WeatherIcon : FrameworkElement
{
    public static readonly DependencyProperty CodeProperty = DependencyProperty.Register(nameof(Code), typeof(int), typeof(WeatherIcon),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));
    public int Code { get => (int)GetValue(CodeProperty); set => SetValue(CodeProperty, value); }
    public static readonly DependencyProperty WindSpeedProperty = DependencyProperty.Register(nameof(WindSpeed), typeof(double), typeof(WeatherIcon),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    public double WindSpeed { get => (double)GetValue(WindSpeedProperty); set => SetValue(WindSpeedProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var scale = Math.Min(ActualWidth, ActualHeight) / 64;
        if (scale <= 0) return;
        dc.PushTransform(new ScaleTransform(scale, scale));
        var kind = WeatherCodes.Kind(Code, WindSpeed);
        var sun = new SolidColorBrush(Color.FromRgb(255, 196, 91));
        var cloud = new SolidColorBrush(Color.FromRgb(229, 237, 246));
        var edge = new Pen(new SolidColorBrush(Color.FromRgb(141, 166, 188)), 1.5);
        if (kind == "wind")
        {
            var windPen = new Pen(new SolidColorBrush(Color.FromRgb(144, 185, 202)), 3)
            { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            foreach (var (y, start, end) in new[] { (22d, 11d, 50d), (34d, 17d, 55d), (46d, 9d, 45d) })
            {
                var path = new StreamGeometry();
                using (var g = path.Open())
                {
                    g.BeginFigure(new Point(start, y), false, false);
                    g.BezierTo(new Point(start + 12, y - 5), new Point(end - 13, y + 5), new Point(end, y), true, false);
                }
                path.Freeze(); dc.DrawGeometry(null, windPen, path);
            }
            dc.Pop(); return;
        }
        if (kind is "sun" or "partly")
        {
            var center = kind == "sun" ? new Point(32, 30) : new Point(23, 22);
            dc.DrawEllipse(sun, null, center, 12, 12);
            for (var i = 0; i < 8; i++)
            {
                var a = Math.PI * i / 4;
                dc.DrawLine(new Pen(sun, 2.5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round },
                    new Point(center.X + Math.Cos(a) * 16, center.Y + Math.Sin(a) * 16),
                    new Point(center.X + Math.Cos(a) * 21, center.Y + Math.Sin(a) * 21));
            }
        }
        if (kind != "sun")
        {
            dc.DrawEllipse(cloud, edge, new Point(26, 35), 13, 11);
            dc.DrawEllipse(cloud, edge, new Point(38, 31), 15, 14);
            dc.DrawRoundedRectangle(cloud, edge, new Rect(14, 32, 40, 17), 8, 8);
            if (kind == "rain" || kind == "storm")
            {
                var rainPen = new Pen(new SolidColorBrush(Color.FromRgb(92, 176, 226)), 3) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                foreach (var x in new[] { 24, 36, 48 }) dc.DrawLine(rainPen, new Point(x, 52), new Point(x - 2, 58));
            }
            if (kind == "snow")
            {
                var snowPen = new Pen(Brushes.White, 1.7);
                foreach (var x in new[] { 24, 37, 50 })
                {
                    dc.DrawLine(snowPen, new Point(x - 3, 55), new Point(x + 3, 55));
                    dc.DrawLine(snowPen, new Point(x, 52), new Point(x, 58));
                }
            }
            if (kind == "storm")
            {
                var bolt = new StreamGeometry();
                using (var g = bolt.Open()) { g.BeginFigure(new Point(36, 44), true, true); g.LineTo(new Point(29, 55), true, false); g.LineTo(new Point(36, 53), true, false); g.LineTo(new Point(32, 62), true, false); g.LineTo(new Point(45, 49), true, false); g.LineTo(new Point(37, 50), true, false); }
                bolt.Freeze(); dc.DrawGeometry(sun, null, bolt);
            }
            if (kind == "fog")
            {
                var fogPen = new Pen(new SolidColorBrush(Color.FromRgb(172, 193, 206)), 2);
                dc.DrawLine(fogPen, new Point(17, 54), new Point(47, 54));
                dc.DrawLine(fogPen, new Point(23, 59), new Point(53, 59));
            }
        }
        dc.Pop();
    }
}
