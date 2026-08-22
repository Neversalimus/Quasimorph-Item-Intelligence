# Changelog

## v1.7.41.1 — 2026-08-22

- Added Quasimorph 1.0.3 stock-sensitive Trade pricing presentation.
- Added exact batch totals and first-to-last unit price movement for station transactions.
- Added the new two-line Trade station-card layout as default.
- Added an MCM option to restore the previous compact Trade table without reverting 1.0.3 pricing math.
- Clarified station stock as `IN STOCK / ОСТАТОК` and separated Mission from Travel information.
- Kept station consumer discovery aligned with the verified vanilla `Station.ConsumableItems.ContainsKey(itemId)` contract.
- Fixed Modder Mode ship-cargo item creation on Quasimorph 1.0.3 using the audited cargo API.
- Retained mission clone inventory item creation under explicit Modder Mode only.
- Shortened oversized player-facing Loot / MCM / Baron / container explanations and added text-safety budgets.
- Improved container chance formatting to localized `≈ min–max%` ranges and updated container subtype resolution.
- Preserved feature-owned compatibility gates, architecture budgets, localization parity and fail-closed exact calculations.
- Stable runtime marker: `1.7.41.1 (StableRelease17411)`.
- Runtime-tested on Quasimorph `1.0.3.577s.887ffe7` / Assembly-CSharp SHA-256 `FE68E4355D4ED9CBAB7F8B1BA7717DBC1CC3FD749D0D11A644A9A3DB5EAB478F`.

## v1.7.39 — 2026-08-17

- Fixed technology-level resolution used by Search, Catalog and Modder data.
- Fixed stale / missing Magnum required and owned counts on first tab entry.
- Added exact Damage/AP information to supported firearm and melee modes.
- Aligned Trade consumer visibility and current availability checks with verified vanilla contracts.
- Preserved fail-closed starmap travel safety and lazy/time-sliced indexing.
- Stable runtime marker: `1.7.39 (StableRelease1739)`.