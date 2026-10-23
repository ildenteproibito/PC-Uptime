# Changelog

All notable changes to PC Uptime are documented here. Versions follow SemVer.

## [5.1.0] - 2026-10-22

### Added
- Mini HUD overlay window for compact uptime display.
- Night prompt window for end-of-day review.
- Badge generator for shareable uptime/stability badges.
- Dashboard configuration with persisted layout.
- Multi-language localization engine and language profiles.

### Changed
- Reworked crash report: unexpected vs. clean shutdowns, stability score, confidence level.
- Improved session records with active/idle seconds and termination type.

### Fixed
- Uptime monotonicity across sleep/resume cycles.

## [4.2.1] - 2026

### Fixed
- Minor stability score calculation edge cases.
- Localization string fallbacks.

## [4.2.0] - 2026

### Added
- System metrics tracker.
- Export helper (CSV/JSON) for sessions and diagnostics.
- Diagnostic incident tracking.

## [1.0.1] - earlier

### Added
- Initial public release: uptime tracking, boot time, basic display formatting.
- MSTest suite with WPF initialization smoke test.
