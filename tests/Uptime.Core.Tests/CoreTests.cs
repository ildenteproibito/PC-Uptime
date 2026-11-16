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

    [Fact]
    public void Localization_ThemeKeys_PresentInBothLanguages()
    {
        var loc = Localizer.Instance;
        var original = loc.Language;
        try
        {
            loc.Language = "it";
            Assert.False(string.IsNullOrWhiteSpace(loc["LblThemes"]));
            Assert.False(string.IsNullOrWhiteSpace(loc["ThemesDesc"]));
            Assert.Equal("Attivo", loc["ThemeActive"]);
            Assert.Equal("Scorri a sinistra", loc["TooltipScrollLeft"]);
            Assert.Equal("Scorri a destra", loc["TooltipScrollRight"]);

            loc.Language = "en";
            Assert.False(string.IsNullOrWhiteSpace(loc["LblThemes"]));
            Assert.False(string.IsNullOrWhiteSpace(loc["ThemesDesc"]));
            Assert.Equal("Active", loc["ThemeActive"]);
            Assert.Equal("Scroll left", loc["TooltipScrollLeft"]);
            Assert.Equal("Scroll right", loc["TooltipScrollRight"]);
        }
        finally
        {
            loc.Language = original;
        }
    }

    [Fact]
    public void EventLogLocalizer_TranslatesScm7000AndLevels_AcrossAllThreeLanguages()
    {
        const string sample = "The Brave Update Service (brave) service failed to start due to the following error:\nThe service did not respond to the start or control request in a timely fashion.";

        Assert.Equal("Errore", EventLogLocalizer.LocalizeLevel("Error", "it"));
        Assert.Equal("Critico", EventLogLocalizer.LocalizeLevel("Critical", "it"));
        Assert.Equal("Ошибка", EventLogLocalizer.LocalizeLevel("Errore", "ru"));
        Assert.Equal("Критическая", EventLogLocalizer.LocalizeLevel("Critico", "ru"));

        var itMsg = EventLogLocalizer.LocalizeMessage(7000, "Service Control Manager", sample, "it");
        Assert.Contains("Brave Update Service (brave)", itMsg);
        Assert.Contains("non è riuscito ad avviarsi", itMsg);
        Assert.Contains("tempo utile", itMsg);

        var ruMsg = EventLogLocalizer.LocalizeMessage(7000, "Service Control Manager", sample, "ru");
        Assert.Contains("Brave Update Service (brave)", ruMsg);
        Assert.Contains("Сбой при запуске службы", ruMsg);

        var enMsg = EventLogLocalizer.LocalizeMessage(7000, "Service Control Manager", itMsg, "en");
        Assert.Contains("failed to start", enMsg);
        Assert.Contains("timely fashion", enMsg);
    }

    [Fact]
    public void UpdateManifestValidator_ValidatesVersionAndTrustedManifest()
    {
        Assert.True(UpdateManifestValidator.IsNewerVersion("1.0.0", "1.0.1"));
        Assert.True(UpdateManifestValidator.IsNewerVersion("1.0.0", "v1.2.0"));
        Assert.False(UpdateManifestValidator.IsNewerVersion("1.0.0", "1.0.0"));
        Assert.False(UpdateManifestValidator.IsNewerVersion("1.2.0", "1.1.9"));

        var validSha = new string('a', 64);
        var ok = new UpdateManifest("1.0.1", "https://uptime-pc.vercel.app/downloads/Uptime-1.0.0-win-x64.zip", validSha, "Notes");
        var badHost = new UpdateManifest("1.0.1", "https://evil.example/update.zip", validSha);
        var badSha = new UpdateManifest("1.0.1", "https://uptime-pc.vercel.app/downloads/Uptime-1.0.0-win-x64.zip", "1234");

        Assert.True(UpdateManifestValidator.IsValidManifest(ok));
        Assert.False(UpdateManifestValidator.IsValidManifest(badHost));
        Assert.False(UpdateManifestValidator.IsValidManifest(badSha));
    }

    [Fact]
    public void StabilityAnalyzer_InitiatedShutdownFollowedByCrash_IsUnexpected()
    {
        var t0 = new DateTime(2026, 11, 14, 10, 0, 0);
        var events = new[]
        {
            new LogEvent(t0, 12, "Microsoft-Windows-Kernel-General"),
            new LogEvent(t0.AddHours(2), 1074, "User32"),
            new LogEvent(t0.AddHours(2).AddMinutes(2), 12, "Microsoft-Windows-Kernel-General"),
            new LogEvent(t0.AddHours(2).AddMinutes(2).AddSeconds(5), 41, "Microsoft-Windows-Kernel-Power", 0),
        };

        var report = StabilityAnalyzer.Analyze(events, t0.AddHours(3));
        Assert.Equal(2, report.Sessions.Count);
        Assert.Equal(SessionOutcome.Unexpected, report.Sessions[0].Outcome);
        Assert.Equal(1, report.UnexpectedCount);
    }

    [Fact]
    public void StabilityAnalyzer_RapidRebootsWithin10Minutes_DoNotBleedCrashIntoEarlierCleanBoot()
    {
        var t0 = new DateTime(2026, 11, 14, 10, 0, 0);
        var events = new[]
        {
            new LogEvent(t0, 12, "Microsoft-Windows-Kernel-General"),
            new LogEvent(t0.AddMinutes(2), 13, "Microsoft-Windows-Kernel-General"),
            new LogEvent(t0.AddMinutes(3), 12, "Microsoft-Windows-Kernel-General"),
            new LogEvent(t0.AddMinutes(6), 12, "Microsoft-Windows-Kernel-General"),
            new LogEvent(t0.AddMinutes(6).AddSeconds(10), 41, "Microsoft-Windows-Kernel-Power", 0x9F),
        };

        var report = StabilityAnalyzer.Analyze(events, t0.AddMinutes(15));
        Assert.Equal(3, report.Sessions.Count);
        Assert.Equal(SessionOutcome.Clean, report.Sessions[0].Outcome);
        Assert.Equal(SessionOutcome.Unexpected, report.Sessions[1].Outcome);
        Assert.Equal(SessionOutcome.InProgress, report.Sessions[2].Outcome);
    }

    [Fact]
    public void UpdateManifestValidator_HandlesRevisionAndRejectsPreReleaseOrBareUrl()
    {
        Assert.True(UpdateManifestValidator.IsNewerVersion("1.0.0.1", "1.0.0.2"));
        Assert.False(UpdateManifestValidator.IsNewerVersion("1.0.0", "1.0.1-alpha"));

        var validSha = new string('a', 64);
        Assert.False(UpdateManifestValidator.IsValidManifest(new UpdateManifest("1.0.1", "https://uptime-pc.vercel.app/", validSha)));
        Assert.False(UpdateManifestValidator.IsValidManifest(new UpdateManifest("1.0.1", "https://uptime-pc.vercel.app/bad url.exe", validSha)));
    }

    [Fact]
    public void Health_And_BootSession_ClampNegativeDurations()
    {
        var negative = TimeSpan.FromHours(-5);
        Assert.Equal(7, Health.DaysUntilMaintenance(negative));

        var start = new DateTime(2026, 11, 14, 12, 0, 0);
        var session = new BootSession(start, null, SessionOutcome.InProgress);
        Assert.Equal(TimeSpan.Zero, session.DurationAt(start.AddMinutes(-10)));
    }

    [Fact]
    public void NewsArticle_DefaultsAndProperties_AreProperlyInitialized()
    {
        var article = new NewsArticle
        {
            Id = "test-id",
            Title = "Nuova release",
            Content = "Contenuto news",
            Category = "UPDATE",
            IsPinned = true,
            AuthorId = "1233950558184800297",
            AuthorName = "IL DENTE PROIBITO",
        };

        Assert.Equal("test-id", article.Id);
        Assert.Equal("Nuova release", article.Title);
        Assert.Equal("Contenuto news", article.Content);
        Assert.Equal("UPDATE", article.Category);
        Assert.True(article.IsPinned);
        Assert.NotNull(article.Reactions);
        Assert.NotNull(article.MyReactions);
    }

    [Fact]
    public void NewsComment_PropertiesAndBadges_AreHandledCorrectly()
    {
        var comment = new NewsComment
        {
            Id = "c-1",
            NewsId = "test-id",
            DiscordId = "123",
            Username = "TestUser",
            DisplayName = "Tester",
            Badges = new List<string> { "Beta Tester" },
            Content = "Ottimo aggiornamento!",
            CanDelete = true,
        };

        Assert.Equal("c-1", comment.Id);
        Assert.Equal("test-id", comment.NewsId);
        Assert.Single(comment.Badges);
        Assert.Equal("Beta Tester", comment.Badges[0]);
        Assert.True(comment.CanDelete);
    }
}
