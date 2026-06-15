# Scrybe

> **scry** (read a remote screen) + **scribe** (write by typing)

Scrybe is a standalone, local-first Windows utility for developers, SysAdmins
and DevOps working in constrained remote consoles where the clipboard is broken
or unavailable: RDP, vSphere/ESXi, IPMI/iDRAC/iLO, VNC/noVNC, and locked-down
SSH or terminal sessions.

## Problem

In these consoles, copy/paste often fails in both directions. Scrybe restores
that bridge without cloud services:

- **Extraction** (screen to local): capture a region from the monitor under the
  cursor, run OCR, and
  copy cleaned, code-aware text to the local clipboard.
- **Injection** (local to remote): type text as synthetic keystrokes into the
  target console, including long commands and DPAPI-protected stored secrets,
  when paste is blocked.

## Differentiation

Capture2Text and Textify extract text, but they do not type text back into a
remote console. Scrybe covers both directions.

## Current Scope

- **License:** Apache 2.0.
- **Local-first:** offline, no telemetry, no cloud OCR.
- **Windows-native:** WPF, tray app, Per-Monitor DPI Aware v2.
- **Capture scope today:** monitor under cursor, with Per-Monitor DPI Aware v2
  coordinate handling.
- **Theme:** Dracula.

## Prerequisites

- Windows 10 19041 or newer.
- .NET 10 SDK. The repository pins exact SDK `10.0.103` in `global.json`.
- The bundled `tessdata/eng.traineddata` file is required for OCR.
- Published builds ship the Tesseract native DLLs in a loose `x64/` directory
  beside `Scrybe.exe`; keep that folder next to the executable.

## Build / Run

From a fresh clone:

```powershell
dotnet restore Scrybe.slnx
dotnet build Scrybe.slnx --configuration Debug
dotnet test Scrybe.slnx --configuration Release
dotnet run --project src\Scrybe.App\Scrybe.App.csproj
```

Convenience scripts are provided for double-click or terminal use:

- `Run.bat` launches the app from source in Debug.
- `Build.bat` builds Debug.
- `Test.bat` runs the test suite.
- `Release.bat` runs the local release pipeline.
- `Build.ps1 -Mode Release -DryRun` verifies formatting, runs tests in Release,
  builds, publishes locally, and prints the GitHub release commands it would run.
- `Build.ps1 -Mode Release -Publish` creates the release commit/tag and GitHub
  release; it requires a clean `main` branch and authenticated `gh`.

The formatting gate is:

```powershell
dotnet format Scrybe.slnx --verify-no-changes
```

## Status

**v1.0 - Dogfooding.** OCR extraction, keystroke injection, a DPAPI secret vault,
settings, hotkeys, packaging and the Control Hub are in place. Remaining work is
limited to issues found through real use.

## Structure

```text
Scrybe/
|-- src/       # WPF app, Core library, OCR engine
|-- tests/     # xUnit suite
|-- locales/   # localized EN/FR strings
|-- tessdata/  # bundled OCR model
|-- docs/      # architecture overview and ADRs
|-- CHANGELOG.md
`-- README.md
```

For a module map and runtime-flow overview, see
[`docs/architecture.md`](docs/architecture.md). Release history is summarized in
[`CHANGELOG.md`](CHANGELOG.md).
