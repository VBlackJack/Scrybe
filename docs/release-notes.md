# Scrybe v2026.090601

[Français](fr/release-notes.md)

## Changes

- Review and correct OCR text beside the captured region before copying.
- Use application profiles for injection mode and pacing, displayed at target
  confirmation and frozen for each request.
- Restore local store versions after reviewing metadata and confirming the
  replacement. The previous version is saved first; 20 versions are retained.
- Import and export versioned snippet libraries, inspect templates and select
  keep, replace or copy handling for conflicts.
- Recover editor drafts after storage conflicts; reject stale writes and keep
  failed changes out of live libraries.
- Show injection progress and cancellation, recheck foreground identity and
  keyboard layout, and preserve newer clipboard content.
- Select capture regions with the keyboard and preserve boundary whitespace.
- Add OCR corpus measurements, interface rendering checks, packaged OCR
  self-tests and a Windows Sandbox configuration generator.
- Provide separate English and French documentation.

## Data and compatibility

Review is optional and disabled by default. Profiles identify the local process.
Backups retain existing DPAPI protection and require the same Windows account and
machine for protected content. Deleted items can remain in retained versions.
Restart after restoring settings. Snippet exchange is limited to 4 MiB and 1000
items and excludes secret and history stores.

Keep the x64 runtime and tessdata assets with the executable. Native reception,
RDP/Citrix behavior, spoken accessibility and clean-Windows execution require
their respective interactive checks. Local automated checks do not certify those
environments.
