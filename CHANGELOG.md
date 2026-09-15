# Changelog

## v1.7.42.8 — 2026-09-15

- Added optional Verbose logging in MCM and config.ini, disabled by default.
- Normal mode keeps Player.log concise while retaining important warnings, errors and a small set of lifecycle/status messages.
- Detailed UI, index, navigation, performance and audit diagnostics are now written only when verbose logging is enabled.
- Added and persisted the new setting in all eight interface languages.
## v1.7.42.7 — 2026-09-14

- Enlarged item IDs and corrected their double tinting, including hover and pressed colors.
- Enlarged search hints and entered text, removed italic search hints and improved their contrast.
- Enlarged and brightened keyboard hints, using two lines in all eight interface languages.
- Adjusted label rectangles to fit the text while retaining the existing content-row capacity and zoom controls.
- Added optional Enhanced readability in MCM for brighter secondary labels and larger tabs and notes, localized in all eight interface languages.
- Fixed clipped station-production explanations and replaced an unsupported check-mark glyph in the Loot legend with localized text.
- Greatly reduced Trade scan overhead by resolving price methods once and reading the current space interface directly for travel checks. Current prices, stock and batch calculations remain unchanged.
- Improved GitHub release recovery for drafts that are not yet visible in the release list.

## v1.7.42.6 — 2026-09-11

- Restored compatibility with Quasimorph 1.0.4.582s.c4335c5 for exact Trade prices and batches, loot calculations, item sources, cargo creation and ordinary enemy body/implant calculations.
- Updated fire-mode scatter to include augmentation effects and the game's zero clamp, retaining the previous formula for older supported APIs.
- Added Spanish, Brazilian Portuguese and French, bringing the interface to eight languages with MCM selection and localized number formatting.
- Improved fitting of long catalog buttons, categories and filter captions.

## v1.7.42.5 — 2026-09-10

- Added German, Polish and Simplified Chinese with a separate MCM language selector. Game-provided names and search continue to use the game's language.
- Added a large browser view covering 94% of the screen, 100–200% zoom and separate saved preferences for normal and large views.
- Adapted columns, scrolling, catalog, notes, item links and hover-card placement to the available screen space.
- Corrected enemy implant sources for body slots, tissue compatibility, preset implants and socket competition. Sources affected by random body changes are marked Conditional; amputation recovery chances remain separate.
- Fixed missing Chinese MCM captions and language-selector text after restarting the game.
- Improved fitting of long header buttons and the All category caption.

## v1.7.42.4 — 2026-09-08

- Fixed missing amputation sources and combined repeated drop weights. Base amputation chances explain the guaranteed-augmentation upgrade exclusion; installed implant recovery remains separate.
- Fixed recipe rows merging unrelated items with the same display name, preserving related variants and independent chip unlocks.
- Restored supported Loot modifiers, container estimates, Scavengers rewards, story sources and Modder Mode cargo creation on Quasimorph 1.0.4.581s.2952480.
- Updated random starting equipment sources to the game's new tables.
- Improved unavailable-calculation explanations and clarified the limits of container estimates.
- Prevented probability roll-count overflow on extreme finite inputs.

## v1.7.42.2 — 2026-09-07

- Restored exact Trade prices, batch totals and stock-sensitive next-unit prices on Quasimorph 1.0.4.581s.2952480.
- Kept compatibility checks independent for other numerical features.

## v1.7.42.1 — 2026-08-24

- Restored Marauder I–IV, Organization and Field Medic loot calculations on Quasimorph 1.0.3.578s.024ad60.
- Restored save-aware container estimates and Scavengers / Purge Brigade mission chances.
- Restored supported story acquisition sources and random starting equipment pools.

## v1.7.42 — 2026-08-23

- Added a Cards / Table switch directly inside the Trade tab, with saved layout preferences and localized controls.
- Removed the old Previous Trade Layout setting from MCM.
- Kept current prices and station availability consistent when switching layouts.

## v1.7.41.3 — 2026-08-22

- Restored exact Trade prices and Modder Mode ship-cargo item creation on Quasimorph 1.0.3.578s.024ad60.
- Retained the station-production and Trade layout improvements from v1.7.41.2.

## v1.7.41.2 — 2026-08-22

- Moved station-production recipes from Trade to Recipes.
- Ordered canonical disassembly before station production.
- Made the Previous Trade Layout preference independent of numerical compatibility checks.

## v1.7.41.1 — 2026-08-22

- Added Quasimorph 1.0.3 stock-sensitive Trade prices, exact batch totals and first-to-last unit price movement.
- Added a two-line station-card layout and an MCM option for the previous compact table.
- Fixed Modder Mode ship-cargo item creation on Quasimorph 1.0.3.

## v1.7.39 — 2026-08-17

- Fixed technology-level resolution used by Search, Catalog and Modder data.
- Fixed stale or missing Magnum required and owned counts on first tab entry.
- Added exact Damage/AP information to supported firearm and melee modes.
- Corrected Trade consumer visibility and current availability checks.

