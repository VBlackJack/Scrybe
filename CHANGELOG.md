# Changelog

All notable changes to Scrybe are summarized here from the conventional commit
history. Dates use the project CalVer tags.

## Unreleased

Changes after `v2026.060901` in the local hardening branch.

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
