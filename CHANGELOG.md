# Changelog

[Français](CHANGELOG.fr.md)

## v2026.090601

Released on 2026-09-06. Changes since `v2026.061601`.

### Features

- Add optional OCR correction beside the original crop, preserving previous outputs on cancellation.
- Add confirmed process profiles for injection mode and pacing.
- Retain 20 local store versions with preview, revision checks and DPAPI validation before restoration.
- Add strict versioned snippet import/export with template preview and explicit conflict policies.
- Provide separate English and French README, documentation and release notes.

### Reliability and accessibility

- Recheck foreground identity and keyboard layout throughout injection; release Shift before special keys.
- Preserve newer clipboard content and OCR boundary whitespace.
- Reject failed-read and stale-write saves, quarantine invalid records and serialize library mutations.
- Preserve editor drafts during reload and publish library changes only after persistence succeeds.
- Add injection progress, visible cancellation and precise failure statuses.
- Add keyboard region selection, accessible coordinate announcements and bounded high-DPI layouts.

### Validation and delivery

- Add OCR corpus measurements, native/remote receivers and 22 EN/FR interface renders.
- Add packaged OCR self-tests and Windows Sandbox configuration generation.
- Unify build/test project configuration and refresh versioned project-reference locks.
- Produce separate EN/FR release-note assets with checksums; verify the committed version in CI.


## v2026.061601

Previously documented hardening delivered since `v2026.060901`.

### Security And Correctness

- Disabled debug injection hotkeys by default.
- Made clipboard injection propagate `InjectionResult`, so failures no longer
  clear the clipboard or report success.
- Completed the capture overlay wait on window close, preventing capture
  deadlock after non-standard closes.
- Added DPAPI app-specific optional entropy with a versioned protected-value
  format and legacy migration.
- Made the password-grade target confirmation gate unit-testable while keeping
  the native confirmation dialog in production.
- Verified snippet foreground restore before typing.
- Surfaced JSON store save failures to users instead of silently diverging from
  disk.

### Persistence

- Added atomic JSON writes for settings, snippets, secrets and capture history
  through same-directory temp files, write-through flush and atomic rename.

### Tooling And Reproducibility

- Pinned the .NET SDK through `global.json`.
- Added NuGet lock files and pinned language/analyzer levels.
- Added the `dotnet format --verify-no-changes` gate.
- Made Release test runs actually test the requested configuration.
- Added CodeQL and Dependency Review workflows for security scanning on GitHub.
- Made CodeQL opt-in on private repositories so code scanning settings do not
  block private releases.
- Added Authenticode release-signing support, SPDX SBOM generation and SHA256
  checksum files to the release pipeline.

### UI And Documentation

- Documented clone-to-run prerequisites and current primary-monitor capture
  scope.
- Exposed clipboard-injection and history-palette hotkeys in Settings.
- Added accessibility labels and list virtualization to manager views.
- Flattened shell tabs to avoid per-tab outline seams.

## v2026.060901

### UI

- Refined the Control Hub dashboard and manager forms.
- Added Fluent-style icons to shell tabs and primary actions.
- Compactly arranged the home action bar and supporting status surfaces.

## v2026.060802

### Features

- Added encrypted capture history with a palette.
- Added editing for capture-history entries.
- Added clipboard-to-keystrokes injection.

### UI

- Consolidated Settings, About and manager surfaces into the tabbed shell.
- Added themed window chrome, scrollbars and confirmation dialogs.

### Build

- Hardened the publish release flow.

## v2026.060801

### Features

- Added CalVer release versioning.
- Added the About window with build metadata.

## Initial

- Initial Scrybe codebase.
