using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Uptime.Presentation.Controls;

/// <summary>
/// Tick ruler, visual signature of Uptime: one tick = one hour.
/// Filled green ticks up to GoodTicks, amber beyond; white indicator on current point.
/// </summary>
public sealed class TimeRuler : Control
{
    public static readonly StyledProperty<int> TotalTicksProperty =
        AvaloniaProperty.Register<TimeRuler, int>(nameof(TotalTicks), 168);
    public static readonly StyledProperty<int> FilledTicksProperty =
        AvaloniaProperty.Register<TimeRuler, int>(nameof(FilledTicks));
    public static readonly StyledProperty<int> GoodTicksProperty =
        AvaloniaProperty.Register<TimeRuler, int>(nameof(GoodTicks), 72);
    public static readonly StyledProperty<int> MajorEveryProperty =
        AvaloniaProperty.Register<TimeRuler, int>(nameof(MajorEvery), 24);
    public static readonly StyledProperty<IReadOnlyList<string>?> LabelsProperty =
        AvaloniaProperty.Register<TimeRuler, IReadOnlyList<string>?>(nameof(Labels));

    static TimeRuler()
    {
        AffectsRender<TimeRuler>(TotalTicksProperty, FilledTicksProperty, GoodTicksProperty, MajorEveryProperty, LabelsProperty);
    }

    public int TotalTicks { get => GetValue(TotalTicksProperty); set => SetValue(TotalTicksProperty, value); }
    public int FilledTicks { get => GetValue(FilledTicksProperty); set => SetValue(FilledTicksProperty, value); }
    public int GoodTicks { get => GetValue(GoodTicksProperty); set => SetValue(GoodTicksProperty, value); }
    public int MajorEvery { get => GetValue(MajorEveryProperty); set => SetValue(MajorEveryProperty, value); }
    public IReadOnlyList<string>? Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    private static readonly IPen Muted = new Pen(new SolidColorBrush(Color.Parse("#2A2E36")), 1);
    private static readonly IPen Good = new Pen(new SolidColorBrush(Color.Parse("#7BC4A8")), 1);
    private static readonly IPen Amber = new Pen(new SolidColorBrush(Color.Parse("#E0A458")), 1);
    private static readonly IPen Major = new Pen(new SolidColorBrush(Color.Parse("#3A3F48")), 1);
    private static readonly IPen Marker = new Pen(Brushes.White, 2);
    private static readonly IBrush LabelBrush = new SolidColorBrush(Color.Parse("#8D939C"));
    private static readonly Typeface LabelFace = new("Segoe UI Variable, Segoe UI, sans-serif");

    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width;
        var total = TotalTicks;
        if (w <= 0 || total <= 0) return;

        const double baseY = 40;
        var step = w / total;
        var filled = Math.Clamp(FilledTicks, 0, total);

        for (var i = 0; i < total; i++)
        {
            var x = Math.Round(i * step) + 0.5;
            var pen = i >= filled ? Muted : i < GoodTicks ? Good : Amber;
            ctx.DrawLine(pen, new Point(x, baseY - 16), new Point(x, baseY));
        }

        if (MajorEvery > 0)
        {
            for (var i = 0; i <= total; i += MajorEvery)
            {
                var x = Math.Min(Math.Round(i * step), w - 1) + 0.5;
                ctx.DrawLine(Major, new Point(x, baseY - 28), new Point(x, baseY));
            }
        }

        var mx = Math.Min(filled * step, w - 1);
        ctx.DrawLine(Marker, new Point(mx, 0), new Point(mx, baseY));

        var labels = Labels;
        if (labels is not { Count: > 0 }) return;
        var n = labels.Count;
        for (var k = 0; k < n; k++)
        {
            var ft = new FormattedText(labels[k], CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, LabelFace, 12, LabelBrush);
            var x = n == 1 ? 0 : k * (w / (n - 1));
            x = k == 0 ? x : k == n - 1 ? x - ft.Width : x - ft.Width / 2;
            ctx.DrawText(ft, new Point(x, baseY + 8));
        }
    }
}
