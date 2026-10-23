# PC Uptime

A lightweight Windows desktop application for operating-system uptime, stability
diagnostics, and customizable analytics. Built with WPF on .NET 8.

<p align="center">
  <img src="Store_Assets/Halloween_Clean_256x256.png" alt="PC Uptime logo" width="128" />
</p>

## Features

- **Uptime tracking** — accurate boot time and monotonic uptime via `GetTickCount64`,
  with a formatted primary/secondary display (days/hours/minutes/seconds).
- **Stability diagnostics** — crash report with unexpected vs. clean shutdowns over the
  last 30 days, stability score, confidence level and incident list.
- **Session history** — per-session records (boot time, duration, active/idle seconds,
  termination type) with active-vs-idle percentages.
- **System metrics tracker** — background tracking of machine metrics.
- **Dashboard** — customizable `DashboardConfig` with persisted layout.
- **Mini HUD** — compact always-on-top overlay window.
- **Night prompt** — scheduled prompt window for end-of-day review.
- **Export** — CSV/JSON export of sessions and diagnostics (`ExportHelper`).
- **Badge generator** — shareable uptime/stability badges.
- **Localization** — multi-language UI through `LocalizationEngine` + `LanguageProfile`.
- **Halloween theme** — seasonal background asset support.

## Tech stack

- .NET 8, WPF + Windows Forms interop
- [`WPF-UI`](https://github.com/lepoco/wpfui) for modern controls
- `System.Drawing.Common`
- MSTest for unit/integration tests

## Project layout

```
PC_Uptime_Source/
├── AwayUptime/          # Main WPF application
│   ├── App.xaml(.cs)    # Application entry point
│   ├── MainWindow.xaml  # Primary UI
│   ├── MiniHudWindow.*  # Compact overlay
│   ├── NightPromptWindow.*
│   ├── *Helper.cs       # Domain helpers (uptime, export, badge, localization)
│   └── *.cs             # Models: SessionRecord, UptimeStats, CrashReport, ...
├── tests/Uptime.Tests/  # MSTest suite
├── Properties/          # Assembly info
└── Uptime.csproj        # Project file
```

## Build

Requires the .NET 8 SDK.

```powershell
dotnet build Uptime.csproj
dotnet run --project Uptime.csproj
```

## Tests

```powershell
dotnet test tests/Uptime.Tests/Uptime.Tests.csproj
```

The suite includes a WPF initialization smoke test (STA thread) and calculation tests
for uptime monotonicity, boot-time reconstruction, display formatting and active/idle
percentages.

## Data & privacy

All data is stored locally on the user's machine. No telemetry is sent. Crash reports
and session records are derived from local Windows event logs and in-app tracking.

## License

All rights reserved. The source code is published for transparency and review; it is not licensed for redistribution or commercial use.

## Author

Maintained by the project owner. Contributions, issues and feature requests are welcome
via GitHub Issues.
