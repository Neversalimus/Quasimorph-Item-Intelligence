# Item Intelligence 1.7.43.2-test3

For Quasimorph 1.0.4.590s.969afc6. This is a DEV validation build.

- Grenade launchers, including Shaker and the built-in Ur launcher, link to their configured grenade items. Their unused rifle-ammo placeholders no longer create ammunition links or supply damage types, traits or nominal Damage/AP. Default damage types follow the configured grenade explosion. Attack modes remain inspectable when Damage/AP is unavailable.
- `none` in a positional ammo override means to use the default ammo for that mode. This restores the first attack's damage type for eight weapons, including `venus_axe_1`, `venus_poleaxe_1`, `prison_axe_1` and Reaper (`scrivnus_blade_1`). Empty override slots keep their positions.
- Mode tooltips show each mode's ammo traits and status effects. Reaper's slash shows Wounding; its stab shows no ammo traits. Common weapon traits are not mislabeled as mode-specific ammo traits. Translations use the game's trait and status keys.
- Augmentations link to weapons supplied by their installed body slots, and weapons link back to all matching augmentation items. The relation follows `AugmentationRecord.WoundSlotIds -> WoundSlotRecord.BareHandWeapon`, supports multiple items and slots, and is shown directly in Overview. It does not infer a relation from an item name. The current game tables contain 20 linked augmentation items.
- Mode tooltips keep all twelve supported rows at smaller window sizes and higher zoom. Row spacing adapts to the available height.
- Includes the separate Genome Upgrade Uses change: configured genome-upgrade materials have a separate Magnum section. Ship research quantities and remaining totals keep their existing calculations.

Validation uses production algorithms with game-service fixtures, a complete audit against locally extracted installed-game tables, the repository's existing regression suites, and compilation against the installed game's assemblies. A live game session is still needed to verify appearance and interaction. No public release or Workshop upload is performed by the installer.
