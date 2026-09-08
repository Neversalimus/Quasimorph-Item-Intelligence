# Quasimorph Item Intelligence

**Item Intelligence** is an in-game item browser and reference tool for **Quasimorph**.

Prepared stable version: **v1.7.42.4**

Base stable source: **v1.7.42.2**, commit `c9e3f98d7202599f3d14629780fbf438b3069b8b`.

Steam Workshop: https://steamcommunity.com/sharedfiles/filedetails/?id=3780078201

## v1.7.42.4

- Prevents unrelated same-name items from merging into one recipe row; preserves canonical custom variants and independent chip unlocks.
- Explains unavailable Loot calculations and suggests manual mode only when it is usable.
- Runs actual production C# behavior tests in every build; release retries and complete payload hashes also have automated coverage.
- Adds reusable frozen-release scripts in `Release/` with atomic Git ref updates and resumable verified assets.
- The installer builds against the locally installed game and stages the existing public item `3780078201`. The release kit freezes the exact Windows-built payload before publication.
- Re-audits Loot modifiers, container estimates, Scavengers, story/start sources and direct cargo creation against the supplied 1.0.4 assemblies, with independent feature fingerprints.
- Fixes the 1.0.4 random-start table migration to `RandomStart_*`; older audited builds retain their `General_*` pools.
- Corrects amputation source parsing for vanilla weight/ID tuples, sums repeated outcomes and explicitly labels base probabilities.
- Runs 660 production behavior assertions and 26 release assertions; all 101 C# source files compile against the 118 current game dependencies.

Test3 game acceptance confirms Space and Dungeon operation, RU/EN switching, and 236 amputation slots covering 120 items in two campaigns. Core indexes report no partial failures; perk values, exact Trade contracts and Scavenger rows are present. Three isolated performance-budget overruns (51.2 ms cold, 20.5/24.0 ms warm) are recorded without evidence of a sustained slowdown. See `Support/RuntimeAcceptanceTest3.json` for scope and limits.

Promotion changes only the runtime version and marker in production C#; gameplay and localization are identical to Test3. Stable runtime marker: `1.7.42.4 (StableRelease17424)`. The final Windows DLL and public publication require the release kit's build/freeze and Steam steps.

Runtime data guards remain active, and game SHA `BE780...` is not promoted to the global VERIFIED identity. Container estimates disclose excluded budget, temporary Tech and location events. See `Support/Compatibility104.json` for the independent feature audit.

Run `pwsh -NoProfile -File ./BUILD_AND_STAGE.ps1 -Mode RELEASE -ContractsOnly` for build checks and behavior tests. Run `Tests/Run-MutationTests.ps1` to verify that deliberately broken production algorithms are rejected. No game files are needed for those tests. PowerShell 7 is required.

Russian release instructions: [INSTRUCTIONS_RU.md](INSTRUCTIONS_RU.md). Changes and validation scope: [FIX_REPORT_RU.md](FIX_REPORT_RU.md).

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
- English and Russian localization.
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
