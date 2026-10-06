Item Intelligence 1.7.43.0 adds native ALT / More details support inside the Item Intelligence browser.

- Hovering a QII item now supports the same vanilla ALT / More details view used by Quasimorph.
- The implementation reuses the game's own `UI_TooltipMore` action and `TooltipFactory` additional-tooltip path; Item Intelligence does not clone or patch the detail builders.
- The modal guard allows only the tooltip-more action while a QII-owned native item tooltip is active, so gameplay/UI actions behind the browser remain blocked.
- A dedicated compatibility gate disables only ALT details if the vanilla additional-tooltip API changes, leaving ordinary QII tooltips and the rest of the browser available.
- Retains the audited Quasimorph 1.0.4.590 compatibility restored in 1.7.42.9.

Public Workshop: https://steamcommunity.com/sharedfiles/filedetails/?id=3780078201
