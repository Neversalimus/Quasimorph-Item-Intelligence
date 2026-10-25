# Item Intelligence 1.7.43.2-test4

For Quasimorph 1.0.4.590s.969afc6. DEV validation build; includes the mode, installed-limb and genome-upgrade fixes from the preceding build.

- Fix the first browser hotkey on the space screen when no item has been hovered. An explicit hotkey now opens the catalog with no selected item. An observed item still opens its card. The setting applies to F2, F7 or another configured binding.
- Support the same catalog entry when a save resumes directly in a mission, without SpaceStarted. Gameplay lifecycle hooks enable the request; the main menu remains idle. The opening-frame input guard recognizes catalog requests, and Escape closes a catalog-only browser. Item-only actions remain disabled until an item is selected.
- Match ReloadLauncherSystem's reload rules exactly. A null AllowedGrenadeIds permits every GrenadeRecord; an empty list permits none. DefaultGrenadeId supplies the initial payload and no longer overrides reload restrictions. Both directions of ammunition links use the same result.
- Shaker's 11 entries in the report are correct: `frag_grenade`, `quasi_grenade`, `thermal_grenade`, `stun_grenade`, `smoke_grenade`, `toxic_grenade`, `toxic_gas_grenade`, `frost_grenade`, `frost_gas_grenade`, `urparp_grenade` and `hellfire_grenade`. Makeshift grenades and other items outside that list are excluded. Recipe checkmarks/crosses indicate production unlocks, not ammunition compatibility.
- The built-in Ur launcher has an empty reload list in the installed game. Its starting Gibil payload still determines the default mode's damage type, but is not advertised as reloadable ammunition.

Validation: production browser request/capture tests cover fresh space, direct mission resume, custom F7, explicit/stale item targets, unavailable data, compatibility gates and 1,000 idle menu ticks. Production weapon tests cover null versus empty allowlists, default payload exclusion, both link directions and rebuilds. The expanded local table audit includes compound item components (the game's ParseId strips the leading asterisk): 325 weapons, 531 ammo-backed modes and 23 linked augmentations. Earlier static-table counts excluded those components; the runtime indexing already included them.

The user's manual checks passed for the preceding build. This build still needs the first-open catalog interaction checked in the live game. Compilation and automated fixtures do not substitute for Unity interaction testing.

The Publicizer screenshot is an optimization suggestion, not evidence of a functional defect. The current reflection layer already caches member lookups and misses; service discovery uses cached types and bounded retries. This change does not introduce a new build dependency or claim an unmeasured FPS improvement.
