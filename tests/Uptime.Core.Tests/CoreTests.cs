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
        store.Save(new Data { N = 2 }); // il backup contiene N = 1
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
            new(T0, 12, KG),                          // avvio 1
            new(T0.AddHours(10), 13, KG),             // spegnimento pulito
            new(T0.AddDays(1), 12, KG),               // avvio 2
            new(T0.AddDays(1).AddHours(5), 12, KG),   // avvio 3 (nessun 13 prima: crash)
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
}
