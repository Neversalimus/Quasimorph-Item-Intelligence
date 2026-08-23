# Quasimorph Item Intelligence

**Item Intelligence** is an in-game item browser and reference tool for **Quasimorph**.

Current stable version: **v1.7.42.1**

Steam Workshop: https://steamcommunity.com/sharedfiles/filedetails/?id=3780078201

## Features

- Fast item inspector opened with **F2**.
- Search, catalog, favorites, history and advanced filters.
- Smart item overview, recipes, production relationships and Magnum requirements.
- Canonical disassembly relationships and reverse disassembly sources.
- Station-production relationships shown under Recipes, separate from live Trade data.
- Trade information with direct station navigation to the starmap.
- Quasimorph 1.0.3 stock-sensitive pricing with first-to-last unit movement and exact batch totals on audited builds.
- Two Trade layouts switchable directly inside the Trade tab; the selected layout is persisted.
- Loot sources grouped by containers, general placement, enemies, faction rewards, mission pools and special sources.
- Container chance estimates based on verified weighted pools, roll counts, Tech context and supported loot modifiers.
- Manual loot-modifier calculator for Marauder / Organization / Field Medic contexts.
- Faction technology information.
- Weapon/ammo relationships and detailed fire-mode tooltips, including Damage/AP and Critical Damage/AP where provable.
- English and Russian localization.
- Optional **Modder Mode** with audited item creation for ship cargo and mission clone inventory.

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