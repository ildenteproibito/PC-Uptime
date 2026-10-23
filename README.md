<p align="center">
  <img src="Store_Assets/Halloween_Clean_256x256.png" alt="PC Uptime logo" width="128" />
</p>

<h1 align="center">PC Uptime</h1>

<p align="center">
  <strong>Track your PC's uptime, analyze stability, and review detailed session analytics.</strong><br/>
  A lightweight Windows desktop application built with WPF on .NET 8.
</p>

<p align="center">
  <a href="https://apps.microsoft.com/detail/9p7h0nn62hk0">Microsoft Store</a> ·
  <a href="#features">Features</a> ·
  <a href="#architecture">Architecture</a> ·
  <a href="#build--run">Build</a> ·
  <a href="#roadmap">Roadmap</a> ·
  <a href="#website">Website</a>
</p>

---

## Overview

PC Uptime is a Windows desktop utility that monitors how long your machine has been
running, evaluates system stability from local event logs and in-app tracking, and
presents the results through a customizable dashboard and a compact always-on-top HUD.
All data stays on the user's machine — no telemetry is collected or transmitted.

The application is distributed through the **Microsoft Store** and is currently at
version **5.1.0**.

## Website

The official website is available at [Website](https://uptime-pc.vercel.app/) and provides an
overview of the application, feature highlights, architecture notes, download
links and legal information.

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

Key design points:

- **Models are plain POCOs** (`SessionRecord`, `UptimeStats`, `CrashReport`) with
  no UI dependency, which keeps them testable.
- **Helpers are static/stateless** where possible (`UptimeHelper`, `ExportHelper`),
  so logic can be exercised without instantiating windows.
- **Localization is data-driven**: `LanguageProfile` carries formatting rules and
  `LocalizationEngine` resolves strings, so adding a language does not require
  code changes beyond a new profile.
- **WPF + WinForms interop** is enabled (`UseWindowsForms`) for system-tray and
  notification scenarios.

## Tech stack

| Component | Technology |
|---|---|
| Framework | .NET 8, WPF |
| UI library | [WPF-UI](https://github.com/lepoco/wpfui) |
| Graphics | System.Drawing.Common |
| Testing | MSTest |
| Packaging | MSIX (Microsoft Store) |

## Build & run

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
# Build
dotnet build Uptime.csproj

# Run
dotnet run --project Uptime.csproj
```

For a Store-ready MSIX package, use the Windows SDK tooling (`makeappx`, `signtool`)
located under `build_tools/`, or publish via Visual Studio's packaging workflow.

## Tests

```powershell
dotnet test tests/Uptime.Tests/Uptime.Tests.csproj
```

The suite covers:

- Uptime monotonicity and positivity.
- Boot-time reconstruction from uptime + current time.
- Display formatting across day/hour/minute intervals.
- Active-vs-idle percentage calculation.
- A WPF initialization smoke test (STA thread) that instantiates `App` and
  `MainWindow`.

## Data & privacy

- All session records, crash reports and dashboard configuration are stored
  **locally** on the user's machine.
- **No telemetry** is collected or transmitted.
- Stability data is derived from local Windows event logs and in-app tracking.

## Roadmap

### Quality & engineering
- [ ] Pin NuGet dependency versions (currently `WPF-UI` uses `*`).
- [ ] Split `LocalizationEngine.cs` into per-language resource files.
- [ ] Add CI pipeline (build + tests) on every push.
- [ ] Add code coverage reporting and badge.
- [ ] Introduce structured logging for diagnostics.

### UX & features
- [ ] Dark/light theme toggle beyond seasonal themes.
- [ ] System-tray mode with quick uptime tooltip.
- [ ] Configurable alerts (e.g. notify after N hours of continuous uptime).
- [ ] Per-process uptime breakdown (which apps kept the PC awake).
- [ ] Export to more formats (PDF report, image snapshot).
- [ ] Historical charts for uptime/stability trends over weeks and months.
- [ ] Multi-monitor aware HUD placement and snapping.

### Platform
- [ ] Auto-update mechanism integrated with the Store listing.
- [ ] Windows 11 widgets integration.
- [ ] Optional portable (non-Store) build.

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

## License

All rights reserved. The source code is published for transparency and review; it
is not licensed for redistribution or commercial use.

## Author

Maintained by [@ildenteproibito](https://github.com/ildenteproibito).
Issues and feature requests are welcome via GitHub Issues.