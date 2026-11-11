# UPTIME — CINEMATIC SOFTWARE LAUNCH FILM

This directory contains the complete production package for the official cinematic launch film for **Uptime** (PC Uptime), created with precision cinematography, procedural sound design, and Remotion.

---

## 📁 PRODUCTION STRUCTURE

```text
trailer/
├── BRIEF.md                    # Creative brief, messaging, and project objectives
├── CONCEPTS.md                 # 3 distinct creative concepts with evaluation & decision
├── TREATMENT.md                # Full shot-by-shot storyboard, camera moves, and audio map
├── QA_REPORT.md                # Quality assurance and technical compliance report
├── package.json                # Remotion 4.x dependencies and build scripts
├── remotion.config.ts          # Remotion headless rendering and GPU configuration
├── tsconfig.json               # TypeScript configuration
├── generate_soundtrack.py      # Procedural 48kHz stereo cinematic score generator
├── generate_qa_and_contact_sheet.py # Automated QA validation and frame contact sheet generator
├── public/                     # Static assets (screenshots, vector logos, soundtrack.wav)
│   ├── monitor.png
│   ├── stability.png
│   ├── operations.png
│   ├── uptime.png
│   ├── uptime.svg
│   └── soundtrack.wav
├── src/                        # Remotion composition source
│   ├── theme.ts                # Exact color tokens, fonts, and design constants
│   ├── index.ts                # Remotion entry point
│   ├── Root.tsx                # Composition registration
│   ├── Trailer.tsx             # Master timeline orchestration (2400 frames @ 60fps)
│   ├── Poster.tsx              # 1920x1080 promotional key visual poster
│   ├── components/
│   │   ├── Background.tsx      # Dark obsidian backdrop with radial lighting & micro-grid
│   │   ├── WindowFrame.tsx     # 3D perspective desktop application window
│   │   ├── MiniHud.tsx         # The compact floating always-on-top HUD widget
│   │   └── VectorLogo.tsx      # Precision SVG clock dial with dynamic arc sweep
│   └── scenes/
│       ├── Act1Opening.tsx     # Act I: The Opening (0.0s – 7.0s | frames 0 – 420)
│       ├── Act2Reveal.tsx      # Act II: The Reveal (7.0s – 15.0s | frames 420 – 900)
│       ├── Act3Experience.tsx  # Act III: The Experience (15.0s – 27.0s | frames 900 – 1620)
│       ├── Act4Impact.tsx      # Act IV: The Impact (27.0s – 35.0s | frames 1620 – 2100)
│       └── Act5FinalReveal.tsx # Act V: Final Reveal & Store CTA (35.0s – 40.0s | frames 2100 – 2400)
└── out/                        # Rendered master deliverables
    ├── uptime-trailer.mp4      # Master 1920x1080 @ 60 FPS cinematic video (H.264, BT.709)
    ├── soundtrack.mp3          # Master 320kbps stereo soundtrack
    ├── poster.png              # 1920x1080 key visual poster
    └── contact_sheet.jpg       # 12-milestone scene contact sheet
```

---

## 🎬 TECHNICAL HIGHLIGHTS

1. **Brand Authenticity**:
   - Matches the real Uptime Avalonia XAML design system: Obsidian `#0F1114`, Slate `#1B1E24`, Sage Emerald `#7BC4A8`, and Amber `#E0A458`.
   - Showcases real features: Live uptime counting, TimeRuler session telemetry, `Win` + `Alt` + `U` Mini-HUD summon, 30-day stability index (99.4%), Kernel-Power incident forensics, and UEFI reboot.
   - Zero generic AI aesthetics: no unnecessary neon or irrelevant stock graphics.

2. **Sound Design**:
   - 100% original electronic cinematic score generated at 48,000 Hz, 16-bit PCM stereo.
   - Sub-bass drone foundations (32.7 Hz / 65.4 Hz), precision clock micro-ticks, mechanical tactile clicks for hotkey triggers, dynamic risers, and a warm resolving chord swell.

3. **Autonomous Rendering**:
   - Multi-threaded headless Chromium rendering via Remotion at full 60.0 FPS.
   - Rec.709 color gamut, BT.709 matrix, YUV420p format.

---

## 🚀 COMMANDS

- **Preview in browser**: `npm run start`
- **Render video**: `npm run build`
- **Render poster**: `npm run still`
- **Regenerate soundtrack**: `python generate_soundtrack.py`
