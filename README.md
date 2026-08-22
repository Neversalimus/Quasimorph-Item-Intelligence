# Quasimorph Item Intelligence

**Item Intelligence** is an in-game item browser and reference tool for **Quasimorph**.

Current stable version: **v1.7.41.1**

Steam Workshop: https://steamcommunity.com/sharedfiles/filedetails/?id=3780078201

## Features

- Fast item inspector opened with **F2**.
- Search, catalog, favorites, history and advanced filters.
- Smart item overview, recipes, production relationships and Magnum requirements.
- Trade information with direct station navigation to the starmap.
- Quasimorph 1.0.3 stock-sensitive trade pricing, including first-to-last unit price movement and exact batch totals.
- Two Trade layouts: the new station-card design by default, with the previous compact table available from MCM.
- Loot sources grouped by containers, general placement, enemies, faction rewards, mission pools and special sources.
- Container drop-chance estimates based on verified weighted pools, roll counts, Tech context and supported loot modifiers.
- Manual loot-modifier calculator for Marauder / Organization / Field Medic contexts.
- Faction technology information.
- Weapon/ammo relationships and detailed fire-mode tooltips, including Damage/AP and Critical Damage/AP where the vanilla data contract is provable.
- Disassembly relationships and chip/data unlock information.
- English and Russian localization.
- Optional **Modder Mode**, including audited item creation for ship cargo and mission clone inventory. Ordinary mode remains inspection-only.

## v1.7.41.1

v1.7.41.1 is the stable Quasimorph 1.0.3 compatibility / Trade / Loot polish release.

Highlights:

- adapted Trade presentation to Quasimorph 1.0.3 per-unit stock-dependent repricing;
- added exact batch totals and first-to-last unit price movement instead of presenting one misleading static price;
- added a new readable two-line station card and an MCM option to restore the previous compact Trade layout;
- clarified station stock as **IN STOCK / ОСТАТОК** and kept mission and travel information separate;
- fixed station consumer presentation to stay aligned with the verified vanilla `Station.ConsumableItems` contract;
- fixed Modder Mode item creation on Quasimorph 1.0.3 using the audited cargo API rather than relying on an unavailable console command;
- polished and shortened long player-facing explanations, with text-safety contracts to prevent oversized explanatory strings from returning;
- improved Loot/container chance presentation, including localized `≈ min–max%` formatting and current container subtype resolution;
- retained feature-owned compatibility gates, architecture budgets, localization parity and fail-closed exact calculations.

Stable runtime marker:

`[ItemIntelligence] ACTIVE VERSION 1.7.41.1 (StableRelease17411).`

Validated runtime game build: Quasimorph `1.0.3.577s.887ffe7`.

Validated Quasimorph 1.0.3 Assembly-CSharp SHA-256:

`FE68E4355D4ED9CBAB7F8B1BA7717DBC1CC3FD749D0D11A644A9A3DB5EAB478F`

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

Exact numerical claims are guarded by feature-owned compatibility contracts. When a future game build cannot be verified, affected exact calculations are designed to fail closed rather than silently present unsupported numbers.

Modder Mode intentionally exposes save-mutating item creation actions and is disabled unless the player explicitly enables it.

Item Intelligence is a third-party mod and is not affiliated with Magnum Scriptum.