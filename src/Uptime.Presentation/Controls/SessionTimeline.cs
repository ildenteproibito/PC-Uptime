using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Uptime.Presentation.Controls;

/// <summary>Segmento della cronologia: posizione e lunghezza come frazioni (0..1) della finestra.</summary>
public sealed record TimelineSegment(double Start, double Length, IBrush Brush);

/// <summary>Scala a tacche (una per giorno) con le sessioni disegnate come barre sopra.</summary>
public sealed class SessionTimeline : Control
{
    public static readonly StyledProperty<IReadOnlyList<TimelineSegment>?> SegmentsProperty =
        AvaloniaProperty.Register<SessionTimeline, IReadOnlyList<TimelineSegment>?>(nameof(Segments));
    public static readonly StyledProperty<int> DaysProperty =
        AvaloniaProperty.Register<SessionTimeline, int>(nameof(Days), 30);
    public static readonly StyledProperty<string?> LeftLabelProperty =
        AvaloniaProperty.Register<SessionTimeline, string?>(nameof(LeftLabel));
    public static readonly StyledProperty<string?> RightLabelProperty =
        AvaloniaProperty.Register<SessionTimeline, string?>(nameof(RightLabel));

    static SessionTimeline()
    {
        AffectsRender<SessionTimeline>(SegmentsProperty, DaysProperty, LeftLabelProperty, RightLabelProperty);
    }

    public IReadOnlyList<TimelineSegment>? Segments { get => GetValue(SegmentsProperty); set => SetValue(SegmentsProperty, value); }
    public int Days { get => GetValue(DaysProperty); set => SetValue(DaysProperty, value); }
    public string? LeftLabel { get => GetValue(LeftLabelProperty); set => SetValue(LeftLabelProperty, value); }
    public string? RightLabel { get => GetValue(RightLabelProperty); set => SetValue(RightLabelProperty, value); }

    private static readonly IPen Tick = new Pen(new SolidColorBrush(Color.Parse("#3A3F48")), 1);
    private static readonly IBrush LabelBrush = new SolidColorBrush(Color.Parse("#8D939C"));
    private static readonly Typeface LabelFace = new("Segoe UI Variable, Segoe UI, sans-serif");

    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width;
        if (w <= 0 || Days <= 0) return;

        const double barH = 6, tickTop = 22, tickBottom = 38;
        var step = w / Days;

        if (Segments is { } segments)
        {
            foreach (var s in segments)
            {
                var x = Math.Clamp(s.Start, 0, 1) * w;
                var len = Math.Max(3, Math.Clamp(s.Length, 0, 1) * w);
                len = Math.Min(len, w - x);
                if (len <= 0) continue;
                ctx.DrawRectangle(s.Brush, null, new Rect(x, 0, len, barH), 3, 3);
            }
        }

        for (var i = 0; i <= Days; i++)
        {
            var x = Math.Min(Math.Round(i * step), w - 1) + 0.5;
            ctx.DrawLine(Tick, new Point(x, tickTop), new Point(x, tickBottom));
        }

        if (!string.IsNullOrEmpty(LeftLabel))
            ctx.DrawText(Make(LeftLabel), new Point(0, tickBottom + 8));
        if (!string.IsNullOrEmpty(RightLabel))
        {
            var ft = Make(RightLabel);
            ctx.DrawText(ft, new Point(w - ft.Width, tickBottom + 8));
        }
    }

    private static FormattedText Make(string text) =>
        new(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, LabelFace, 12, LabelBrush);
}
