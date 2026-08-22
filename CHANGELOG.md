# Changelog

## v1.7.41.3 — 2026-08-22

- Restored exact Trade prices on Quasimorph `1.0.3.578s.024ad60`.
- Audited critical vanilla Trade IL against the previous `1.0.3.577` build and confirmed the Item Intelligence price/transaction contracts remain unchanged.
- Added a dedicated Trade-only compatibility gate for Assembly SHA `A38C4D993C9BF60D0DDE0EDD348F201C97574F907808417A33C8A20F4772E9C1`.
- Restored Modder Mode ship-cargo item creation on the hotfix build using a separate cargo-only gate plus exact runtime `MagnumCargoSystem.AddCargo` signature validation.
- Kept the new SHA feature-scoped instead of globally certifying unrelated exact domains.
- Includes the v1.7.41.2 Station Production / Recipes reclassification, disassembly-first ordering, Previous Trade Layout hardening and Trade-layout diagnostics.
- Stable runtime marker: `1.7.41.3 (StableRelease17413)`.

## v1.7.41.2 — 2026-08-22

- Removed station-production recipe data from the Trade tab and moved it to Recipes.
- Verified the live vanilla Station Production path through `Station.CurrentReceipts`, `Data.BarterReceipts`, station storage and `ItemProductionSystem.StartStationItemProduction`.
- Ordered canonical disassembly before Station Production.
- Decoupled the `Previous Trade Layout` presentation preference from the exact-version/SHA pricing gate.
- Added deduplicated Trade-layout diagnostics.

## v1.7.41.1 — 2026-08-22

- Added Quasimorph 1.0.3 stock-sensitive Trade pricing presentation.
- Added exact batch totals and first-to-last unit price movement for station transactions.
- Added the new two-line Trade station-card layout as default.
- Added an MCM option to restore the previous compact Trade table without reverting 1.0.3 pricing math.
- Fixed Modder Mode ship-cargo item creation on Quasimorph 1.0.3 using the audited cargo API.

## v1.7.39 — 2026-08-17

- Fixed technology-level resolution used by Search, Catalog and Modder data.
- Fixed stale / missing Magnum required and owned counts on first tab entry.
- Added exact Damage/AP information to supported firearm and melee modes.
- Aligned Trade consumer visibility and current availability checks with verified vanilla contracts.