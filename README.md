@@ -1,188 +1,94 @@
<p align="center">
  <img src="Store_Assets/Halloween_Clean_256x256.png" alt="PC Uptime logo" width="128" />
</p>
# PC Uptime

<h1 align="center">PC Uptime</h1>
See how long your PC has been running, review recent sessions, and understand unexpected shutdowns from one quiet Windows app.

<p align="center">
  <strong>Track your PC's uptime, analyze stability, and review detailed session analytics.</strong><br/>
  A lightweight Windows desktop application built with WPF on .NET 8.
</p>
**PC Uptime** is an independent desktop project for Windows. It keeps monitoring data on your computer and presents uptime, stability, and system activity in a clear dashboard.

<p align="center">
  <a href="https://apps.microsoft.com/detail/9p7h0nn62hk0?hl=en-EN&gl=EN">Microsoft Store</a> ·
  <a href="#features">Features</a> ·
  <a href="#architecture">Architecture</a> ·
  <a href="#build--run">Build</a> ·
  <a href="#roadmap">Roadmap</a> ·
  <a href="#website">Website</a>
  <a href="https://apps.microsoft.com/detail/9p7h0nn62hk0?hl=en-EN&amp;gl=EN"><strong>Get it from Microsoft Store</strong></a>
  &nbsp;·&nbsp; <a href="https://uptime-pc.vercel.app/">Website</a>
  &nbsp;·&nbsp; <a href="https://uptime-pc.vercel.app/support/">Help and support</a>
  &nbsp;·&nbsp; <a href="https://discord.gg/88qrhwjBDP">Discord</a>
</p>

---

## Overview
## Screenshots

PC Uptime is a Windows desktop utility that monitors how long your machine has been
running, evaluates system stability from local event logs and in-app tracking, and
presents the results through a customizable dashboard and a compact always-on-top HUD.
All data stays on the user's machine — no telemetry is collected or transmitted.
The screenshots below show the app’s Monitor, Stability, and Operations views.

The application is distributed through the **Microsoft Store** and is currently at
version **5.1.0**.
<table>
  <tr>
    <td align="center" width="33%"><a href="docs/screenshots/monitor.png"><img src="docs/screenshots/monitor.png" alt="PC Uptime Monitor view showing the current session and system details" width="100%"></a><br><sub>Monitor</sub></td>
    <td align="center" width="33%"><a href="docs/screenshots/stability.png"><img src="docs/screenshots/stability.png" alt="PC Uptime Stability view showing the stability score and boot history" width="100%"></a><br><sub>Stability</sub></td>
    <td align="center" width="33%"><a href="docs/screenshots/operations.png"><img src="docs/screenshots/operations.png" alt="PC Uptime Operations view showing power actions and preferences" width="100%"></a><br><sub>Operations</sub></td>
  </tr>
</table>

## Website
## What it does

The official website is available at [Uptime](https://uptime-pc.vercel.app/) and provides an
overview of the application, feature highlights, architecture notes, download
links and legal information.
- **Tracks uptime** from the current Windows boot and shows when the session began.
- **Keeps session history** with durations and active or idle time.
- **Reviews stability** using Windows startup and shutdown events, with incident details for unexpected shutdowns.
- **Shows system information** alongside the current session and a restart recommendation.
- **Puts uptime within reach** through the notification-area icon, a global shortcut (`Win` + `Alt` + `U`), and the compact Mini-HUD.
- **Provides power tools** for a clean restart, full shutdown, restart to UEFI/BIOS, and timed shutdown.
- **Exports records** as JSON or CSV and creates shareable uptime badges.
- **Offers preferences** for startup behavior, Fast Startup, dashboard layout, language, and the optional night reminder.

## Features

- **Accurate uptime tracking** — boot time and uptime duration computed from
  `GetTickCount64`, with a primary/secondary formatted display
  (days, hours, minutes, seconds).
- **Stability diagnostics** — crash report comparing unexpected vs. clean
  shutdowns over the last 30 days, a stability score, a confidence level, and a
  list of diagnostic incidents.
- **Session history** — per-session records (boot time, duration, active/idle
  seconds, termination type) with active-vs-idle percentages.
- **System metrics tracker** — background tracking of machine resources.
- **Customizable dashboard** — persisted layout via `DashboardConfig`.
- **Compact HUD** — always-on-top overlay window for at-a-glance uptime.
- **Night prompt** — scheduled end-of-day review window.
- **Data export** — sessions and diagnostics exported to CSV/JSON.
- **Shareable badges** — uptime/stability badge generator.
- **Localization** — multi-language UI via `LocalizationEngine` and `LanguageProfile`.
- **Seasonal themes** — themed backgrounds (e.g. Halloween).

## Architecture

```
PC_Uptime_Source/
├── Uptime/                      # Main WPF application
│   ├── App.xaml(.cs)            # Application entry point, lifecycle
│   ├── MainWindow.xaml(.cs)     # Primary UI + dashboard
│   ├── MiniHudWindow.*          # Compact overlay
│   ├── NightPromptWindow.*      # Scheduled prompt
│   ├── UptimeHelper.cs          # Uptime / boot-time computation
│   ├── SystemMetricsTracker.cs  # Background metrics collection
│   ├── ExportHelper.cs          # CSV/JSON export
│   ├── BadgeGenerator.cs        # Badge rendering
│   ├── LocalizationEngine.cs    # i18n engine
│   ├── LanguageProfile.cs       # Per-language formatting rules
│   ├── CrashReport.cs           # Stability report model
│   ├── SessionRecord.cs         # Session model
│   ├── UptimeStats.cs           # Aggregated stats model
│   ├── DashboardConfig.cs       # Persisted dashboard layout
│   └── DiagnosticIncident.cs    # Incident model
├── tests/Uptime.Tests/          # MSTest suite
├── Store_Assets/                # Store listing images
├── Properties/                  # Assembly info
└── Uptime.csproj               # Project file
```
## Get started

Key design points:
Install PC Uptime from the [Microsoft Store](https://apps.microsoft.com/detail/9p7h0nn62hk0), then open it from the Start menu. The Monitor, Stability, and Operations tabs provide the main dashboard, history, and system controls. Use the tray icon or `Win` + `Alt` + `U` to bring the app back when it is minimized.

- **Models are plain POCOs** (`SessionRecord`, `UptimeStats`, `CrashReport`) with
  no UI dependency, which keeps them testable.
- **Helpers are static/stateless** where possible (`UptimeHelper`, `ExportHelper`),
  so logic can be exercised without instantiating windows.
- **Localization is data-driven**: `LanguageProfile` carries formatting rules and
  `LocalizationEngine` resolves strings, so adding a language does not require
  code changes beyond a new profile.
- **WPF + WinForms interop** is enabled (`UseWindowsForms`) for system-tray and
  notification scenarios.
For usage help and troubleshooting, visit the [support page](https://uptime-pc.vercel.app/support/). You can also ask questions in the [Uptime Discord server](https://discord.gg/88qrhwjBDP).

## Tech stack
## System requirements

| Component | Technology |
|---|---|
| Framework | .NET 8, WPF |
| UI library | [WPF-UI](https://github.com/lepoco/wpfui) |
| Graphics | System.Drawing.Common |
| Testing | MSTest |
| Packaging | MSIX (Microsoft Store) |
- Windows 10 (version 2004 / build 19041) or later
- 64-bit (x64) Windows
- Internet access is needed to download or install the app; monitoring runs locally

## Build & run
## Build from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
The checked-in desktop app uses C#, WPF, and .NET 8. To build it, install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) and run these commands from the repository root in PowerShell:

```powershell
# Build
dotnet build Uptime.csproj

# Run
dotnet run --project Uptime.csproj
dotnet restore .\Uptime.csproj
dotnet build .\Uptime.csproj --configuration Release
```

For a Store-ready MSIX package, use the Windows SDK tooling (`makeappx`, `signtool`)
located under `build_tools/`, or publish via Visual Studio's packaging workflow.

## Tests
To run the test suite:

```powershell
dotnet test tests/Uptime.Tests/Uptime.Tests.csproj
dotnet test .\tests\Uptime.Tests\Uptime.Tests.csproj --configuration Release
```

The suite covers:

- Uptime monotonicity and positivity.
- Boot-time reconstruction from uptime + current time.
- Display formatting across day/hour/minute intervals.
- Active-vs-idle percentage calculation.
- A WPF initialization smoke test (STA thread) that instantiates `App` and
  `MainWindow`.

## Data & privacy
## How it is organized

- All session records, crash reports and dashboard configuration are stored
  **locally** on the user's machine.
- **No telemetry** is collected or transmitted.
- Stability data is derived from local Windows event logs and in-app tracking.
| Path | Contents |
| --- | --- |
| `Uptime/` | WPF application, dashboard, Mini-HUD, tray integration, and views |
| `UptimeHelper.cs` | Uptime and boot-time helpers |
| `SystemMetricsTracker.cs` | Session activity, stability information, and system metrics |
| `ExportHelper.cs` | JSON and CSV export |
| `LocalizationEngine.cs` / `LanguageProfile.cs` | Interface text and localized formatting |
| `tests/Uptime.Tests/` | MSTest test suite |
| `docs/screenshots/` | Screenshots used in this README |

## Roadmap
## Privacy

### Quality & engineering
- [ ] Pin NuGet dependency versions (currently `WPF-UI` uses `*`).
- [ ] Split `LocalizationEngine.cs` into per-language resource files.
- [ ] Add CI pipeline (build + tests) on every push.
- [ ] Add code coverage reporting and badge.
- [ ] Introduce structured logging for diagnostics.
PC Uptime reads Windows system information and event history to show uptime and stability. Monitoring records and event details are processed locally; the app does not send telemetry. You control any exported records and should review them before choosing to share them.

### UX & features
- [ ] Dark/light theme toggle beyond seasonal themes.
- [ ] System-tray mode with quick uptime tooltip.
- [ ] Configurable alerts (e.g. notify after N hours of continuous uptime).
- [ ] Per-process uptime breakdown (which apps kept the PC awake).
- [ ] Export to more formats (PDF report, image snapshot).
- [ ] Historical charts for uptime/stability trends over weeks and months.
- [ ] Multi-monitor aware HUD placement and snapping.
Read the [Privacy Policy and Terms of Service](https://uptime-pc.vercel.app/privacy/) for details about the app and website.

### Platform
- [ ] Auto-update mechanism integrated with the Store listing.
- [ ] Windows 11 widgets integration.
- [ ] Optional portable (non-Store) build.
## Project status

## FAQ

**Is this vibe-coded / AI-generated?**
The project was developed with AI assistance (Antigravity) as a coding partner,
but every feature, test and release decision was reviewed and shipped manually.
The commit history, test suite and Store release process reflect that.

**Can I redistribute or sell this code?**
No. The source is published for transparency and review only; all rights are
reserved by the author.

**Where is my data stored?**
Locally, in the application's data folder. Nothing leaves the machine.
PC Uptime is an independent project maintained in the open. The repository reflects the available source; Microsoft Store is the recommended way to install the published app. Follow the [website](https://uptime-pc.vercel.app/) or [Discord](https://discord.gg/88qrhwjBDP) for project updates. To report a problem, open a [GitHub issue](https://github.com/ildenteproibito/PC-Uptime/issues).

## License

All rights reserved. The source code is published for transparency and review; it
is not licensed for redistribution or commercial use.

## Author
All rights reserved. The source is published for transparency and review; it is not licensed for redistribution or commercial use.

Maintained by [@ildenteproibito](https://github.com/ildenteproibito).
<<<<<<< HEAD
Issues and feature requests are welcome via GitHub Issues.
=======
>>>>>>> 7ec3a3a (docs: refresh README with app screenshots)
