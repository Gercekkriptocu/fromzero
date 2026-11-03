# FromZero 0.16 implementation tracker

User authorized all independently achievable improvements from the nine-part 0.15 roadmap. No subagents. Do not execute real cleanup, uninstall, game/GPU/network/power/driver or OS changes while validating. Use pure/mocked tests and read-only inventory/network/file checks. Files UTF-8 explicitly in Python.

Implement concrete UI-connected features:
1. Evidence-based preparation plan with current/target/source/restart/recovery and no unnecessary legacy runtime defaults; preserve existing schema-2 plans.
2. Offline driver inventory manifest: hardware matching, arch/OS evidence, CAT signature, network-first export index, verify before preparation install; no magic guarantee of missing packages.
3. Durable per-attempt journal + resume evidence for completed package/app entries; preserve records on mismatch; no full rollback promises.
4. Repeated FPS comparison (three before/after CSV runs), same protocol checks, 1% low/P99/stutter and explicit non-comparable states.
5. Bound-interface DNS/direct TCP and gateway ICMP quality, measured suggestions and before/after repair proof.
6. App size/startup/running filters, protected shared components, removal result inventory verification and read-only leftover preview.
7. Local executable import analysis (PEReader), game-dependent runtime needs; no unknown legacy bulk auto-install.
8. Source health, crosshair/ranking freshness, unknown player/code separation and offline crosshair export/import bounded validated.
9. Text scaling/accessibility, redacted diagnostic preview/export, SBOM/integrity, optional real signing tooling; trusted auto-update blocked without actual pinned publisher key/certificate.

Implemented all nine UI-connected areas above. Source health also checks individual manufacturer support pages. Existing saved schema-2 preparation opened with its previous selections. English and Turkish additions are embedded and the 0.16 release notes are included in the package.

Validation: 383 checks passed, including prepared-app presence on resume, attempt journal tampering, Unicode INF and CAT digest checks, real PE import parsing, benchmark protocol, DNS response validation, local support report redaction, pinned test signature and downgrade refusal, real packaged worker startup, final-file integrity, and live public FACEIT refresh. `artifacts/checks-0.16.0-final.log` holds the test output. Package integrity, all 415 listed file hashes, and the ZIP digest were independently verified in `artifacts/package-0.16-verification.json`.

UI review: inspected the preserved preparation plan and its new detail window, Workspace report preview, text scaling, FPS lab, and a real read-only connection quality measurement. Fixed sidebar label clipping at 130% and grouped CS2 Restore with the catalogue glyph so they wrap together. Updated the quality button to show its result immediately. The packaged 0.16 app was launched after the final build. No real driver installation, cleanup, uninstaller or settings optimization was run.

The package is a local unsigned preview. Optional publisher signing and pinned update verification are implemented. A real publishing certificate and controlled distribution channel are needed for production signed updates. Physical USB clean-install testing and real game benchmark captures remain environment-dependent.
