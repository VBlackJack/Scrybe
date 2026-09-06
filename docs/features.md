# Capture review, profiles and data exchange

[Français](fr/features.md)

## Review OCR before copying

Enable **Review OCR before copying** in Settings and save. Capture a region as
usual. The original crop appears beside the recognized, cleaned text. Edit the
text, then choose **Accept and copy**. The accepted text becomes the last capture
and enters protected history when history is enabled.

Cancel or close the review to leave the previous clipboard, last capture and
history unchanged. No capture image is saved on cancellation. Review is disabled
by default. Spaces, tabs and line endings are preserved after editing. Enter and
Tab insert characters in the editor; Ctrl+Tab moves keyboard focus out of it.
Escape cancels. Review drafts and images remain in memory.

## Application injection profiles

In Settings, add a profile, select it and enter the target process name without
the `.exe` suffix, such as `mstsc`. Choose Unicode or scancode, the delay between
keys (5 to 200 ms) and additional delay after Enter (0 to 1000 ms), then save.

Names are matched exactly, ignoring case. Empty names, duplicate names, executable
paths, wildcards and invalid delays are rejected. Unmatched targets use global
settings. Profiles identify the local process, so remote applications sharing the
same RDP/Citrix client also share its profile.

The confirmation prompt displays the selected strategy and delays. These values
are frozen for that request, including secret injection. A profile never bypasses
target confirmation, foreground/layout checks or cancellation. It does not switch
the Windows keyboard layout.

## Local versions and restoration

Before replacing an existing JSON store, Scrybe saves the previous bytes in
`<store>.versions` next to settings, snippets, secrets or history. Each envelope
has a format version, timestamp, store name, SHA-256 and payload. At most 20
versions are retained per store. First creation has no previous version.
Saving identical bytes does not consume a version or evict useful history.

Open **Versions and restoration** in Settings. Select a version and continue.
Review the store name, timestamp, record count and byte count in the confirmation,
then choose **Restore this version**. The app rejects changed previews, corrupt
versions and unsupported payloads. The current version is backed up before atomic
replacement. A required backup failure also blocks the normal store save.

Libraries reload after restoration. Reload open editors before saving drafts.
Restart Scrybe to apply restored settings; the live settings form is not silently
replaced. Drafts are not backups and remain in memory only.

Secret and history payloads retain their DPAPI encryption. Restoration checks
decryptability under the current Windows account before writing and clears
temporary decrypted buffers. Backups are intended for the same account and
machine, not portable secret exchange. Settings, snippet templates and display
metadata remain readable; base64 encoding of an envelope payload is not encryption.
Deleted secrets and history can remain in the retained versions until those
versions expire. Quarantine and version history are separate mechanisms.

## Import and export snippets

The snippet manager exports the committed library, excluding editor drafts,
secret stores and history stores. Select a **new** JSON destination. Existing
files are not overwritten.

Import accepts the `Scrybe.Snippets` version 1 envelope, up to 4 MiB and 1000
snippets. Unknown fields or versions, duplicate JSON properties, duplicate IDs,
null records and malformed parameter definitions are rejected. Save or discard
an open draft before starting an import.

The preview lists every incoming snippet. Select one to inspect its template and
parameter defaults. Choose one policy for the import:

- **Keep existing snippets:** skip incoming conflicts.
- **Replace conflicting snippets:** replace the single matching record while
  retaining its stable ID. Ambiguous matches are rejected.
- **Import conflicts as separate copies:** preserve existing records and give
  conflicting incoming records new IDs.

A conflict is an identical ID or the same name and category, ignoring case for
display names. The policy also applies between incoming items processed in order.
Import validates the final collection and saves once. If the reviewed library or
disk revision changed, the import is refused without publishing partial results.

The format is for non-secret templates. Text manually entered into templates or
parameter defaults can still be sensitive; inspect it before sharing the file.

## Documentation and release files

English documentation uses the standard paths. French mirrors use
`README.fr.md`, `CHANGELOG.fr.md` and `docs/fr/`, with reciprocal language links.
Release notes follow the same separation. Repository topics describe implemented
capabilities and do not certify remote transport compatibility.
