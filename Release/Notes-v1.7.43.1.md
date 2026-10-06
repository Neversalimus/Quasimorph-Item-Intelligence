Item Intelligence 1.7.43.1 adds exact damage-type information to the existing weapon-mode hover tooltips.

- Melee attack modes now show their actual damage type.
- Ranged fire modes show the damage type of the same standard/override ammunition used by the existing standard Damage/AP calculation.
- Mode-to-ammo resolution preserves Quasimorph's positional Firemodes / OverrideAmmo mapping; empty override slots fall back to DefaultAmmo without shifting later modes.
- Damage types are never guessed from mode or ammunition names. Unknown combinations fail closed.
- The tooltip value column is wider so longer localized damage-type names remain readable.
- Added complete localization for all eight Item Intelligence interface languages.
- Retains native ALT / More details support and the audited Quasimorph 1.0.4.590 compatibility from 1.7.43.0.

Public Workshop: https://steamcommunity.com/sharedfiles/filedetails/?id=3780078201
