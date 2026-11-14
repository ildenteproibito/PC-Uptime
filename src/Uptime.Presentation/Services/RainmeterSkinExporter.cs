using System;
using System.IO;
using System.Text;

namespace Uptime.Presentation.Services;

/// <summary>
/// Generates a ready-to-use Rainmeter skin (.ini) that reads Uptime's live state.ini without third-party plugins.
/// </summary>
public static class RainmeterSkinExporter
{
    public static string GetSkinDirectory()
    {
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(docs, "Rainmeter", "Skins", "Uptime");
    }

    public static string CreateSkin(
        string stateIniPath,
        bool includeUptime,
        bool includeHealth,
        bool includePower,
        bool includeBattery,
        bool includeSpotify)
    {
        var skinDir = GetSkinDirectory();
        Directory.CreateDirectory(skinDir);
        var skinFile = Path.Combine(skinDir, "Uptime.ini");

        var fileUrl = "file://" + stateIniPath.Replace('\\', '/');
        var sb = new StringBuilder();

        sb.AppendLine("[Rainmeter]");
        sb.AppendLine("Update=1000");
        sb.AppendLine("DynamicWindowSize=1");
        sb.AppendLine("AccurateText=1");
        sb.AppendLine();

        sb.AppendLine("[Metadata]");
        sb.AppendLine("Name=Uptime");
        sb.AppendLine("Author=Uptime");
        sb.AppendLine("Information=Live system status widget powered by Uptime Live State Bridge.");
        sb.AppendLine("Version=1.0.0");
        sb.AppendLine();

        sb.AppendLine("[Variables]");
        sb.AppendLine("BgColor=17,20,24,235");
        sb.AppendLine("BorderColor=255,255,255,28");
        sb.AppendLine("AccentColor=123,196,168,255");
        sb.AppendLine("TextColor=243,245,247,255");
        sb.AppendLine("MutedColor=166,175,186,255");
        sb.AppendLine("FontFace=Segoe UI");
        sb.AppendLine();

        sb.AppendLine("[MeasureState]");
        sb.AppendLine("Measure=WebParser");
        sb.AppendLine($"Url={fileUrl}");
        sb.AppendLine("RegExp=(?si)UptimeFormatted=([^\\r\\n]*).*?Health=([^\\r\\n]*).*?PowerPlan=([^\\r\\n]*).*?BatteryPercent=([^\\r\\n]*).*?SpotifyTrack=([^\\r\\n]*)");
        sb.AppendLine("UpdateRate=1");
        sb.AppendLine("CodePage=65001");
        sb.AppendLine();

        sb.AppendLine("[MeasureUptime]");
        sb.AppendLine("Measure=WebParser");
        sb.AppendLine("Url=[MeasureState]");
        sb.AppendLine("StringIndex=1");
        sb.AppendLine();

        sb.AppendLine("[MeasureHealth]");
        sb.AppendLine("Measure=WebParser");
        sb.AppendLine("Url=[MeasureState]");
        sb.AppendLine("StringIndex=2");
        sb.AppendLine();

        sb.AppendLine("[MeasurePower]");
        sb.AppendLine("Measure=WebParser");
        sb.AppendLine("Url=[MeasureState]");
        sb.AppendLine("StringIndex=3");
        sb.AppendLine();

        sb.AppendLine("[MeasureBattery]");
        sb.AppendLine("Measure=WebParser");
        sb.AppendLine("Url=[MeasureState]");
        sb.AppendLine("StringIndex=4");
        sb.AppendLine();

        sb.AppendLine("[MeasureSpotify]");
        sb.AppendLine("Measure=WebParser");
        sb.AppendLine("Url=[MeasureState]");
        sb.AppendLine("StringIndex=5");
        sb.AppendLine("Substitute=\"\":\"-\"");
        sb.AppendLine();

        var y = 38;
        var extraRows = 0;
        if (includeUptime) extraRows++;
        if (includeHealth) extraRows++;
        if (includePower) extraRows++;
        if (includeBattery) extraRows++;
        if (includeSpotify) extraRows++;

        var cardHeight = Math.Max(64, 48 + (extraRows * 22));

        sb.AppendLine("[MeterBackground]");
        sb.AppendLine("Meter=Shape");
        sb.AppendLine($"Shape=Rectangle 0,0,260,{cardHeight},12 | Fill Color #BgColor# | StrokeWidth 1 | Stroke Color #BorderColor#");
        sb.AppendLine();

        sb.AppendLine("[MeterTitle]");
        sb.AppendLine("Meter=String");
        sb.AppendLine("X=16");
        sb.AppendLine("Y=12");
        sb.AppendLine("FontFace=#FontFace#");
        sb.AppendLine("FontSize=10");
        sb.AppendLine("FontWeight=600");
        sb.AppendLine("FontColor=#AccentColor#");
        sb.AppendLine("AntiAlias=1");
        sb.AppendLine("Text=UPTIME");
        sb.AppendLine();

        if (includeUptime)
        {
            AppendRow(sb, "MeterUptime", "Uptime", "[MeasureUptime]", y, bold: true);
            y += 22;
        }

        if (includeHealth)
        {
            AppendRow(sb, "MeterHealth", "Health", "[MeasureHealth]", y, bold: false);
            y += 22;
        }

        if (includePower)
        {
            AppendRow(sb, "MeterPower", "Power", "[MeasurePower]", y, bold: false);
            y += 22;
        }

        if (includeBattery)
        {
            AppendRow(sb, "MeterBattery", "Battery", "[MeasureBattery]%", y, bold: false);
            y += 22;
        }

        if (includeSpotify)
        {
            AppendRow(sb, "MeterSpotify", "Spotify", "[MeasureSpotify]", y, bold: false, clip: true);
        }

        File.WriteAllText(skinFile, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return skinDir;
    }

    private static void AppendRow(StringBuilder sb, string meterName, string label, string valueExpr, int y, bool bold, bool clip = false)
    {
        sb.AppendLine($"[{meterName}Label]");
        sb.AppendLine("Meter=String");
        sb.AppendLine("X=16");
        sb.AppendLine($"Y={y}");
        sb.AppendLine("FontFace=#FontFace#");
        sb.AppendLine("FontSize=9");
        sb.AppendLine("FontColor=#MutedColor#");
        sb.AppendLine("AntiAlias=1");
        sb.AppendLine($"Text={label}");
        sb.AppendLine();

        sb.AppendLine($"[{meterName}Value]");
        sb.AppendLine("Meter=String");
        sb.AppendLine("X=244");
        sb.AppendLine($"Y={y}");
        sb.AppendLine("StringAlign=Right");
        sb.AppendLine($"MeasureName={ExtractMeasureName(valueExpr)}");
        sb.AppendLine("FontFace=#FontFace#");
        sb.AppendLine("FontSize=9");
        if (bold) sb.AppendLine("FontWeight=600");
        sb.AppendLine("FontColor=#TextColor#");
        sb.AppendLine("AntiAlias=1");
        if (clip)
        {
            sb.AppendLine("W=160");
            sb.AppendLine("ClipString=1");
        }
        sb.AppendLine($"Text={valueExpr}");
        sb.AppendLine();
    }

    private static string ExtractMeasureName(string expr)
    {
        var start = expr.IndexOf('[');
        var end = expr.IndexOf(']');
        if (start >= 0 && end > start + 1)
            return expr.Substring(start + 1, end - start - 1);
        return "MeasureUptime";
    }
}
