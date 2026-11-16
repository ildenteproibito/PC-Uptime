using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace Uptime.Presentation.Services;

public sealed record ThemeDefinition(
    string Id,
    string AccentColor,
    string BgColor,
    string SurfaceColor,
    string LineColor,
    string RowLineColor,
    string OutlineColor,
    string TextColor,
    string MutedColor,
    string DimColor,
    string AmberColor,
    string OrangeColor
);

public static class ThemeManager
{
    public const string DefaultThemeId = "uptime-mint";

    public static IReadOnlyList<ThemeDefinition> Themes { get; } = new List<ThemeDefinition>
    {
        new(
            Id: "uptime-mint",
            AccentColor: "#7BC4A8",
            BgColor: "#0F1114",
            SurfaceColor: "#1B1E24",
            LineColor: "#1D2026",
            RowLineColor: "#1A1D22",
            OutlineColor: "#2A2E36",
            TextColor: "#E8EAED",
            MutedColor: "#9AA0A9",
            DimColor: "#8D939C",
            AmberColor: "#E0A458",
            OrangeColor: "#E8845A"
        ),
        new(
            Id: "apollo-11",
            AccentColor: "#10B981",
            BgColor: "#070D09",
            SurfaceColor: "#0E1812",
            LineColor: "#14251B",
            RowLineColor: "#101E16",
            OutlineColor: "#1D3728",
            TextColor: "#ECFDF5",
            MutedColor: "#6EE7B7",
            DimColor: "#059669",
            AmberColor: "#F59E0B",
            OrangeColor: "#F97316"
        ),
        new(
            Id: "solaris-92",
            AccentColor: "#FACC15",
            BgColor: "#14120B",
            SurfaceColor: "#1F1A10",
            LineColor: "#2C2516",
            RowLineColor: "#241E12",
            OutlineColor: "#42361E",
            TextColor: "#FEF9C3",
            MutedColor: "#CA8A04",
            DimColor: "#854D0E",
            AmberColor: "#F59E0B",
            OrangeColor: "#EA580C"
        ),
        new(
            Id: "nordic-frost",
            AccentColor: "#06B6D4",
            BgColor: "#071018",
            SurfaceColor: "#0E1C28",
            LineColor: "#142A3D",
            RowLineColor: "#102231",
            OutlineColor: "#1E3E5B",
            TextColor: "#ECFEFF",
            MutedColor: "#67E8F9",
            DimColor: "#0891B2",
            AmberColor: "#F59E0B",
            OrangeColor: "#FB923C"
        ),
        new(
            Id: "kyoto-rain",
            AccentColor: "#FB7185",
            BgColor: "#160C12",
            SurfaceColor: "#25141E",
            LineColor: "#361D2B",
            RowLineColor: "#2B1622",
            OutlineColor: "#4E293F",
            TextColor: "#FFF1F2",
            MutedColor: "#FDA4AF",
            DimColor: "#E11D48",
            AmberColor: "#FBBF24",
            OrangeColor: "#F97316"
        ),
        new(
            Id: "monolith-2001",
            AccentColor: "#E4E4E7",
            BgColor: "#000000",
            SurfaceColor: "#141414",
            LineColor: "#222222",
            RowLineColor: "#1A1A1A",
            OutlineColor: "#333333",
            TextColor: "#FFFFFF",
            MutedColor: "#A1A1AA",
            DimColor: "#71717A",
            AmberColor: "#D4D4D8",
            OrangeColor: "#E4E4E7"
        ),
        new(
            Id: "voyager-space",
            AccentColor: "#8B5CF6",
            BgColor: "#0B0818",
            SurfaceColor: "#15102A",
            LineColor: "#221943",
            RowLineColor: "#1B1336",
            OutlineColor: "#342666",
            TextColor: "#F5F3FF",
            MutedColor: "#C4B5FD",
            DimColor: "#7C3AED",
            AmberColor: "#F59E0B",
            OrangeColor: "#F97316"
        ),
        new(
            Id: "cern-cherenkov",
            AccentColor: "#2563EB",
            BgColor: "#050E1E",
            SurfaceColor: "#0B1933",
            LineColor: "#122750",
            RowLineColor: "#0E1F3F",
            OutlineColor: "#1A3975",
            TextColor: "#EFF6FF",
            MutedColor: "#93C5FD",
            DimColor: "#1D4ED8",
            AmberColor: "#F59E0B",
            OrangeColor: "#F97316"
        ),
        new(
            Id: "desert-dune",
            AccentColor: "#D97706",
            BgColor: "#16110D",
            SurfaceColor: "#231B14",
            LineColor: "#35281E",
            RowLineColor: "#2A2018",
            OutlineColor: "#4E3C2C",
            TextColor: "#FEF3C7",
            MutedColor: "#FBBF24",
            DimColor: "#B45309",
            AmberColor: "#F59E0B",
            OrangeColor: "#EA580C"
        ),
        new(
            Id: "crimson-protocol",
            AccentColor: "#EF4444",
            BgColor: "#160809",
            SurfaceColor: "#250D0F",
            LineColor: "#381417",
            RowLineColor: "#2D1013",
            OutlineColor: "#521D22",
            TextColor: "#FEE2E2",
            MutedColor: "#FCA5A5",
            DimColor: "#DC2626",
            AmberColor: "#F59E0B",
            OrangeColor: "#F97316"
        ),
        new(
            Id: "cyberdeck-84",
            AccentColor: "#A3E635",
            BgColor: "#0A0F0D",
            SurfaceColor: "#111B17",
            LineColor: "#1B2A24",
            RowLineColor: "#15221D",
            OutlineColor: "#283E35",
            TextColor: "#F7FEE7",
            MutedColor: "#BEF264",
            DimColor: "#65A30D",
            AmberColor: "#FACC15",
            OrangeColor: "#FB923C"
        ),
        new(
            Id: "mars-base",
            AccentColor: "#F97316",
            BgColor: "#170B07",
            SurfaceColor: "#27120C",
            LineColor: "#391B11",
            RowLineColor: "#2E150D",
            OutlineColor: "#532718",
            TextColor: "#FFF7ED",
            MutedColor: "#FDBA74",
            DimColor: "#C2410C",
            AmberColor: "#FBBF24",
            OrangeColor: "#EA580C"
        ),
        new(
            Id: "outrun-synth",
            AccentColor: "#EC4899",
            BgColor: "#140716",
            SurfaceColor: "#230D27",
            LineColor: "#36143C",
            RowLineColor: "#2B1030",
            OutlineColor: "#4E1D56",
            TextColor: "#FDF2F8",
            MutedColor: "#F472B6",
            DimColor: "#BE185D",
            AmberColor: "#FBBF24",
            OrangeColor: "#FB7185"
        ),
        new(
            Id: "abyssal-trench",
            AccentColor: "#14B8A6",
            BgColor: "#051211",
            SurfaceColor: "#0B201E",
            LineColor: "#11322F",
            RowLineColor: "#0E2825",
            OutlineColor: "#194A45",
            TextColor: "#CCFBF1",
            MutedColor: "#5EEAD4",
            DimColor: "#0D9488",
            AmberColor: "#F59E0B",
            OrangeColor: "#FB923C"
        ),
        new(
            Id: "royal-amethyst",
            AccentColor: "#C084FC",
            BgColor: "#110A1A",
            SurfaceColor: "#1D112B",
            LineColor: "#2C1A42",
            RowLineColor: "#231534",
            OutlineColor: "#402660",
            TextColor: "#FAF5FF",
            MutedColor: "#D8B4FE",
            DimColor: "#9333EA",
            AmberColor: "#F59E0B",
            OrangeColor: "#F43F5E"
        )
    };

    public static ThemeDefinition CurrentTheme { get; private set; } = Themes[0];

    public static void ApplyTheme(string? themeId)
    {
        var theme = Themes.FirstOrDefault(t => t.Id == themeId) ?? Themes[0];
        CurrentTheme = theme;

        if (Application.Current is null) return;

        UpdateBrush("BgBrush", theme.BgColor);
        UpdateBrush("SurfaceBrush", theme.SurfaceColor);
        UpdateBrush("LineBrush", theme.LineColor);
        UpdateBrush("RowLineBrush", theme.RowLineColor);
        UpdateBrush("OutlineBrush", theme.OutlineColor);
        UpdateBrush("TextBrush", theme.TextColor);
        UpdateBrush("MutedBrush", theme.MutedColor);
        UpdateBrush("DimBrush", theme.DimColor);
        UpdateBrush("AccentBrush", theme.AccentColor);
        UpdateBrush("AmberBrush", theme.AmberColor);
        UpdateBrush("OrangeBrush", theme.OrangeColor);

        // Toggle Switch & Controls accents
        UpdateBrush("ToggleSwitchFillOn", theme.AccentColor);
        UpdateBrush("ToggleSwitchStrokeOn", theme.AccentColor);
        UpdateBrush("ToggleSwitchKnobFillOn", theme.BgColor);
    }

    public static void SetGlassContainersEnabled(bool enabled)
    {
        if (Application.Current is null) return;

        UpdateBrush("GlassCardBgBrush", enabled ? "#BA0B0F15" : "#00000000");
        UpdateBrush("GlassCardBorderBrush", enabled ? "#2EFFFFFF" : "#00000000");
        UpdateBrush("GhostButtonBgBrush", enabled ? "#A60B0F15" : "#147BC4A8");
    }

    private static void UpdateBrush(string key, string hex)
    {
        if (Application.Current is null) return;

        var color = Color.Parse(hex);
        var brush = new SolidColorBrush(color);

        Application.Current.Resources[key] = brush;

        foreach (var style in Application.Current.Styles)
        {
            if (style is Styles s && s.Resources.ContainsKey(key))
            {
                s.Resources[key] = brush;
            }
        }
    }
}
