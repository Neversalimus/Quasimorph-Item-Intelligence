# Changelog

## v1.7.42.6 — 2026-09-11

- Restore independently audited compatibility with Quasimorph 1.0.4.582s.c4335c5, including exact Trade prices/batches, loot calculations, source families, cargo and ordinary enemy body/implant paths.
- Include the updated fire-mode scatter formula with augmentation effects, zero clamping and old-API fallback.
- Add Spanish, Brazilian Portuguese and French (657 values each); all eight UI languages include MCM selection, numeric formatting and mixed-script support.
- Fit long catalog launcher/category/filter labels within their existing buttons.
- Accept test2 game evidence and record a complete ES/PT-BR/FR text review, preserving the known MCM/performance observations and untested scenarios.
- Promote stable source for the existing public Workshop item and retain the frozen, resumable GitHub release workflow.

## v1.7.42.6-test2 — 2026-09-11

- Restored feature-owned compatibility with Quasimorph 1.0.4.582s.c4335c5: exact Trade prices/batches, supported loot modifiers and container estimates, Scavenger rewards, source families, cargo creation and ordinary enemy implant/body calculations.
- Updated fire-mode scatter to include the game's augmentation multiplier and zero clamp; older game DLLs retain their original formula through a cached optional API adapter.
- Kept unknown-build and incomplete-data safeguards, all eight UI languages, large-window/zoom controls and Chinese MCM font isolation.
- Added regressions for the new fingerprint and scatter behavior. Full C# 5 compilation and all suites pass; new-patch Unity acceptance is pending.

## v1.7.42.6-test1 — 2026-09-11

- Add Spanish, Brazilian Portuguese and French: 657 translated keys per language, 1971 new values.
- Add Español, Português (Brasil) and Français to MCM after the existing options; support Auto using the real Spanish / BrazilianPortugal / French game identifiers.
- Localize decimal, percent and compact time formatting; retain game-language item names and private mixed-script font ownership.
- Extend localization regression coverage to 5385 assertions, including every shipped key, aliases, switching, force/fallback behavior and Chinese game names with Latin UI.
- Prepare a DEV-only source package and Workshop description drafts. In-game acceptance and native-speaker review of the new languages remain pending.

## v1.7.42.5 — 2026-09-10

- Add German, Polish and Simplified Chinese UI and a separate MCM language selector; keep game-provided names and search in the game's language.
- Add a large browser view covering 94% of the screen, 100–200% zoom and independent saved preferences for normal and large views.
- Adapt columns, row counts, catalog, notes, links and hover-card placement to the available screen space.
- Correct enemy implant candidate selection for body slots, tissue compatibility, preset implants and socket competition. Mark random-body outcomes Conditional and keep amputation recovery chances separate.
- Fix missing Chinese MCM captions after restart, including mixed-language dropdown options, using private fallback fonts scoped to QII.
- Fit long header buttons and use the compact All category caption.
- Retain the corrected GitHub draft discovery and resumable frozen-release workflow.
- Promote Test5 production code with only the runtime version/marker changed. Record scoped game acceptance from Test3–Test5; preserve feature-specific compatibility guards and probability limits.

## v1.7.42.5-test4 — 2026-09-10

- Scale the open-item label rectangle with the browser columns so its label stays next to the action icon in large view.
- Record partial Test3 game acceptance from 13 screenshots and the supplied log at 2560×1440, with scoped performance and external UI findings.
- Carry forward the large window/zoom, body-aware implant sources and five-language DEV candidate.

## v1.7.42.4 — 2026-09-08

- Promote the accepted Test3 gameplay code to stable; runtime marker `1.7.42.4 (StableRelease17424)`.
- Fix missing amputation source data and recipe identity collisions; restore independently reviewed 1.0.4 Loot, Scavenger, start/story and cargo API support.
- Record Test3 acceptance in Space and Dungeon, RU/EN switching and two amputation index builds with 236 slots / 120 items.
- Retain feature-specific compatibility guards and documented probability limits; no global VERIFIED promotion.
- Prepare the existing public Workshop item through the stable installer and frozen release workflow. Final Windows payload hashes are captured during preparation.
- Preserve the 660 production behavior assertions and 26 release assertions. Three isolated runtime performance-budget overruns are recorded in the acceptance evidence.

## v1.7.42.4-test3 — 2026-09-08

- Test2 campaign logs confirm perk values, container estimates, Scavengers, source-family activation and exact Trade contracts; record the observed scope in `Support/RuntimeAcceptanceTest2.json`.
- Fix the empty amputation index: vanilla `AmputatedDrop` is a list of weight/ID tuples, not a dictionary. Sum duplicate outcomes and reject invalid intervals without publishing partial probabilities.
- Clearly label base amputation chances and disclose the guaranteed-augmentation upgrade exclusion; installed implant recovery remains separate.
- Add coverage through the production indexer and two mutation checks. 660 behavior assertions, 26 release assertions and 101-file C# 5 compilation pass.
- Add `[AmputationSources]` index diagnostics. The later Test3 game log confirmed index recovery; see the stable entry above.

## v1.7.42.4-test2 — 2026-09-08

- Reviewed current game 1.0.4.581s.2952480 from the supplied 118 Managed DLLs; recorded evidence in `Support/Compatibility104.json`.
- Restored independently audited Loot modifier, container-estimate, Scavenger, source-family and direct cargo API paths for exact game SHA `BE780...`, retaining runtime data guards and global unverified status.
- Corrected random-start tables: 1.0.4 uses `RandomStart_rewardEquipment` / `RandomStart_rewardConsumables`; prior audited builds keep `General_*`.
- Added behavioral coverage for version boundaries, source table selection, container probability composition and Scavenger candidate multiplicity. Five deliberate mutations are detected.
- Clarified that container estimates exclude location events as well as budget and temporary Tech.
- Added a runtime perk-table contract diagnostic; removed repeated automatic game-DLL collection from the DEV installer.
- Full 100-file C# 5 compilation against current game dependencies passes; campaign/mission acceptance remains pending.

## v1.7.42.4-test1 — 2026-09-08

- Recipe consolidation now includes canonical item identity, incorporating the prepared same-name recipe fix.
- Loot notes distinguish compatibility limitations from missing save data and never direct players to a blocked manual control.
- Extracted deterministic probability and availability helpers; probability roll counts no longer overflow an integer on extreme finite inputs.
- Replaced duplicated PowerShell math checks with execution of production C# methods and independent expected outcomes.
- Added mutation sensitivity checks for probability, recipe identity and unavailable-action guidance.
- Release preparation freezes the complete staged file set and tracked source hashes. Publishing uses atomic leased Git updates, resumable draft/asset uploads and downloaded asset verification.
- DEV installer also collects current game DLL metadata for the outstanding compatibility review, without executing it or including campaign saves.
- No additional game SHA has been enabled. Runtime acceptance on the current game remains required.

## v1.7.42.2 — 2026-09-07

- Restored exact Trade pricing on Quasimorph `1.0.4.581s.2952480`.
- Re-audited the vanilla Trade pricing structure and runtime contract against Assembly SHA `BE78036434737521BB43DABBD934B579A08DC3E8370B834AEE36E40932F56FE0`.
- Added a dedicated Trade-only 1.0.4 compatibility fingerprint; unrelated Loot, Scavenger, cargo and global exactness gates remain fail-closed.
- Runtime acceptance confirmed `Exact103Pricing=True`, 175 runtime stations, exact mission/station links, and numeric Trade rows.
- Vanilla transaction check: row price `259` with `+50%` surcharge produces final cost `388`, matching Item Intelligence.
- Stable runtime marker: `1.7.42.2 (StableRelease17422)`.
- Gate-approved DLL SHA-256: `6E1E7DFD642CA5A5F735CC51BDE68CEB7E9A0D52D567681902C850682E5699D9`.
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
