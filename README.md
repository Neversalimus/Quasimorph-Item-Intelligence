# Quasimorph Item Intelligence

**Item Intelligence** is an in-game item browser and reference tool for **Quasimorph**.

Current stable source: **v1.7.42.6 — StableRelease17426**. Publication is performed through the frozen release workflow.
Steam Workshop: [Item Intelligence](https://steamcommunity.com/sharedfiles/filedetails/?id=3780078201).
Accepted candidate: `b6604f579360522b7173242801d743e345facaf0`, based on stable **v1.7.42.5**.

This release restores the independently audited Trade, loot, cargo and ordinary body/implant paths on Quasimorph **1.0.4.582s.c4335c5**. The game patch also changed fire-mode scatter: QII now includes the vanilla augmentation multiplier and zero clamp, retaining the old formula when the new API is absent. All eight languages, large-window controls and the Chinese MCM fix are retained. The supplied test2 run confirms restored prices and ES/PT-BR/FR browser rendering. All 1971 new translations have been reviewed against English; native-speaker refinements remain welcome. See [release acceptance](Support/ReleaseAcceptance17426.md), [compatibility audit](Support/Compatibility104582.md), [localization review](Support/LocalizationReview17426.md) and [release setup](INSTRUCTIONS_RU.md).

## Large window and zoom

The footer now offers **Large window / Normal window** and **− / +** zoom controls. Large view fills 94% of the screen width and height (including the Modder drawer when enabled). Normal view starts at 100%; large view starts at 150%. Each view remembers its own 100–200% preference in 25-point steps, as well as the last selected view, across restarts.

Fonts, icons and hit targets scale uniformly. Columns use the available width; full notes rewrap, row counts and scrolling adapt. Search and catalog remain reachable, and weapon-mode hover cards stay on screen. On unusually narrow/short screens, uniform zoom is limited to keep controls visible; the displayed percentage is the effective value. The requested preference is retained for returning to a larger screen.

These controls own their settings directly, so saving unrelated MCM options cannot restore an older view preference. At 1920×1080, the large default displays nine rows and makes the UI 50% larger; 200% displays four rows. Game UI outside Item Intelligence is unaffected.

## Implant source correction

Enemy implant sources now follow pre-selection body-slot eligibility, then post-selection nature and socket constraints. Failed installations remain in the selection denominator. The numeric percentage is successful installation at ordinary enemy generation, including earlier preset implants, shared socket competition, uniform socket ranges and ordinary body variants. The amputation recovery percentage remains separate.

When random augmentations can change the body, the model excludes outcomes impossible across the retained body possibilities and marks the remaining sources **Conditional**. It does not claim exact probabilities for the sequence of random augmentations, health-threshold checks or explicit body overrides in special spawns. Random augmentation source rows also use this label. “Preset” describes a configured attempt and does not promise a guaranteed installation.

The index runs in slices and reads game records without creating creatures/items or advancing gameplay RNG. Ctrl+Shift+F10 with an implant selected appends record evidence to the normal manual diagnostics report; this supports investigation without farming a rare drop.

## Localization

All eight languages and the community template contain 657 keys. Spanish, Brazilian Portuguese and French cover the catalog, item relationships, loot conditions, MCM, window controls and Modder Mode. Placeholders, query syntax and compositional spaces are preserved.

MCM offers Auto (Game), English, Русский, Deutsch, Polski, 简体中文 / Chinese, Español, Português (Brasil) and Français. Existing option order is retained. Auto reads the selected Quasimorph language, including the exact game identifiers Spanish, BrazilianPortugal and French. Portuguese support is specifically Brazilian; pt-PT is not mapped to it. Manual selection changes QII UI and numeric formatting; game-provided names and search follow the game language. The browser refreshes after saving settings; MCM captions refresh on the next launch.

The new languages use decimal commas. Spanish and French percentages include a space; Brazilian Portuguese percentages do not. Time units are localized, including French day abbreviation `j`. Long notes use the conservative Latin-language wrap fallback.

QII reads Quasimorph's font presets for the chosen language. For mixed scripts it creates a private font asset with a separate fallback list; vanilla fonts and global TMP settings are untouched. CJK notes wrap at text-element boundaries. Windows-installed Chinese fonts are not required by this implementation.

MCM uses the game's font even when QII supplies captions in another language. QII supplies private Chinese and Latin fallbacks to its settings, sidebar entry, dropdown options and shared tooltip while QII owns it. Reopening MCM reapplies the font after MCM's own font updates. Hovering another mod's setting restores the shared tooltip's original font. This optional integration was checked against MCM source commit `2e500a5f5cf02ffc6c9739e1933b738949bae265`; a warning is logged if its hooks are unavailable. Test5 logs confirm all three hooks, both font assets and the saved Chinese preference on the next process; screenshots show the dropdown and Chinese captions rendering correctly.

`Install.ps1` builds and stages the **public item 3780078201** at `C:\QM_Workshop\ItemIntelligence`. Publication uses the hash-pinned source bundle and [frozen release workflow](Release/README.md), so Steam and GitHub receive the same validated payload. No automatic Steam upload is performed. DEV 3781927679 remains available for future tests. New ES/PT-BR/FR Workshop descriptions are included in `Workshop/`.

## Verification

Current release contracts pass **66052 assertions**. The release source compiles against the supplied 1.0.4.582 DLLs without warnings. [Acceptance report](Support/ReleaseAcceptance17426.md) distinguishes the fresh test2 evidence from historical runs. The only runtime change after test2 is catalog-label autosizing plus version/marker.

- Stable source: all 112 production C# files compile as C# 5 against the supplied 118-dependency 1.0.4.582 set, with zero warnings. The accepted candidate additionally compiled against 1.0.4.581.
- 693 production behavior assertions and 32 release workflow assertions pass.
- 4939 body/implant assertions include an independent exhaustive draw-sequence oracle, socket-range averaging, runtime record-adapter fixtures, missing data and conditional UI formatting.
- 48 scatter assertions execute the complete production scatter owner against modern, legacy and incompatible API fixtures, including additive effects, live changes, zero clamping and invalid values.
- 5385 localization assertions cover real language-file selection and key resolution, metadata precedence, cache replacement, manual/Auto behavior, damaged files, fallback text, formatting, CJK wrapping and private font fallback ownership. Game/TMP/MCM services are fixtures; these are not Unity visual tests.
- 54917 viewport geometry assertions cover 12 resolutions, both views, five zoom levels, Modder drawer, icon variants, footer/column bounds and hover-card placement. These execute production geometry and chrome writes with RectTransform fixtures, not Unity rendering.
- 38 MCM font assertions exercise the complete production callbacks and ownership lifecycle, detached dropdown options, repeated activation, late font availability and restoration for another mod's tooltip. Unity/TMP/MCM objects are fixtures. Separate Test5 game evidence confirms Chinese captions and dropdown rendering after persistence across a full restart; shared-tooltip restoration was not shown in screenshots.
- Historical 1.7.42.5 evidence: Test3 confirms index completion and observed EN/ZH layouts at 2560×1440. Test4 adds PL/DE layouts and catalog at 1920×1080, the corrected item-link alignment and a Polish preference surviving restart. Both historical logs retain the separate MCM slider exception; the F2 session records a 61.5 ms cold open. Enlarged Chinese, expanded PL/DE Loot and Trade, lower resolutions, view/zoom restart persistence and selected-implant export remain pending. This is partial acceptance, not global game compatibility certification.

Run `pwsh -NoProfile -File ./BUILD_AND_STAGE.ps1 -Mode RELEASE -ContractsOnly` for the contracts and behavior suites. No game files are needed for that command. The installer performs the actual Windows build against the local game.

Russian setup and game test: [INSTRUCTIONS_RU.md](INSTRUCTIONS_RU.md).
Changes and limits: [FIX_REPORT_RU.md](FIX_REPORT_RU.md).
Previous stable audit: [Support/Audit17424.md](Support/Audit17424.md).

## Features

- Fast item inspector opened with **F2**.
- Search, catalog, favorites, history and advanced filters.
- Smart item overview, recipes, production relationships and Magnum requirements.
- Canonical disassembly relationships and reverse disassembly sources.
- Station-production relationships shown under Recipes, separate from live Trade data.
- Trade information with direct station navigation to the starmap.
- Quasimorph 1.0.3 / 1.0.4 stock-sensitive pricing with first-to-last unit movement and exact batch totals on audited builds.
- Two Trade layouts switchable directly inside the Trade tab; the selected layout is persisted.
- Loot sources grouped by containers, general placement, enemies, faction rewards, mission pools and special sources.
- Container chance estimates based on verified weighted pools, roll counts, Tech context and supported loot modifiers.
- Manual loot-modifier calculator for Marauder / Organization / Field Medic contexts.
- Faction technology information.
- Weapon/ammo relationships and detailed fire-mode tooltips, including Damage/AP and Critical Damage/AP where provable.
- English, Russian, German, Polish, Simplified Chinese, Spanish, Brazilian Portuguese and French UI with a separate MCM language selector.
- Optional **Modder Mode** with audited item creation for ship cargo and mission clone inventory.

## v1.7.42.2

Quasimorph `1.0.4.581s.2952480` Trade-pricing compatibility hotfix.

- Re-audited the vanilla Trade pricing IL on Quasimorph 1.0.4.
- Restored exact station prices, batch totals and stock-sensitive next-unit prices instead of fail-closed `?`.
- Added the 1.0.4 Assembly fingerprint only to the Trade-owned compatibility gate.
- Loot, Scavenger, cargo-spawn and other exact feature families remain independently guarded and were not promoted by this hotfix.
- Runtime acceptance confirmed `Exact103Pricing=True` on Assembly SHA `BE780...`.
- A vanilla transaction check confirmed the displayed final price: vanilla `259` with `+50%` surcharge resolves to `388`, matching Item Intelligence.
- Stable runtime marker: `1.7.42.2 (StableRelease17422)`.

Validated game build: Quasimorph `1.0.4.581s.2952480`.

Validated Assembly-CSharp SHA-256:

`BE78036434737521BB43DABBD934B579A08DC3E8370B834AEE36E40932F56FE0`

Gate-approved DLL SHA-256:

`6E1E7DFD642CA5A5F735CC51BDE68CEB7E9A0D52D567681902C850682E5699D9`
## v1.7.42.1

Quasimorph `1.0.3.578s.024ad60` compatibility restoration.

- Restored the exact Loot modifier calculator for Marauder I-IV, Organization and Field Medic on the current game hotfix.
- Restored save-aware container chance estimates.
- Restored exact Scavengers / Purge Brigade mission chance rows.
- Restored verified scripted story acquisition sources and random starting equipment source pools.
- Kept the new `A38...` game fingerprint feature-owned: Trade, cargo spawn, Loot modifiers, container estimates, Scavengers and source families have independent narrow compatibility gates instead of globally trusting unrelated exact domains.
- Current-game IL audits passed `7/7` for Loot/Scavenger paths and `10/10` for the remaining source-family paths.
- Runtime acceptance confirmed Marauder I-IV, Organization, Field Medic, `ContainerSaveEstimate`, `ScavengersExact`, `hardcodedCurrentBuild=enabled`, disassembly symmetry, ammo sanity and exact 1.0.3 Trade pricing.
- The public Steam Workshop payload was verified byte-identical to the gate-approved payload before this GitHub release.

Stable runtime marker:

`[ItemIntelligence] ACTIVE VERSION 1.7.42.1 (StableRelease17421).`

Validated runtime game build: Quasimorph `1.0.3.578s.024ad60`.

Validated Assembly-CSharp SHA-256:

`A38C4D993C9BF60D0DDE0EDD348F201C97574F907808417A33C8A20F4772E9C1`

Gate-approved / Steam-published / GitHub-release Item Intelligence DLL SHA-256:

`FEFD4FD75A1BB13DE022BFC80E16A9D3773EAED86C2CA54768CACF71307CEFD0`
## Installation

### Steam Workshop

The recommended installation method is the Steam Workshop item linked above.

### Manual / source build

For a local stable build on Windows with Quasimorph installed:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\Install.ps1
```

The release source kit contains source, a Git bundle and version-specific preparation/publication launchers. The installer compiles the DLL locally; game dependencies and compiled binaries are not bundled. Use the preparation launcher for public releases so the source, payload, ZIP and receipt remain frozen together.

## Compatibility and safety

Exact numerical claims are guarded by feature-owned compatibility contracts. When a future game build cannot be verified, affected exact calculations fail closed rather than silently presenting unsupported numbers. Presentation-only preferences remain independent from exact-math SHA gates.

Modder Mode intentionally exposes save-mutating item creation actions and is disabled unless explicitly enabled.

Item Intelligence is a third-party mod and is not affiliated with Magnum Scriptum.
