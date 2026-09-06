# Reliability and validation

[Français](fr/validation.md)

## Feature checks

See [Capture review, profiles and data exchange](features.md) for the user flows.
Regression tests cover cancelled OCR publication, exact edited whitespace,
profile routing/pacing, invalid drafts, version retention, stale restore previews,
backup integrity, DPAPI recovery and atomic snippet imports.
The overlay command also renders the OCR and import dialogs in EN/FR at 96/192 DPI,
checking bindings and accessible button bounds. Interactive focus and screen-reader
speech still require a real desktop.

## Recovering unsaved changes

All four managers provide **Reload and keep draft**. This reads the current store
revision without automatically saving the editor. Failed reads retain the current
list and keep writes blocked. Review the current list and draft, then explicitly
Save; another disk change after Reload is rejected again.

- Snippets and secrets preserve editor fields and select by stable ID. A deleted
  ID never selects an unrelated entry. Creating a new secret still requires a
  password.
- History preserves revealed draft text. If the entry was deleted externally,
  saving is disabled; the text is not applied to another entry.
- Settings preserve the form while refreshing the store revision. The status
  warns that Save replaces disk settings with the form. There is no automatic merge.
- Drafts remain in memory only. Closing the app discards them. Secret plaintext
  is never exported as a recovery file and follows the existing password editor's
  clear-on-unload behavior.

Library loads and mutations are serialized within each process, including
background capture history. Cross-process leases and hashes remain in force.
Snippet, secret and history mutations are staged until persistence succeeds;
failed changes do not enter palettes or get included in a later unrelated save.

## Injection feedback

A non-activating window shows the confirmed process/handle, processed-stroke
count, status and Stop button. Closing it also cancels. Progress never includes
injected text. Updates are throttled; final statuses distinguish target/layout
change, cancellation, unmappable input, native failure and elevation blocking.
There is no automatic resume: a new request goes through confirmation again.

Counts describe work by the sender, not proven reception by a remote console.
SendInput retains a residual race between foreground checking and input dispatch.

## Automated checks

Use the SDK pinned in `global.json`. Each validation invocation requires a **new
output directory**. Exit codes are 0 success, 1 validation failure, 2 invalid
arguments/existing directory. Failures after directory creation produce a
`failure.json` and logs.

```powershell
dotnet build Scrybe.slnx -c Release
dotnet test Scrybe.slnx -c Release --no-build
dotnet run --project tools/Scrybe.Validation -c Release --no-build -- ocr artifacts/ocr-check
dotnet run --project tools/Scrybe.Validation -c Release --no-build -- overlay artifacts/overlay-check
```

`tools/Scrybe.Validation/corpus.json` defines synthetic console/code samples,
light/dark backgrounds, 12/16 DIP fonts and 96/144/192 DPI. The runner renders PNGs,
uses real Tesseract, and evaluates all cleanup modes. `ocr.json` records cold
initialization, recognition/cleanup durations, exact CER and whitespace-normalized
CER. Exact CER exposes indentation/newline loss that normalized CER can hide.
Each case has a normalized CER budget (currently 0.05); exceeding it fails the
run. Timing has no hardware-dependent pass threshold. An optional third argument
selects another corpus file. Synthetic results do not cover every remote console.

The overlay check renders WPF views without displaying them. It drives the same
selection-key handler and checks window names, polite live regions, coordinate
text, bounded help/readout widths and actual text/background contrast (minimum
4.5). EN/FR, three DPI scales and negative monitor-coordinate metadata are covered.
This verifies automation properties and rendering, not actual spoken output or
physical monitor placement. Interactively test K, arrows, Shift+arrows,
Ctrl+arrows, Enter/Escape and spoken coordinates on each physical monitor.

## Native and remote input

Run on a controlled interactive desktop. The tool shows its own receiver, sends
synthetic text including Tab/Enter, and records actual reception. It never installs
layouts; a requested layout must already exist. The original thread layout is
restored on exit. Native input is excluded from unattended CI.

```powershell
dotnet run --project tools/Scrybe.Validation -c Release --no-build -- native artifacts/native-us --allow-input --layout=00000409 --scenario=complete
dotnet run --project tools/Scrybe.Validation -c Release --no-build -- native artifacts/native-fr --allow-input --layout=0000040c --scenario=complete
dotnet run --project tools/Scrybe.Validation -c Release --no-build -- native artifacts/native-focus --allow-input --scenario=focus
dotnet run --project tools/Scrybe.Validation -c Release --no-build -- native artifacts/native-cancel --allow-input --scenario=cancel
```

Every scenario exercises Unicode and scancode. `native.json` includes the synthetic
received text, result, layout, CER and key/modifier events. Complete scenarios
require exact equality. Interrupted scenarios require an aborted, incomplete
sequence. Foreground admission failure sends nothing and reports a failure.

For an actual RDP/Citrix transport, run the passive receiver **inside the remote
session**, then inject from Scrybe on the local machine through the remote client:

```powershell
dotnet run --project tools/Scrybe.Validation -c Release --no-build -- receiver artifacts/remote-receiver
```

The window displays the reference and an empty receiver. Inject the reference,
then select Compare. Reports contain counts, CER and pass/fail, excluding received
text and key logs. Repeat both injection modes, each installed layout, low/high
pacing, focus loss and emergency abort. Record client/server versions, DPI and
pacing alongside the reports. Local native tests do not validate remote transport.

## Packaged and clean-Windows checks

`Build.ps1` runs the published executable's self-test before signing/packaging.
It loads the real OCR DLLs/model, recognizes a bundled synthetic fixture and
verifies locale uniqueness/parity. It exits before user-data initialization,
hotkeys, clipboard operations and normal UI.

```powershell
dotnet publish src/Scrybe.App/Scrybe.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts/publish-check
& ./artifacts/publish-check/Scrybe.exe --self-test ./artifacts/package-result.json
./tools/New-PackageSandbox.ps1 -PublishDirectory ./artifacts/publish-check -OutputDirectory ./artifacts/sandbox-check
```

The report path must not exist. Automation must wait for the WinExe and verify
both its exit code and `passed` receipt. On a host with Windows Sandbox installed,
open the generated WSB. It maps the package read-only and the new evidence folder
read-write, disables networking/clipboard redirection, and runs the same test
without an installed SDK. Require `sandbox-receipt.json`; generating WSB alone
does not prove a clean-Windows pass. The generator supports WhatIf and does not
install Windows features. Syntax follows [Microsoft's WSB documentation](https://learn.microsoft.com/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-configure-using-wsb-file).

CI runs corpus/overlay checks and the packaged self-test. GitHub CI execution,
remote receipt and clean-Windows receipt remain distinct from local checks.
