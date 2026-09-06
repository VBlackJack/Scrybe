# Scrybe Architecture

[Français](fr/architecture.md)

Scrybe is split into three assemblies:

- `Scrybe.App`: WPF shell, tray app, coordinators, OS interop and ViewModels.
- `Scrybe.Core`: domain models, settings, stores, text/image preprocessing,
  hotkey parsing, DPAPI abstractions and shared interfaces.
- `Scrybe.Ocr`: Tesseract-backed OCR engine implementation.

The app is local-first. OCR, storage, settings, secrets and history all stay on
the Windows machine running Scrybe.

## Module Map

```mermaid
flowchart LR
    User["User / global hotkeys"] --> App["Scrybe.App WPF shell"]
    App --> Coordinators["Coordinators"]
    App --> ViewModels["Manager and settings ViewModels"]
    App --> Diagnostics["Diagnostics and support actions"]

    Coordinators --> Capture["Capture flow"]
    Coordinators --> Injection["Injection flow"]
    Coordinators --> Palettes["Snippet, secret and history palettes"]

    Capture --> WGC["Windows.Graphics.Capture"]
    Capture --> Overlay["WPF selection overlay"]
    Capture --> Ocr["Scrybe.Ocr / Tesseract"]
    Capture --> Clipboard["Windows clipboard"]
    Capture --> History["CaptureHistoryLibrary"]

    Injection --> Confirm["Target confirmation gate"]
    Injection --> SendInput["SendInput Unicode / scancode"]
    Injection --> Clipboard

    ViewModels --> Libraries["Snippet, Secret, History libraries"]
    Diagnostics --> LocalPaths["Runtime paths and asset checks"]
    Diagnostics --> Shell["Windows shell folder open"]
    Diagnostics --> Clipboard
    Libraries --> Stores["JSON stores"]
    Stores --> Atomic["AtomicFileWriter"]
    Stores --> Quarantine["Corrupt JSON quarantine"]
    Stores --> DPAPI["DPAPI CurrentUser + Scrybe entropy"]
    Atomic --> Disk["%LOCALAPPDATA%/Scrybe"]
    Quarantine --> Disk
```

## Runtime Flows

### Capture To Clipboard

1. A hotkey, tray item or hub action calls `CaptureCoordinator`.
2. `WgcScreenCaptureService` captures the monitor under the cursor.
3. `CaptureOverlayWindow` lets the user select a region.
4. `ImagePreprocessor` prepares the crop when enabled.
5. `TesseractOcrEngine` recognizes text.
6. `TextPostProcessor` applies the selected cleanup mode.
7. Optional `OcrReviewWindow` shows the original crop and an editable draft.
   `CapturePublication` publishes only accepted text to the clipboard and
   `OcrTextStore`; cancelling leaves both unchanged.
8. If capture history is enabled, `CaptureHistoryLibrary` persists a
   DPAPI-protected history entry.

The coordinator keeps capture single-flight and reports failures through the
notification service rather than letting UI-triggered tasks crash the process.
`KeyboardSelection` performs bounded move/resize arithmetic in physical pixels;
the overlay converts to DIP for rendering and announces coordinates through a
polite live region. `TextPostProcessor.ProcessCapture` preserves boundary
whitespace for Raw and Code Formatter modes.

### Text Injection

1. A hotkey, palette selection or manager action calls an injection coordinator.
2. Password-grade paths use `InjectionTargetConfirmer`, which captures target
   information, shows the native confirmation prompt, revalidates the target and
   restores foreground before typing.
   It resolves an exact process-name profile and freezes its mode and pacing
   before the confirmation prompt. The progress decorator preserves this snapshot.
3. `InjectionCoordinator` routes text through either `UnicodeInjector` or
   `ScancodeInjector` according to settings.
4. Both injectors use `SendInput` and return an `InjectionResult`.
5. Callers surface UIPI blocks, aborts and other failures explicitly.

Confirmation supplies an `IInjectionContext` holding window/process identity and
the target thread's keyboard layout. Native injectors reject a missing context.
They check it after the initial delay, before each batch and after each paced
stroke, including the final Tab/Enter. An observed mismatch latches the context
invalid. Scancode preflight and sending use the captured layout; special keys
release retained Shift in the same native batch. Cleanup runs on cancellation,
failure and exceptions. These checks cannot make `SendInput` window-bound or
eliminate the final check-to-send race.

Clipboard injection reads a stable text/sequence-number snapshot. On success,
the Windows writer opens the clipboard, compares its sequence number while
holding it open and clears only the matching version. A competing copy, even of
identical text, prevents clearing. Busy clipboard operations retry within the
existing bounded retry policy.

Secrets are never placed on the clipboard. The secret path decrypts into a
caller-owned `char[]`, types from memory and clears the buffer in a `finally`.

The settings screen also exposes the deterministic integrity harness used for
dogfooding injection behavior. It can copy the expected reference payload and
request a 100, 500 or 1000 character reference injection through the currently
selected strategy. The WPF host hides the hub before dispatching the request so
the target console can regain focus before `SendInput` starts.

### Diagnostics

The About tab uses `DiagnosticsInfoProvider` to report the local app-data
directory, settings/logs/snippets/secrets/history paths, app base directory,
locale directory, tessdata directory and required Tesseract/Leptonica assets.
Rows backed by file or directory checks carry an existence state so missing
runtime assets can be highlighted in the UI.

Support actions stay in `Scrybe.App` because they touch the Windows clipboard
and shell. `AboutViewModel` can copy a diagnostic report through
`IClipboardService` and open the logs or app-data directory through
`ISystemShell`.

### Persistence

The four JSON-backed stores are:

- `JsonSettingsStore`
- `JsonSnippetStore`
- `JsonSecretStore`
- `JsonCaptureHistoryStore`

All save paths use `AtomicFileWriter`: write a GUID-named temp file in the same
directory, flush with `WriteThrough`, then atomically move over the destination.
`SaveAsync` returns `false` on the already-caught I/O failure paths so callers
can notify users when memory and disk may diverge.

`JsonStoreFile<T>` admits these writes using a per-instance semaphore, a
persistent sibling `.lock` file opened exclusively and a SHA-256 comparison with
the last loaded/saved bytes. Cooperating processes hold the lease through the
comparison and atomic replacement. Failed reads and stale snapshots block saves
until a successful reload; an uninitialized store can only create a missing file.
Before replacement, `StoreBackups` saves the old bytes in a versioned envelope
under `<store>.versions`, retaining 20 versions. Its preview captures both the
backup hash and live-file hash. Restoration takes the same lease, validates both,
verifies payload structure and DPAPI decryptability, backs up the current bytes,
then replaces atomically. Settings require restart; libraries reload after restore.
This is conflict rejection, not merging. An external writer that ignores the
lease is outside the cross-process serialization contract. Startup loads the
libraries before displaying writable UI and registers the same settings store
instance that performed the initial load.

Load paths treat malformed JSON separately from missing files. A corrupt file is
quarantined next to the original as `*.corrupt.<timestamp>.json`, then the store
falls back to defaults or an empty collection without overwriting the preserved
payload.
Validation also rejects null records and required null fields. Writing a fresh
store is admitted only if the corrupt payload was successfully moved aside.

Secret and history values are protected with DPAPI `CurrentUser`; secrets also
use Scrybe-specific optional entropy and a self-describing protected-value
header so legacy values can be migrated safely.

## Boundaries

`SnippetExchange` owns the strict, versioned non-secret interchange format.
`SnippetLibrary.ImportAsync` verifies the reviewed snapshot and writes the entire
prospective collection once. WPF file dialogs and confirmation remain in App.

The recovery commands reload storage revisions while retaining editor drafts;
library-level gates serialize logical mutations as well as persistence. Injection
contexts report non-sensitive progress through the coordinator to a non-activating
WPF presenter. `tools/Scrybe.Validation` provides OCR, overlay and controlled
input checks; packaged `--self-test` branches before normal startup. See
[Reliability and validation](validation.md) for recovery semantics and coverage.

- `Scrybe.Core` must not reference WPF or Win32 UI APIs.
- `Scrybe.Ocr` owns OCR engine setup and recognition only.
- `Scrybe.App` owns OS-bound behavior: WPF, tray notifications, hotkeys,
  foreground-window restore, `SendInput`, Windows clipboard and WGC capture.
- User-visible text belongs in `locales/en.json` and `locales/fr.json`.
- New persistence code should preserve atomic writes and observable save
  failures.
- New diagnostics or shell actions should stay in `Scrybe.App` behind small
  interfaces so ViewModels remain unit-testable.
