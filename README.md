# Quasimorph Item Intelligence

**Item Intelligence** is an in-game item browser and reference tool for **Quasimorph**.

DEV candidate: **v1.7.42.5-test2 — ImplantBodyLanguagesTest02**.
Gameplay base: released **v1.7.42.4**, commit `05fba90596241f30d09fa47b1ddb0d8ad210bba7`.
The branch also retains the frozen GitHub release recovery correction from `2314b29931be5b7dd80a426edba970a2f315f8b3`.

## Implant source correction

Enemy implant sources now follow pre-selection body-slot eligibility, then post-selection nature and socket constraints. Failed installations remain in the selection denominator. The numeric percentage is successful installation at ordinary enemy generation, including earlier preset implants, shared socket competition, uniform socket ranges and ordinary body variants. The amputation recovery percentage remains separate.

When random augmentations can change the body, the model excludes outcomes impossible across the retained body possibilities and marks the remaining sources **Conditional**. It does not claim exact probabilities for the sequence of random augmentations, health-threshold checks or explicit body overrides in special spawns. Random augmentation source rows also use this label. “Preset” describes a configured attempt and does not promise a guaranteed installation.

The index runs in slices and reads game records without creating creatures/items or advancing gameplay RNG. Ctrl+Shift+F10 with an implant selected appends record evidence to the normal manual diagnostics report; this supports investigation without farming a rare drop.

## Localization candidate

German, Polish and Simplified Chinese were recovered from the older `Languages_MCM_Test2_Full_BuildFix2` source package and selectively ported. All five languages and the community template contain 653 keys. Current availability and amputation explanations are preserved and translated.

MCM offers Auto (Game), English, Русский, Deutsch, Polski and 简体中文 / Chinese. Auto reads the selected Quasimorph language, including inherited singleton metadata and the current EnglishUS / ChineseSimp identifiers. Manual selection changes QII UI and numeric formatting; game-provided entity names and search continue to use the game's language. Changes refresh the browser without restarting. MCM's own registered captions refresh on the next game launch.

QII reads Quasimorph's font presets for the chosen language. For mixed scripts it creates a private font asset with a separate fallback list; vanilla fonts and global TMP settings are untouched. CJK notes wrap at text-element boundaries. Windows-installed Chinese fonts are not required by this implementation.

The candidate is for DEV item **3781927679** and stages into `C:\QM_Workshop\ItemIntelligence_DEV`. Run `Install.ps1` in PowerShell 7, then use the developer-console command it prints. No Workshop or GitHub upload runs automatically. The stable-source gate blocks public release of this test version.

## Verification

- All 108 production C# files compile as C# 5 against the 118 supplied game dependencies, with zero warnings.
- 660 existing production behavior assertions and 32 release workflow assertions pass.
- 4923 body/implant assertions include an independent exhaustive draw-sequence oracle, socket-range averaging, runtime record-adapter fixtures, missing data and conditional UI formatting.
- 3331 localization assertions cover real language-file selection and key resolution, metadata precedence, cache replacement, manual/Auto behavior, damaged files, fallback text, formatting, CJK wrapping and private font fallback ownership. Game/TMP/MCM services are fixtures; these are not Unity visual tests.
- Runtime body-source indexing, the manual record export, language switching, MCM captions/options, font appearance and narrow-column fit still require the new candidate to be tested in-game. Earlier stable logs do not certify this candidate.

Run `pwsh -NoProfile -File ./BUILD_AND_STAGE.ps1 -Mode TEST -ContractsOnly` for the contracts and behavior suites. No game files are needed for that command. The installer performs the actual Windows build against the local game.

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
- English, Russian, German, Polish and Simplified Chinese UI with a separate MCM language selector.
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

Download the stable ZIP from **GitHub Releases**, or build the source on Windows with Quasimorph installed:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\Install.ps1
```

Compiled binaries are intentionally kept out of Git history and are distributed through Steam Workshop / GitHub Releases.

## Compatibility and safety

Exact numerical claims are guarded by feature-owned compatibility contracts. When a future game build cannot be verified, affected exact calculations fail closed rather than silently presenting unsupported numbers. Presentation-only preferences remain independent from exact-math SHA gates.

Modder Mode intentionally exposes save-mutating item creation actions and is disabled unless explicitly enabled.

Item Intelligence is a third-party mod and is not affiliated with Magnum Scriptum.
