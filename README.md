# Quasimorph Item Intelligence

**Item Intelligence** is an in-game item browser and reference tool for **Quasimorph**.

Current stable version: **v1.7.39**

Steam Workshop: https://steamcommunity.com/sharedfiles/filedetails/?id=3780078201

## Features

- Fast item inspector opened with **F2**.
- Search, catalog, favorites, history and advanced filters.
- Recipes and production relationships.
- Magnum project requirements and item usage.
- Trade information with station navigation to the starmap.
- Loot sources, container data, enemy/faction sources and loot modifiers.
- Faction technology information.
- Weapon/ammo relationships and detailed fire-mode tooltips.
- Exact **Damage/AP** display where vanilla data provides enough information; no invented approximation when it does not.
- Disassembly relationships, chip/data unlock information and modding-oriented item IDs.
- English and Russian localization.
- Read-only design: Item Intelligence is intended to inspect game data, not mutate inventory, saves or progression.

## v1.7.39

This release consolidates the final 1.7.39 test lineage into a stable build.

Highlights include:

- corrected technology-level resolution used by Search/Catalog/Modder information;
- Magnum count refresh fixes so required/owned values are available without tab-juggling;
- exact firearm and melee Damage/AP presentation in weapon-mode tooltips;
- trade station consumer / availability logic aligned with verified vanilla contracts;
- melee Damage/AP dropdown recovery and UI hardening;
- continued starmap travel safety, lazy indexing and main-menu/runtime cleanup.

Validated stable runtime marker:

`[ItemIntelligence] ACTIVE VERSION 1.7.39 (StableRelease1739).`

Validated against Quasimorph `1.0.2.573s.9f33900`.

## Installation

### Steam Workshop

The recommended installation method is the Steam Workshop item linked above.

### Manual

Download the stable ZIP from **GitHub Releases** and preserve its `ItemIntelligence` folder structure when installing it with your Quasimorph mod setup.

## Source

The repository contains the stable source lineage used for the release. Compiled binaries are intentionally kept out of Git history and are distributed through GitHub Releases / Steam Workshop.

## Compatibility / safety

The stable runtime reports verified Item Intelligence compatibility contracts for Core, Search/Catalog, Magnum, Recipes, Trade, Ammo, Chip Chance, Disassembly, Factions, Loot, Tooltip and Input on the validated game build.

Item Intelligence remains a third-party mod and is not affiliated with Magnum Scriptum.