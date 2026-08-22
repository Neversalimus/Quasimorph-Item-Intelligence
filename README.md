# Quasimorph Item Intelligence

**Item Intelligence** is an in-game item browser and reference tool for **Quasimorph**.

Current stable version: **v1.7.41.3**

Steam Workshop: https://steamcommunity.com/sharedfiles/filedetails/?id=3780078201

## Features

- Fast item inspector opened with **F2**.
- Search, catalog, favorites, history and advanced filters.
- Smart item overview, recipes, production relationships and Magnum requirements.
- Canonical disassembly relationships and reverse disassembly sources.
- Station-production relationships shown under Recipes, separate from live Trade data.
- Trade information with direct station navigation to the starmap.
- Quasimorph 1.0.3 stock-sensitive pricing with first-to-last unit movement and exact batch totals on audited builds.
- Two Trade layouts: station cards by default and the previous compact table via MCM.
- Loot sources grouped by containers, general placement, enemies, faction rewards, mission pools and special sources.
- Container chance estimates based on verified weighted pools, roll counts, Tech context and supported loot modifiers.
- Manual loot-modifier calculator for Marauder / Organization / Field Medic contexts.
- Faction technology information.
- Weapon/ammo relationships and detailed fire-mode tooltips, including Damage/AP and Critical Damage/AP where provable.
- English and Russian localization.
- Optional **Modder Mode** with audited item creation for ship cargo and mission clone inventory.

## v1.7.41.3

Compatibility hotfix for Quasimorph `1.0.3.578s.024ad60`.

- Restored exact Trade prices after the game hotfix changed `Assembly-CSharp.dll`.
- Audited the new Trade IL against `1.0.3.577`; the authoritative price, transaction and vanilla Trade-window paths used by Item Intelligence remain unchanged.
- Added a dedicated Trade-only compatibility gate for the new Assembly SHA instead of broadening unrelated exact features.
- Restored Modder Mode ship-cargo item spawning on the hotfix build through a separate cargo-only compatibility gate.
- Cargo mutation still revalidates the exact vanilla `MagnumCargoSystem.AddCargo` signature before use.
- Preserved the v1.7.41.2 Station Production / Recipes cleanup and Previous Trade Layout improvements.

Stable runtime marker:

`[ItemIntelligence] ACTIVE VERSION 1.7.41.3 (StableRelease17413).`

Validated runtime game build: Quasimorph `1.0.3.578s.024ad60`.

Validated hotfix Assembly-CSharp SHA-256 for Trade and cargo-spawn feature gates:

`A38C4D993C9BF60D0DDE0EDD348F201C97574F907808417A33C8A20F4772E9C1`

Compatibility remains feature-scoped: recognizing this SHA for Trade/cargo does not automatically certify unrelated exact Loot or Scavenger calculations.

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