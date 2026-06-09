# ADR 0001 - Foundational stack

- **Status:** Accepted
- **Date:** 2026-06-07
- **Decider:** Julien Bombled

## Context

Scrybe is a standalone, local-first Windows utility that restores the broken
clipboard in both directions on locked-down remote consoles (RDP, vSphere/ESXi
VMRC, iDRAC/iLO, VNC/noVNC, locked SSH):

- **Extraction** - capture a screen region, OCR it, deliver clean code-aware text
  to the local clipboard.
- **Injection** - type an arbitrary string (long command, or a stored password)
  into the remote console as synthetic keystrokes, because paste is blocked.

The project license is **Apache 2.0**, which constrains every embedded dependency.
The stack evaluation compared OCR engines, capture APIs, injection mechanisms and
distribution models. Three load-bearing claims were verified before acceptance.

## Decision

### Runtime & UI
- **C# / .NET 10** (research assumed .NET 8; raised to 10 for parity with Heimdall.Next).
- **WPF** desktop, tray + topmost overlay, **Per-Monitor DPI Aware v2** mandatory.
- Reuse existing assets: `wpf-design-system` + **ThemeForge** (Dracula). No UI from scratch.

### Screen capture
- **Windows.Graphics.Capture (WGC)** + D3D11 for the freeze/select overlay.
- Coordinates handled in **physical pixels**, converted via per-monitor DPI context.

### OCR
- **Baseline (MVP): Tesseract 5** (Apache 2.0; Leptonica BSD-2-Clause). `tessdata_fast`
  by default, English model first, console-specific pre-processing chain.
- Engine kept **warm / pre-initialized at startup** to hold the p95 < 500 ms budget
  (same rationale as Heimdall's COM pre-warm).
- **Secondary (Phase 2): PaddleOCR / PP-OCR** (Apache 2.0) behind a feature flag for a
  high-accuracy mode on dark/compressed captures.
- **Windows.Media.Ocr rejected as baseline** - officially supported only with package
  identity (MSIX) and exposes no native confidence score. Reserved for an optional mode
  if MSIX is ever added.

### Injection
- **SendInput, scancode-first.** Never `PostMessage` / `WM_CHAR` as primary strategy.
- Subject to **UIPI**: can only inject into targets of equal or lower integrity level -
  failure must be explicit, never silent.
- Per-viewer **pacing profiles** (RDP / VMRC / iDRAC / iLO / noVNC).
- Two typing modes: **safe ASCII** (commands & secrets) vs **extended text** (only on
  user-validated keyboard layouts).

### Secrets
- **DPAPI (CurrentUser)** at rest; `SecureZeroMemory` immediately after typing.
- Secrets are **never placed on the clipboard**.
- Optional **Windows Hello / UserConsentVerifier** gate before secret auto-type.

### Distribution
- **Single-file self-contained EXE** for the MVP. MSIX only if the optional
  Windows.Media.Ocr mode is later added.

## Verified claims

| Claim | Verdict | Source |
|---|---|---|
| Windows.Media.Ocr needs package identity, no native confidence score | Confirmed (officially MSIX-only; works unpackaged but unsupported) | learn.microsoft.com |
| Capture2Text is GPLv3 (not reusable under Apache 2.0) | Confirmed | github.com/GSam/Capture2Text |
| Textify (m417z) is GPLv3 (not reusable under Apache 2.0) | Confirmed | github.com/m417z/Textify |

## Consequences

- **Positive:** license-clean Apache 2.0 stack; zero cloud / offline; reuses Heimdall
  WPF + ThemeForge investment; clear unique positioning (only bidirectional tool - no
  competitor does injection).
- **Negative / risks:** Tesseract accuracy depends heavily on the pre-processing chain;
  UIPI blocks injection into elevated targets (must be surfaced, not hidden); the
  p95 < 500 ms budget assumes a warm engine and a modest crop, not a full-screen OCR.
- **Deferred (own ADRs when reached):** PP-OCR integration (ONNX Runtime telemetry must
  be audited for the no-telemetry guarantee); MSIX packaging decision.

## Quality targets (from research, to validate against a real console corpus)

- Extraction: CER ≤ 0.5 % (clean) / ≤ 1.5 % (degraded), exact line match ≥ 98 %, p95 < 500 ms.
- Injection: 100 % keystroke integrity on supported profiles (100 / 500 / 1000 char tests).
- Corpus: RDP, VMRC, vSphere/noVNC, iDRAC, iLO, VNC, SSH (Win/Linux), BIOS/UEFI; light &
  dark themes; DPI 100 / 125 / 150 / 200 %.

## Addendum 2026-06-07 - OCR pre-processing, empirically corrected (post-increment 3b)

The initial stack evaluation recommended a binarization step (global + adaptive). **Empirical testing on
Tesseract 5 overturned this** and the pipeline doctrine is corrected accordingly:

- **Binarization is OFF by default.** Tesseract 5's LSTM reads contrasted **grayscale** better than
  binary; Otsu binarization + nearest-neighbor upscale destroyed thin antialiased glyphs and made OCR
  worse (`0.0.0.0` → `@.@.6.8`, CER 2.9% vs 0.7% raw). Binarization remains available behind an opt-in flag.
- **Winning pre-processing chain:** grayscale → automatic polarity detection + inversion → conditional
  **bilinear** upscale (not nearest-neighbor) → contrast normalization. Proven on a low-contrast
  light-on-dark fixture: CER 0.73% → 0.00% (`0.0.0.0` and `code=exited` corrected), guarded by a CI
  before/after assertion.
- **Contrast normalization is the real lever on dim text**; polarity inversion alone is insufficient.
  On already high-contrast text Tesseract needs no help.
- **Residual `0`→`@` glyph confusion** (model-level, seen on colored/highlighted text) is NOT solvable by
  pre-processing - it belongs to **text post-processing** (context-aware confusion correction, increment 4).

## Addendum 2026-06-07 - Injection mode, measured (post-increment 5a-hardening)

The initial stack evaluation stated "SendInput, **scancode-first**" as the doctrine, on the unmeasured assumption
that *remote viewers prefer scancodes*. We measured instead of assuming.

- **Keyboard layout matters: this dev machine is French AZERTY.** A static US scancode table produced
  garbage; the scancode path now resolves char→scancode against the **active layout** (`VkKeyScanEx`
  + `MapVirtualKeyEx`). The trigger chord Ctrl+Alt (= AltGr on AZERTY) had to be released before typing,
  and per-character events are sent as one **atomic** `SendInput` batch with a Shift **state machine**.
- **A Unicode strategy (`KEYEVENTF_UNICODE`) was added.** It sends codepoints directly, bypassing the
  layout/Shift/AltGr entirely, structurally eliminating all three scancode bug classes. Enter/Tab stay VK.
- **Pacing was the other root cause.** The 5a glitches were *two* cumulative faults: layout bugs AND a
  too-fast 4 ms inter-key delay (the Windows ~15 ms timer floor masked it). Raising the base delay to
  **25 ms** removed the dropped/duplicated characters in BOTH modes.
- **Local A/B (Notepad), known reference string, ground-truth comparison:**

  | Mode      | 100 chars | 500 chars | 1000 chars |
  |-----------|-----------|-----------|------------|
  | Unicode   | 100%      | 100%      | 100%       |
  | Scancode  | 100%      | 100%      | 100%       |

  Both reach **100%** once layout and pacing are fixed. Mixed case, digits, symbols and newlines included.
- **Default = `Unicode`** (layout-independent, simpler, eliminates the bug classes by construction);
  **scancode is the fallback**. Safe default for local apps and most non-elevated viewers.
- **Remote-console A/B was pending at 5a time.** It has since been measured on real remote targets; see
  the 2026-06-08 remote-console addendum below.

## Addendum 2026-06-08 - Secret vault, local-first (post-increment 5c)

The secret path is now implemented as a separate vault, not as encrypted snippets:

- **At rest:** `%LOCALAPPDATA%/Scrybe/secrets.json` stores only non-secret metadata plus DPAPI
  CurrentUser ciphertext. A deterministic test reads the raw JSON and proves the plaintext marker is
  absent.
- **At type time:** the palette shows only metadata, decrypts only the selected secret, restores the
  target foreground window, and routes typing through the existing `InjectionCoordinator`. Secrets are
  never written to the clipboard and are never included in FileLogger messages.
- **Memory handling:** DPAPI unmanaged plaintext blobs are overwritten before release and managed byte
  buffers are cleared with `CryptographicOperations.ZeroMemory`. The hardened reveal-to-inject path now
  returns a caller-owned `char[]`, streams keystrokes from `ReadOnlyMemory<char>` without building a full
  secret `string` or keystroke sequence, and clears the `char[]` in a `finally` after typing. The remaining
  limitation is explicit: WPF `PasswordBox` and the managed runtime can still create or move copies that
  cannot be reliably scrubbed in place.
- **Runtime proof:** published `Dist/win-x64/Scrybe.exe`, controlled local text target,
  `Ctrl+Alt+K` -> Enter, received `SCRYBE_SECRET_TEXTBOX_OK_8443`; the marker was absent from both vault
  JSON and the Scrybe log.

## Addendum 2026-06-08 - Remote-console injection, measured (post-0005a-remote)

The remote-console gate is now measured on real targets, not inferred from local Notepad:

- **Secret auto-type proof:** Scrybe typed a DPAPI-protected secret through the hardened secret path
  (`Ctrl+Alt+K`, explicit target confirmation, no clipboard) into an XRDP password field opened through
  Heimdall TestEnv via a gateway tunnel. XRDP accepted the login. This proves the actual secret use case
  end-to-end for that target family.
- **Integrity matrix target:** embedded Heimdall RDP session to Windows Server 2022
  (`192.168.31.136:3389`), Notepad as the receive target, remote PowerShell clipboard comparison as the
  verifier. The reference corpus included mixed case, digits, symbols and newlines. The Scrybe published
  executable was used, with 25 ms injection pacing.

  | Mode @ 25 ms | 100 chars | 500 chars | 1000 chars |
  |--------------|-----------|-----------|------------|
  | Unicode      | 100%      | 100%      | 100%       |
  | Scancode     | 100%      | 100%      | 100%       |

- **Logs corroborated each run:** 0 skipped characters in both modes; 1000-char runs completed in about
  32.4 s. `Ctrl+Alt+F3` was intercepted or ineffective in this viewer, so a debug alias
  `Ctrl+Alt+F6` was added for the 1000-char reference injection.
- **Decision:** keep **Unicode** as the default remote injection mode because it is layout-independent and
  passed the measured RDP/XRDP targets. Keep **Scancode** as the validated fallback. Keep **25 ms** as the
  default pacing for these profiles; no escalation to 50/100 ms was needed.
- **Scope of proof:** this validates the current RDP Windows Server 2022 and XRDP-through-gateway targets.
  VMRC, iDRAC/iLO, browser noVNC, VNC and BIOS/UEFI remain separate profile validations, not assumptions.

## Addendum 2026-06-09 - DPAPI entropy and protected-value versioning (post-audit I-1)

The initial secret vault used DPAPI `CurrentUser` without `optionalEntropy`. That protected secrets
against other Windows users, but any process already running as the same user could attempt generic
DPAPI unprotect sweeps over `%LOCALAPPDATA%/Scrybe/secrets.json`.

- **Decision:** Scrybe now passes stable application-specific entropy to both `CryptProtectData` and
  `CryptUnprotectData`. This is defense in depth, not a cryptographic boundary against a fully
  compromised user session: a targeted attacker that reverse-engineers Scrybe can recover the entropy.
  The proportionate goal is to bind the vault to Scrybe's scheme and defeat opportunistic tools that
  blindly unprotect every DPAPI blob they find for the current user.
- **Protected-value format:** the JSON schema is unchanged. `SecretEntry.ProtectedSecret` remains a
  base64 string, but new values encode `SCRYBEDPAPI` magic bytes, a version byte `0x01`, then the DPAPI
  ciphertext protected with Scrybe entropy. Values without that marker are treated as legacy DPAPI blobs
  and are unprotected without entropy.
- **Migration:** `SecretLibrary.LoadAsync` performs eager, per-entry migration when the configured
  protector supports it. A legacy value is decrypted with the legacy path, re-protected with the current
  entropy scheme, and the store is saved once only if at least one entry changed. If one entry cannot be
  migrated, Scrybe logs the failure and keeps that original protected value rather than dropping or
  corrupting it.
- **UX unchanged:** no master password or additional prompt is introduced. The vault remains local-first
  and auto-type still works without placing secrets on the clipboard.
