# ReClaim Cleaner integration

Source: https://github.com/Gercekkriptocu/reclaim-cleaner

Pinned commit: c154b018c715c38b1362da0ad8c3f016cf2c6b5d

Copyright (c) 2026 yusufky63 and contributors. MIT; see LICENSE.txt.

The 233 target rules in targets.json are copied unchanged and embedded in FromZero.
ReclaimRules.Match and validation/path handling, ReclaimGuard, and ReclaimCleaner
adapt the upstream Rust paths.rs, rules.rs, junk.rs and clean.rs to C# / WPF.
The original Rust crate and Tauri UI are not bundled or executed.

FromZero-specific differences:
- Only reviewed file snapshots are deleted; new files after scanning are preserved.
- Handles revalidate final paths, size, timestamps, link count and file attributes.
- Reparse points/cloud placeholders/hard links/read-only files are skipped.
- Folder and contents modes remove eligible files, retaining directory structures.
- No commands from rule data are executed. Report targets link to Windows storage tools.
- Admin targets require the app to have already been elevated by the user.
- At most 20,000 files per rule and 100,000 per scan are planned; partial scans are marked.
- Build-artifact discovery, duplicate search, AI-model management and space maps from
  the upstream application are not part of this integration.
