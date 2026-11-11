using Uptime.Core;
using Xunit;

namespace Uptime.Core.Tests;

public class AtomicJsonStoreTests : IDisposable
{
    private sealed class Data { public int N { get; set; } }

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "uptime-tests-" + Guid.NewGuid());
    private string FilePath => Path.Combine(_dir, "stats.json");
    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

    [Fact]
    public void Load_WhenNothingExists_ReturnsMissing()
    {
        var r = new AtomicJsonStore<Data>(FilePath).Load();
        Assert.Equal(LoadStatus.Missing, r.Status);
        Assert.Null(r.Value);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var store = new AtomicJsonStore<Data>(FilePath);
        store.Save(new Data { N = 7 });
        var r = store.Load();
        Assert.Equal(LoadStatus.Ok, r.Status);
        Assert.Equal(7, r.Value!.N);
    }

    [Fact]
    public void CorruptPrimary_RecoversFromBackup_AndQuarantinesCorruptFile()
    {
        var store = new AtomicJsonStore<Data>(FilePath);
        store.Save(new Data { N = 1 });
        store.Save(new Data { N = 2 }); // backup holds N = 1
        File.WriteAllText(FilePath, "{ not json");

        var r = store.Load();
        Assert.Equal(LoadStatus.RecoveredFromBackup, r.Status);
        Assert.Equal(1, r.Value!.N);
        Assert.Single(r.QuarantinedFiles);
        Assert.True(File.Exists(r.QuarantinedFiles[0]));
    }

    [Fact]
    public void BothInvalid_IsUnrecoverable_ButNothingIsDeleted()
    {
        var store = new AtomicJsonStore<Data>(FilePath);
        store.Save(new Data { N = 1 });
        store.Save(new Data { N = 2 });
        File.WriteAllText(FilePath, "garbage");
        File.WriteAllText(store.BackupPath, "garbage");

        var r = store.Load();
        Assert.Equal(LoadStatus.Unrecoverable, r.Status);
        Assert.Equal(2, r.QuarantinedFiles.Count);
        Assert.All(r.QuarantinedFiles, f => Assert.True(File.Exists(f)));
    }

    [Fact]
    public void InterruptedWrite_LeavesPrimaryIntact()
    {
        var store = new AtomicJsonStore<Data>(FilePath);
        store.Save(new Data { N = 5 });
        File.WriteAllText(FilePath + ".tmp", "{ half written");

        var r = store.Load();
        Assert.Equal(LoadStatus.Ok, r.Status);
        Assert.Equal(5, r.Value!.N);
    }
}

public class HealthTests
{
    [Theory]
    [InlineData(2.9, HealthLevel.Good)]
    [InlineData(3.2, HealthLevel.Attention)]
    [InlineData(7.0, HealthLevel.Attention)]
    [InlineData(7.1, HealthLevel.Maintenance)]
    public void For_UsesThresholds(double days, HealthLevel expected) =>
        Assert.Equal(expected, Health.For(TimeSpan.FromDays(days)));

    [Fact]
    public void DaysUntilMaintenance_RoundsUp() =>
        Assert.Equal(4, Health.DaysUntilMaintenance(new TimeSpan(3, 4, 12, 30)));

    [Fact]
    public void Parts_SplitCorrectly()
    {
        var p = UptimeParts.From(new TimeSpan(3, 4, 12, 30));
        Assert.Equal(new UptimeParts(3, 4, 12, 30), p);
    }
}

public class StabilityAnalyzerTests
{
    private const string KG = "Microsoft-Windows-Kernel-General";
    private const string KP = "Microsoft-Windows-Kernel-Power";
    private const string EL = "EventLog";
    private static readonly DateTime T0 = new(2026, 10, 1, 8, 0, 0);

    [Fact]
    public void Classifies_Clean_Unexpected_And_InProgress()
    {
        var events = new List<LogEvent>
        {
            new(T0, 12, KG),                          // boot 1
            new(T0.AddHours(10), 13, KG),             // clean shutdown
            new(T0.AddDays(1), 12, KG),               // boot 2
            new(T0.AddDays(1).AddHours(5), 12, KG),   // boot 3 (no preceding 13: crash)
            new(T0.AddDays(1).AddHours(5).AddSeconds(20), 41, KP),
            new(T0.AddDays(1).AddHours(5).AddSeconds(45), 6008, EL),
        };

        var r = StabilityAnalyzer.Analyze(events, T0.AddDays(2));

        Assert.Equal(new[] { SessionOutcome.Clean, SessionOutcome.Unexpected, SessionOutcome.InProgress },
            r.Sessions.Select(s => s.Outcome).ToArray());
        Assert.Equal(50.0, r.ScorePercent);
        Assert.Equal(DataConfidence.InsufficientData, r.Confidence);
    }

    [Fact]
    public void Events41And6008_WithinWindow_AreOneIncident_WithHighConfidence()
    {
        var events = new List<LogEvent>
        {
            new(T0, 41, KP),
            new(T0.AddSeconds(100), 6008, EL),
            new(T0.AddHours(3), 6008, EL),
        };

        var r = StabilityAnalyzer.Analyze(events, T0.AddDays(1));

        Assert.Equal(2, r.Incidents.Count);
        Assert.Equal(IncidentConfidence.High, r.Incidents[0].Confidence);
        Assert.Equal(IncidentConfidence.Medium, r.Incidents[1].Confidence);
    }

    [Fact]
    public void FallsBackTo6005_WhenNoKernelGeneralBoots()
    {
        var events = new List<LogEvent> { new(T0, 6005, EL), new(T0.AddDays(2), 6005, EL) };
        var r = StabilityAnalyzer.Analyze(events, T0.AddDays(3));
        Assert.Equal(2, r.Sessions.Count);
    }

    [Fact]
    public void NoEvents_YieldsNoScore()
    {
        var r = StabilityAnalyzer.Analyze(Array.Empty<LogEvent>(), T0);
        Assert.Null(r.ScorePercent);
        Assert.Empty(r.Sessions);
    }

    [Fact]
    public void Incident_IncludesBsodExplanation_WhenBugcheckPresent()
    {
        var events = new List<LogEvent>
        {
            new(T0, 41, KP, 0x000000D1),
            new(T0.AddSeconds(10), 6008, EL),
        };

        var r = StabilityAnalyzer.Analyze(events, T0.AddDays(1));
        Assert.Single(r.Incidents);
        Assert.NotNull(r.Incidents[0].Explanation);
        Assert.Equal("DRIVER_IRQL_NOT_LESS_OR_EQUAL", r.Incidents[0].Explanation!.Name);
    }
}

public class BsodExplainerTests
{
    [Fact]
    public void Explain_CodeZero_ReturnsPowerLoss()
    {
        var exp = BsodExplainer.Explain(0);
        Assert.Equal("POWER_LOSS_OR_HARD_RESET", exp.Name);
        Assert.Equal("0x00000000", exp.HexCode);
    }

    [Theory]
    [InlineData(0x0000001A, "MEMORY_MANAGEMENT")]
    [InlineData(0x000000D1, "DRIVER_IRQL_NOT_LESS_OR_EQUAL")]
    [InlineData(0x0000003B, "SYSTEM_SERVICE_EXCEPTION")]
    [InlineData(0x00000124, "WHEA_UNCORRECTABLE_ERROR")]
    public void Explain_KnownCodes_ReturnAccurateNames(long code, string expectedName)
    {
        var exp = BsodExplainer.Explain(code);
        Assert.Equal(expectedName, exp.Name);
        Assert.False(string.IsNullOrWhiteSpace(exp.Meaning));
        Assert.False(string.IsNullOrWhiteSpace(exp.Advice));
    }

    [Fact]
    public void Explain_UnknownCode_ReturnsFormattedHex()
    {
        var exp = BsodExplainer.Explain(0x00000ABC);
        Assert.Equal("SYSTEM_CRASH", exp.Name);
        Assert.Equal("0x00000ABC", exp.HexCode);
    }

    [Fact]
    public void Explain_EnglishLanguage_ReturnsEnglishTexts()
    {
        var exp = BsodExplainer.Explain(0x000000D1, "en");
        Assert.Contains("driver", exp.Meaning, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("reinstall", exp.Advice, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Explain_ItalianLanguage_ReturnsItalianTexts()
    {
        var exp = BsodExplainer.Explain(0x000000D1, "it");
        Assert.Contains("Reinstalla", exp.Advice, StringComparison.OrdinalIgnoreCase);
    }
}
