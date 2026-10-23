using System;
using System.Linq;
using AwayUptime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Uptime.Tests;

[TestClass]
public class LocalizationTests
{
	[TestMethod]
	public void SupportedLanguages_HasAll10Languages()
	{
		var langs = LocalizationEngine.SupportedLanguages;
		Assert.AreEqual(12, langs.Count);

		var codes = langs.Select(l => l.Id).ToList();
		CollectionAssert.Contains(codes, "it");
		CollectionAssert.Contains(codes, "en");
		CollectionAssert.Contains(codes, "es");
		CollectionAssert.Contains(codes, "fr");
		CollectionAssert.Contains(codes, "de");
		CollectionAssert.Contains(codes, "pt");
		CollectionAssert.Contains(codes, "ru");
		CollectionAssert.Contains(codes, "ja");
		CollectionAssert.Contains(codes, "zh");
		CollectionAssert.Contains(codes, "ko");
		CollectionAssert.Contains(codes, "pl");
		CollectionAssert.Contains(codes, "tr");
	}

	[TestMethod]
	public void AllWidgets_HaveLocalizedTitlesInAllLanguages()
	{
		foreach (var lang in LocalizationEngine.SupportedLanguages)
		{
			foreach (var widgetId in DashboardConfig.AllWidgetIds)
			{
				string title = lang.GetWidgetTitle(widgetId);
				Assert.IsFalse(string.IsNullOrWhiteSpace(title), $"Widget {widgetId} should have localized title in {lang.Id}.");
				Assert.AreNotEqual(widgetId, title, $"Widget {widgetId} should not fall back to its ID in {lang.Id}.");
			}
		}
	}

	[TestMethod]
	public void Presets_HaveLocalizedTitlesInAllLanguages()
	{
		var presets = new[] { DashboardConfig.PresetDefault, DashboardConfig.PresetMinimal, DashboardConfig.PresetReliability, DashboardConfig.PresetCustom };
		foreach (var lang in LocalizationEngine.SupportedLanguages)
		{
			foreach (var p in presets)
			{
				string title = lang.GetPresetTitle(p);
				Assert.IsFalse(string.IsNullOrWhiteSpace(title), $"Preset {p} should have localized title in {lang.Id}.");
			}
		}
	}

	[TestMethod]
	public void TooltipsAndEditButton_AreLocalizedInAllLanguages()
	{
		foreach (var lang in LocalizationEngine.SupportedLanguages)
		{
			Assert.IsFalse(string.IsNullOrWhiteSpace(lang.BtnEditDashboard), $"BtnEditDashboard missing in {lang.Id}");
			Assert.IsFalse(string.IsNullOrWhiteSpace(lang.ToolTipCustomizeDashboard), $"ToolTipCustomizeDashboard missing in {lang.Id}");
			Assert.IsFalse(string.IsNullOrWhiteSpace(lang.ToolTipPinHud), $"ToolTipPinHud missing in {lang.Id}");
			Assert.IsFalse(string.IsNullOrWhiteSpace(lang.ToolTipUnpinHud), $"ToolTipUnpinHud missing in {lang.Id}");
			Assert.IsFalse(string.IsNullOrWhiteSpace(lang.ToolTipQuickShare), $"ToolTipQuickShare missing in {lang.Id}");
			Assert.IsFalse(string.IsNullOrWhiteSpace(lang.ToolTipClose), $"ToolTipClose missing in {lang.Id}");
			Assert.IsFalse(string.IsNullOrWhiteSpace(lang.ToolTipHudDrag), $"ToolTipHudDrag missing in {lang.Id}");
			Assert.IsFalse(string.IsNullOrWhiteSpace(lang.ToolTipHudOpen), $"ToolTipHudOpen missing in {lang.Id}");
			Assert.IsFalse(string.IsNullOrWhiteSpace(lang.ToolTipHudClose), $"ToolTipHudClose missing in {lang.Id}");
			Assert.IsFalse(string.IsNullOrWhiteSpace(lang.RefreshDiagnosticsHeader), $"RefreshDiagnosticsHeader missing in {lang.Id}");
			Assert.IsFalse(string.IsNullOrWhiteSpace(lang.ExportDataHeader), $"ExportDataHeader missing in {lang.Id}");
			Assert.IsFalse(string.IsNullOrWhiteSpace(lang.CopySummaryHeader), $"CopySummaryHeader missing in {lang.Id}");
		}
	}

	[TestMethod]
	public void PolishLanguage_HasCorrectSpecificTranslations()
	{
		var pl = LocalizationEngine.SupportedLanguages.First(l => l.Id == "pl");
		Assert.AreEqual("Edytuj", pl.BtnEditDashboard);
		Assert.AreEqual("Dostosuj pulpit", pl.ToolTipCustomizeDashboard);
		Assert.AreEqual("Przypnij mini-widżet do pulpitu (HUD)", pl.ToolTipPinHud);
		Assert.AreEqual("Mini-widżet (HUD) AKTYWNY - Kliknij, aby odpiąć", pl.ToolTipUnpinHud);
		Assert.AreEqual("Kopiuj kartę statystyk (Reddit/Discord)", pl.ToolTipQuickShare);
		Assert.AreEqual("Zamknij", pl.ToolTipClose);
		Assert.AreEqual("Kliknij dwukrotnie, aby otworzyć Uptime • Przeciągnij, aby przesunąć", pl.ToolTipHudDrag);
		Assert.AreEqual("Otwórz Uptime", pl.ToolTipHudOpen);
		Assert.AreEqual("Zamknij Mini-HUD", pl.ToolTipHudClose);
		Assert.AreEqual("Odśwież diagnostykę i niezawodność", pl.RefreshDiagnosticsHeader);
	}

	[TestMethod]
	public void TimelineProperties_AreLocalizedInAllLanguages()
	{
		foreach (var lang in LocalizationEngine.SupportedLanguages)
		{
			Assert.IsFalse(string.IsNullOrWhiteSpace(lang.TimelineActiveSessionBadge), $"TimelineActiveSessionBadge missing in {lang.Id}");
			Assert.IsFalse(string.IsNullOrWhiteSpace(lang.TimelineInProgressBadge), $"TimelineInProgressBadge missing in {lang.Id}");
			Assert.IsFalse(string.IsNullOrWhiteSpace(lang.TimelineDurationLabel), $"TimelineDurationLabel missing in {lang.Id}");
			Assert.IsFalse(string.IsNullOrWhiteSpace(lang.TimelineNormalShutdownBadge), $"TimelineNormalShutdownBadge missing in {lang.Id}");
			Assert.IsFalse(string.IsNullOrWhiteSpace(lang.TimelineRestartBadge), $"TimelineRestartBadge missing in {lang.Id}");
			Assert.IsFalse(string.IsNullOrWhiteSpace(lang.TimelineUnexpectedShutdownBadge), $"TimelineUnexpectedShutdownBadge missing in {lang.Id}");
			Assert.IsFalse(string.IsNullOrWhiteSpace(lang.TimelineCompletedBadge), $"TimelineCompletedBadge missing in {lang.Id}");
			Assert.IsFalse(string.IsNullOrWhiteSpace(lang.TimelineNoSessionsNotice), $"TimelineNoSessionsNotice missing in {lang.Id}");
			Assert.IsFalse(string.IsNullOrWhiteSpace(lang.StartedLabel), $"StartedLabel missing in {lang.Id}");
		}
	}

	[TestMethod]
	public void EnglishLanguage_HasCorrectTimelineTranslations()
	{
		var en = LocalizationEngine.SupportedLanguages.First(l => l.Id == "en");
		Assert.AreEqual("ACTIVE", en.TimelineActiveSessionBadge);
		Assert.AreNotEqual("ATTIVA", en.TimelineActiveSessionBadge);
		Assert.AreEqual("In Progress", en.TimelineInProgressBadge);
		Assert.AreNotEqual("In Corso", en.TimelineInProgressBadge);
		Assert.AreEqual("Duration", en.TimelineDurationLabel);
		Assert.AreNotEqual("Durata", en.TimelineDurationLabel);
		Assert.AreEqual("Clean", en.TimelineNormalShutdownBadge);
		Assert.AreNotEqual("Pulito", en.TimelineNormalShutdownBadge);
		Assert.AreEqual("Restart", en.TimelineRestartBadge);
		Assert.AreEqual("Crash", en.TimelineUnexpectedShutdownBadge);
		Assert.AreEqual("Ended", en.TimelineCompletedBadge);
		Assert.AreEqual("Started", en.StartedLabel);
	}

	[TestMethod]
	public void CardAndRebootBios_AreLocalizedInAllLanguages()
	{
		foreach (var lang in LocalizationEngine.SupportedLanguages)
		{
			Assert.IsFalse(string.IsNullOrWhiteSpace(lang.BtnCardLabel), $"BtnCardLabel missing in {lang.Id}");
			Assert.IsFalse(string.IsNullOrWhiteSpace(lang.BtnRebootBios), $"BtnRebootBios missing in {lang.Id}");
			Assert.IsFalse(string.IsNullOrWhiteSpace(lang.RebootBiosTitle), $"RebootBiosTitle missing in {lang.Id}");
			Assert.IsFalse(string.IsNullOrWhiteSpace(lang.RebootBiosDesc), $"RebootBiosDesc missing in {lang.Id}");
		}

		var en = LocalizationEngine.SupportedLanguages.First(l => l.Id == "en");
		Assert.AreEqual("Card", en.BtnCardLabel);
		Assert.AreEqual("Reboot BIOS", en.BtnRebootBios);

		var es = LocalizationEngine.SupportedLanguages.First(l => l.Id == "es");
		Assert.AreEqual("Tarjeta", es.BtnCardLabel);
		Assert.AreEqual("Reiniciar BIOS", es.BtnRebootBios);

		var fr = LocalizationEngine.SupportedLanguages.First(l => l.Id == "fr");
		Assert.AreEqual("Carte", fr.BtnCardLabel);
		Assert.AreEqual("Redémarrer BIOS", fr.BtnRebootBios);

		var de = LocalizationEngine.SupportedLanguages.First(l => l.Id == "de");
		Assert.AreEqual("Karte", de.BtnCardLabel);
		Assert.AreEqual("BIOS Neustart", de.BtnRebootBios);

		var ru = LocalizationEngine.SupportedLanguages.First(l => l.Id == "ru");
		Assert.AreEqual("Карточка", ru.BtnCardLabel);
		Assert.AreEqual("Перезагрузить BIOS", ru.BtnRebootBios);

		var ja = LocalizationEngine.SupportedLanguages.First(l => l.Id == "ja");
		Assert.AreEqual("カード", ja.BtnCardLabel);
		Assert.AreEqual("BIOS再起動", ja.BtnRebootBios);

		var zh = LocalizationEngine.SupportedLanguages.First(l => l.Id == "zh");
		Assert.AreEqual("卡片", zh.BtnCardLabel);
		Assert.AreEqual("重启 BIOS", zh.BtnRebootBios);

		var ko = LocalizationEngine.SupportedLanguages.First(l => l.Id == "ko");
		Assert.AreEqual("카드", ko.BtnCardLabel);
		Assert.AreEqual("BIOS 재부팅", ko.BtnRebootBios);

		var pl = LocalizationEngine.SupportedLanguages.First(l => l.Id == "pl");
		Assert.AreEqual("Karta", pl.BtnCardLabel);
		Assert.AreEqual("Uruchom BIOS", pl.BtnRebootBios);

		var tr = LocalizationEngine.SupportedLanguages.First(l => l.Id == "tr");
		Assert.AreEqual("Kart", tr.BtnCardLabel);
		Assert.AreEqual("BIOS'u Yeniden Başlat", tr.BtnRebootBios);
	}
}