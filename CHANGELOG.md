# Changelog

## v1.7.42.1 — 2026-08-24

- Restored Marauder I-IV, Organization and Field Medic exact Loot projections on Quasimorph `1.0.3.578s.024ad60`.
- Restored save-aware container chance estimates and exact Scavengers / Purge Brigade mission chance presentation.
- Restored the separately audited hardcoded story acquisition source family and random starting equipment pools.
- Added independent A38 compatibility ownership for Loot modifiers, container estimates, Scavengers and source-family paths without promoting A38 into the broad `IsAuditedFeatureAssembly` gate.
- Current-game exact IL audits passed `7 PASS / 0 FAIL` for Loot/Scavenger paths and `10 PASS / 0 FAIL` for source-family paths.
- Runtime acceptance confirmed `hardcodedCurrentBuild=enabled`, Marauder I-IV, Organization, Field Medic, `ContainerSaveEstimate`, `ScavengersExact`, `Exact103Pricing=True`, `partialFailures=0`, disassembly `symmetry=OK` and ammo `falseAmmoLinks=0`.
- Pre-release gate v1.2 passed `13 PASS / 0 WARN / 0 BLOCK`.
- Steam Workshop download verification confirmed all 7 public payload files are byte-identical to the frozen Approved payload.
- Gate-approved / published DLL SHA-256: `FEFD4FD75A1BB13DE022BFC80E16A9D3773EAED86C2CA54768CACF71307CEFD0`.
- Stable runtime marker: `1.7.42.1 (StableRelease17421)`.

## v1.7.42 — 2026-08-23

- Added an immediate Cards / Table switch directly inside the Trade tab.
- Persisted Trade layout directly to config so the UI no longer depends on MCM binding state.
- Removed the old player-facing Previous Trade Layout MCM toggle while retaining its persistent compatibility key.
- Added localized `VIEW / ВИД` controls with active-state feedback and icon/text fallback.
- Preserved exact Quasimorph `1.0.3.578s.024ad60` pricing and station-consumer logic while switching layouts.
- Runtime acceptance confirmed repeated direct switching with `persisted=True` and `Exact103Pricing=True`.
- Release pipeline verified that Steam Workshop and GitHub release asset contain the exact gate-approved DLL `B7D441375169074B4E499A473B3C169FC253777D8BE3D793D7517B243116C6CE`.
- Stable runtime marker: `1.7.42 (StableRelease1742)`.

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