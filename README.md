# Quasimorph Item Intelligence

**Item Intelligence** is an in-game item browser and reference tool for **Quasimorph**.

Current stable version: **v1.7.42**

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

## v1.7.42

Trade-layout UX and reliability update.

- Added a compact **Cards / Table** switch directly to the Trade tab.
- Layout changes apply immediately without opening MCM, closing Item Intelligence or reloading the tab.
- The selected layout is persisted directly to the existing configuration key and survives reopening the browser / restarting the game.
- Removed the old player-facing `Previous Trade Layout` MCM toggle; the persistent key remains for backwards-compatible configuration storage.
- Added clear active-state feedback and localized `VIEW / ВИД` presentation.
- Preserved exact Quasimorph `1.0.3.578s.024ad60` Trade pricing and station-consumer contracts.
- Verified repeated `Cards -> Table -> Cards` switching with `persisted=True` and `Exact103Pricing=True`.
- Published Steam payload and GitHub release asset use the exact same gate-approved DLL.

Stable runtime marker:

`[ItemIntelligence] ACTIVE VERSION 1.7.42 (StableRelease1742).`

Validated runtime game build: Quasimorph `1.0.3.578s.024ad60`.

Validated Assembly-CSharp SHA-256:

`A38C4D993C9BF60D0DDE0EDD348F201C97574F907808417A33C8A20F4772E9C1`

Gate-approved / published Item Intelligence DLL SHA-256:

`B7D441375169074B4E499A473B3C169FC253777D8BE3D793D7517B243116C6CE`

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