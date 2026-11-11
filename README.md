# PC Uptime

See how long your PC has been running, review recent sessions, and understand unexpected shutdowns from one focused Windows desktop application.

**PC Uptime** is an independent Windows utility for monitoring system uptime, reviewing session history, assessing system stability, and accessing essential power-management operations. Monitoring data is processed locally and presented through a native-feeling desktop interface.

<p align="center">
  <img src="docs/Uptime-Logo.png" alt="PC Uptime logo" width="360" />
</p>

<p align="center">
  <a href="https://apps.microsoft.com/detail/9p7h0nn62hk0?hl=en-GB&gl=EN">
    <img src="https://get.microsoft.com/images/en-us%20dark.svg" alt="Get it from Microsoft Store" width="220" />
  </a>
  &nbsp; <a href="https://uptime-pc.vercel.app/">
    <img src="https://img.shields.io/badge/Website-PC%20Uptime-3B82F6?style=flat&labelColor=20242C" alt="PC Uptime website" />
  </a>
  &nbsp; <a href="https://uptime-pc.vercel.app/support/">
    <img src="https://img.shields.io/badge/Support-Help%20Center-64748B?style=flat&labelColor=20242C" alt="PC Uptime help and support" />
  </a>
  &nbsp; <a href="https://discord.gg/88qrhwjBDP">
    <img src="https://img.shields.io/badge/Discord-Join%20community-5865F2?style=flat&logo=discord&logoColor=white&labelColor=20242C" alt="Join our Discord server" />
  </a>
</p>

## Watch the trailer

Get a closer look at PC Uptime in the official project trailer.

<p align="center">
  <video src="https://github.com/user-attachments/assets/31f1d3b9-b442-4472-9b60-07a57158a41d" controls playsinline preload="metadata" width="100%">
    Your browser does not support embedded video. <a href="https://github.com/user-attachments/assets/31f1d3b9-b442-4472-9b60-07a57158a41d">Watch or download the PC Uptime trailer</a>.
  </video>
</p>

<p align="center">
  <a href="https://cdn.rapidfile.cc/lRMgfjMaXV/trailer.mp4">Open trailer directly</a>
</p>

## Screenshots

The screenshots below show the Monitor, Stability, and Operations views.

<table>
  <tr>
    <td align="center" width="33%"><a href="docs/screenshots/monitor.png"><img src="docs/screenshots/monitor.png" alt="PC Uptime Monitor view showing the current session and system details" width="100%"></a><br><sub>Monitor</sub></td>
    <td align="center" width="33%"><a href="docs/screenshots/stability.png"><img src="docs/screenshots/stability.png" alt="PC Uptime Stability view showing the stability score and boot history" width="100%"></a><br><sub>Stability</sub></td>
    <td align="center" width="33%"><a href="docs/screenshots/operations.png"><img src="docs/screenshots/operations.png" alt="PC Uptime Operations view showing power actions and preferences" width="100%"></a><br><sub>Operations</sub></td>
  </tr>
</table>

## Features

- **Uptime monitoring:** tracks the current Windows boot session and reports its start time and elapsed duration.
- **Session history:** records session durations and active or idle time.
- **Stability analysis:** evaluates Windows startup and shutdown events and surfaces details about unexpected shutdowns.
- **System context:** presents relevant system information alongside uptime and maintenance guidance.
- **Desktop integration:** provides notification-area access, a global keyboard shortcut (`Win` + `Alt` + `U`), and a compact Mini-HUD.
- **Power operations:** supports clean restart, full shutdown, restart to UEFI/BIOS, and scheduled shutdown.
- **Data export:** exports records in JSON or CSV format and generates shareable uptime badges.
- **Configurable interface:** includes preferences for startup behavior, Fast Startup, dashboard layout, language, and the optional night reminder.

## Platform compatibility

PC Uptime is currently available for Windows. Support for Linux and macOS is planned.

<table align="center">
  <tr>
    <td align="center" width="33%">
      <img src="https://img.shields.io/badge/Windows%2010%20%2F%2011-0078D4?style=for-the-badge&logo=windows&logoColor=white" alt="Windows 10 and 11 logo" />
      <br /><sub><strong>Currently supported</strong></sub>
    </td>
    <td align="center" width="33%">
      <img src="https://img.shields.io/badge/Linux-111827?style=for-the-badge&logo=linux&logoColor=FCC624" alt="Linux logo" />
      <br /><sub><strong>Planned</strong></sub>
    </td>
    <td align="center" width="33%">
      <img src="https://img.shields.io/badge/macOS-111827?style=for-the-badge&logo=apple&logoColor=white" alt="macOS logo" />
      <br /><sub><strong>Planned</strong></sub>
    </td>
  </tr>
</table>

## Technology stack

PC Uptime is built with the following technologies:

| Component | Technology |
| --- | --- |
| Language | C# |
| Target framework | .NET 10 |
| Desktop UI | Avalonia UI 11.3 |
| UI theme | Avalonia Fluent Theme |
| MVVM infrastructure | CommunityToolkit.Mvvm |
| Windows integration | Win32 APIs and Windows Event Log |
| Automated tests | xUnit |

The presentation project targets `net10.0-windows`. The Core, Infrastructure, and test projects target `net10.0`.

## Architecture

The solution is organized into three application projects with separate responsibilities:

| Project | Responsibility |
| --- | --- |
| `Uptime.Core` | Domain models, uptime and stability logic, service abstractions, localization, and core persistence utilities. |
| `Uptime.Infrastructure` | Windows-specific implementations for uptime and idle-time providers, event log access, power operations, and startup configuration. |
| `Uptime.Presentation` | Avalonia desktop application, views, view models, UI controls, application diagnostics, tray-related integration, and global hotkey handling. |
| `Uptime.Core.Tests` | Automated tests for core application logic. |

The presentation layer consumes Core abstractions and Infrastructure implementations. This separation keeps domain logic distinct from Windows-specific operations and UI concerns.

## Get started

Install PC Uptime from the [Microsoft Store](https://apps.microsoft.com/detail/9p7h0nn62hk0?hl=en-GB&gl=EN), then launch it from the Start menu. The Monitor, Stability, and Operations sections provide the main dashboard, session and event history, and system controls.

Use the notification-area icon or `Win` + `Alt` + `U` to bring the application back when it is minimized.

For usage help and troubleshooting, visit the [support page](https://uptime-pc.vercel.app/support/) or ask questions in the [Uptime Discord server](https://discord.gg/88qrhwjBDP).

## System requirements

- Windows 10 (version 2004 / build 19041) or later
- 64-bit (x64) Windows
- Internet access is required to download or install the application; monitoring runs locally

## Build from source

### Prerequisites

- Windows x64
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Git

### Restore and build

Run the following commands from the repository root in PowerShell:

```powershell
dotnet restore .\Uptime.slnx
dotnet build .\Uptime.slnx --configuration Release
```

### Run tests

```powershell
dotnet test .\tests\Uptime.Core.Tests\Uptime.Core.Tests.csproj --configuration Release
```

### Publish a self-contained Windows x64 build

```powershell
dotnet publish .\src\Uptime.Presentation\Uptime.Presentation.csproj --configuration Release --runtime win-x64 --self-contained true
```

The publish output is generated under the presentation project's `bin\Release\net10.0-windows\win-x64\publish` directory.

## Repository layout

```text
.
├── docs/
│   ├── screenshots/
│   └── Uptime-Logo.png
├── src/
│   ├── Uptime.Core/
│   ├── Uptime.Infrastructure/
│   └── Uptime.Presentation/
├── tests/
│   └── Uptime.Core.Tests/
└── Uptime.slnx
```

## Privacy

PC Uptime reads Windows system information and event history to show uptime and stability. Monitoring records and event details are processed locally; the app does not send telemetry. You control any exported records and should review them before choosing to share them.

Read the [Privacy Policy and Terms of Service](https://uptime-pc.vercel.app/privacy/) for details about the app and website.

## Project status

PC Uptime is an independent project maintained in the open. This repository contains the application source code; the Microsoft Store is the recommended channel for installing the published application. Follow the [website](https://uptime-pc.vercel.app/) or [Discord](https://discord.gg/88qrhwjBDP) for project updates. To report a problem, open a [GitHub issue](https://github.com/ildenteproibito/PC-Uptime/issues).

## License

All rights reserved. The source is published for transparency and review; it is not licensed for redistribution or commercial use.

Maintained by [@ildenteproibito](https://github.com/ildenteproibito).
