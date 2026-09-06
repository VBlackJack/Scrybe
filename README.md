# Scrybe

[Français](README.fr.md)

> **scry** (read a remote screen) + **scribe** (write by typing)

Scrybe is a small Windows tray app for the awkward consoles where normal
copy/paste is unreliable or simply not available.

The idea is straightforward: capture text from the screen when you need to read
it, then type text back into the target when paste is blocked. Everything stays
local. There is no cloud OCR, no telemetry, and no account to create.

## Why It Exists

Remote and locked-down consoles often break the most basic workflow: getting a
command, password, log line or token from one side to the other. You can see the
text, but you cannot copy it. You have the text locally, but you cannot paste it.

Scrybe is meant to make those moments less painful:

- **Capture from screen:** select a region, run OCR locally, clean up the text,
  and copy it to the local clipboard.
- **Inject into target:** send local text as synthetic keystrokes, including
  long commands, snippets and locally protected secrets.
- **Stay offline:** keep settings, snippets, secrets and capture history on the
  machine, under the current Windows user profile.

## What It Does Today

- Runs as a Windows-native WPF tray application.
- Captures the monitor under the cursor with per-monitor DPI handling.
- Cleans OCR output with modes for plain text, code and logs.
- Offers optional side-by-side OCR correction before copying.
- Types text back through Unicode or scancode injection strategies.
- Applies process-specific injection modes and pacing after target confirmation.
- Stores snippets and secrets for repeated remote-console work.
- Imports and exports versioned snippet files with explicit conflict policies.
- Retains 20 local versions per store with previewed, revision-checked restoration.
- Protects secrets and capture history text with Windows DPAPI.
- Provides a Control Hub, configurable hotkeys, diagnostics and EN/FR
  localization.
- Packages as a self-contained Windows release with the OCR runtime beside the
  executable.

The capture overlay supports keyboard selection: press **K** to create a centered
region, use **arrow keys** to move it, **Shift+arrows** to resize it and **Ctrl**
for ten-pixel steps. **Enter** confirms; **Escape** cancels. Coordinates are in
physical pixels. Raw and code cleanup preserve boundary whitespace, including
indentation and trailing line breaks.

Injection rechecks the confirmed foreground window, process and keyboard layout
before each keystroke batch and stops when an observed change occurs. Scancode
mapping uses the target window's layout. Windows `SendInput` cannot atomically
bind input to a window; this guard reduces focus-change exposure but cannot
eliminate the race between checking the foreground and sending a batch.
Clipboard injection clears only the clipboard version it originally read, so
content copied during typing is retained.

## Local Data

Scrybe stores its data under:

```text
%LOCALAPPDATA%\Scrybe
```

The app keeps its data local by design:

- secrets and protected history text are encrypted with DPAPI;
- malformed JSON stores are moved aside as `*.corrupt.<timestamp>.json`;
- diagnostics show runtime paths, app-data locations and required OCR assets;
- logs and diagnostic reports are available from the About tab.

Stores reject saves after a failed read or when the file changed since loading.
A persistent sibling `.lock` file coordinates cooperating Scrybe processes; its
presence alone does not mean the store is locked. After a save conflict, use
**Reload and keep draft**, review the editor, then explicitly Save again.
Conflicting changes are not automatically merged. Invalid records, including
null list entries, are quarantined with the original payload preserved; if
quarantine fails, writes stay blocked.

Injection has a non-activating progress window with Stop and explicit interruption
reasons. See [Reliability and validation](docs/validation.md) for draft recovery,
OCR measurements, native/remote receivers, accessibility and clean-Windows checks.
See [Capture review, profiles and data exchange](docs/features.md) for the new
workflows, backup retention and import limits.

## Requirements

- Windows 10 19041 or newer.
- .NET 10 SDK. The repository pins SDK `10.0.103` in `global.json`.
- The bundled OCR model at `tessdata/eng.traineddata`.
- For published builds, keep the loose `x64/` runtime folder next to
  `Scrybe.exe`.

## Build And Run

From a fresh clone:

```powershell
dotnet restore Scrybe.slnx
dotnet build Scrybe.slnx --configuration Debug
dotnet test Scrybe.slnx --configuration Release
dotnet run --project src\Scrybe.App\Scrybe.App.csproj
```

Convenience scripts are available for terminal or double-click use:

- `Run.bat` launches the app from source in Debug.
- `Build.bat` builds Debug.
- `Test.bat` runs the test suite.
- `Release.bat` runs the local release pipeline.

The release pipeline verifies formatting, runs the Release tests, builds,
publishes, checks the published layout, generates an SPDX SBOM, writes SHA256
checksums, then creates a zip under `Dist/`.

The formatting gate is:

```powershell
dotnet format Scrybe.slnx --verify-no-changes
```

For a dry run of the release process:

```powershell
.\Build.ps1 -Mode Release -DryRun
```

Release signing is optional. Unsigned releases are allowed, but Windows
SmartScreen may warn users; publish the generated `.sha256` and `.spdx.json`
files with the zip. To sign a release with Authenticode, provide either
`SCRYBE_SIGNING_CERT_THUMBPRINT` for a certificate in the Windows certificate
store, or `SCRYBE_SIGNING_CERT_PATH` plus `SCRYBE_SIGNING_CERT_PASSWORD` for a
PFX file, then add `-Sign`:

```powershell
.\Build.ps1 -Mode Release -Publish -Sign
```

CodeQL runs automatically for public repositories. For a private repository,
enable GitHub code scanning and set the repository variable
`SCRYBE_ENABLE_CODEQL=true`; otherwise the CodeQL workflow is skipped so private
builds and releases are not blocked by an external repository setting.

## Project Status

Scrybe is in dogfooding. The main workflow is in place: OCR capture, text
injection, snippets, DPAPI-backed secrets, protected capture history,
diagnostics, hotkeys, packaging and the Control Hub.

The remaining work is driven by real use: polishing edge cases, making failures
clearer, and tightening the small interactions that matter when working inside
stubborn remote consoles.

## Repository Layout

```text
Scrybe/
|-- src/       # WPF app, core library, OCR engine
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
The release has separate [English notes](docs/release-notes.md) and
[French notes](docs/fr/release-notes.md).
