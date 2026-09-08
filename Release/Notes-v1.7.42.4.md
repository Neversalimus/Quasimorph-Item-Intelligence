Item Intelligence 1.7.42.4 restores item-source information for Quasimorph 1.0.4 and fixes missing amputation drops.

- Fixed missing amputation sources and repeated drop weights. The chance column now explicitly shows base chances and explains the guaranteed-augmentation upgrade exclusion.
- Fixed recipe rows merging unrelated items with the same display name, while preserving related variants and independent chip unlocks.
- Restored supported Loot modifiers, container estimates, Scavengers rewards, story sources and the direct cargo-creation path in Modder Mode for the reviewed 1.0.4 build.
- Updated random starting equipment sources to the game's new tables.
- Improved unavailable-calculation explanations and clarified the limits of container estimates.

The accepted Test3 code was promoted with only the runtime version and release marker changed. Its game log confirms Space and Dungeon operation, RU/EN switching and an amputation index containing 120 items. Automated coverage exercises 660 production behavior assertions and 26 release assertions.

Reviewed game: Quasimorph 1.0.4.581s.2952480. Compatibility remains checked separately for each feature; unknown builds and unsupported runtime data do not inherit these approvals. See the repository's FIX_REPORT_RU.md and Support/RuntimeAcceptanceTest3.json for validation scope.
