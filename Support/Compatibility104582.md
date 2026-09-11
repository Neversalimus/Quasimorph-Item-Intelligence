# Quasimorph 1.0.4.582 — compatibility review

Reviewed 2026-09-11 for **1.7.42.6-test2 / Compat104582Test02**. Source base: `23df60564fafb32c6502e591f97cbd24053219d0` (eight-language DEV candidate). The runtime log supplied with this patch instead runs stable 1.7.42.5.

Game: **1.0.4.582s.c4335c5**, Steam build **25253182**.
Assembly-CSharp SHA256: `9D0C784A764D75EC6616BD3B5744C0E581BB1CE148FD175BD8CABA8195854006`.
All 120 manifest entries in the supplied archive passed length/hash validation. Of 118 Managed DLLs, only Assembly-CSharp differs from the previously reviewed 1.0.4.581 set.

## Findings and changes

| Area | Evidence | Candidate behavior |
| --- | --- | --- |
| Trade | All 343 methods of the selected trade, station, price and progression types and their nested types are unchanged. The supplied log has available price/state data but `Exact103Pricing=False`. | Restore exact station prices and batch calculations for this fingerprint. Existing runtime checks remain. |
| Loot, source families and cargo | All 60 previously reviewed methods retain their algorithms. Implant extraction bonus and its generated lambda change compiler ordinals only. | Restore the independent loot-modifier, container-estimate, Scavenger, source-family and cargo gates. Keep the 1.0.4 RandomStart pool policy. |
| Body/implants | All 14 ordinary body generation, selection and installation methods in the prior review retain their algorithms. Record layouts used by the model are unchanged. | Restore the separate body-model gate. Preserve failed-installation weights, socket competition and Conditional handling of random augmentations. |
| Fire-mode scatter | The vanilla tooltip now adds `GetAugmentScatterAngleMult()` to the perk multiplier and clamps the result to zero. The new getter includes only augmentation effects. | Read the game's augmentation term through a cached optional method lookup. Keep the original formula on older DLLs. Hide the value if the new API is incompatible or unavailable at runtime. |

The updated tooltip formula is:

```text
max((mode scatter + weapon bonus) * (perk multiplier + augmentation term), 0)
```

The inspected weapon supplies the record. No mercenary means a neutral multiplier of 1. The change follows the fire-mode tooltip; it does not promise the complete combat calculation with cover and every temporary effect. No gameplay objects are spawned or mutated by this calculation.

The raw comparison covered 3,821 types and 27,539 / 27,548 methods. It found 40 changed common methods, of which 28 differ only in compiler-generated identifiers after separate review. Other game changes concern instant perk/stun handling, AP chains, movement accounting and related tooltips. The checked QII code does not reference the removed `WarCrimeProceed` APIs. `Compatibility104582.json` records the compared method fingerprints and scope; it contains no decompiled method bodies or game DLLs.

## Verification and limits

- All 112 production C# files compile as C# 5 against both the 1.0.4.582 and 1.0.4.581 dependency sets: zero errors and warnings.
- All static build contracts pass. The scatter owner budget changes from 190 to 225 lines to accommodate the optional API adapter; its field ownership and existing lookup restrictions remain checked.
- 66,052 assertions pass: behavior 693, body/implants 4,939, scatter 48, release 32, localization 5,385, MCM fonts 38, viewport 54,917.
- Scatter fixtures execute the complete production owner with the new API, without it, and with an incompatible return type. They cover additive effects, live changes, zero clamping, neutral context, wrong/missing records, exceptions and non-finite values.
- This environment does not run Quasimorph/Unity. Fresh in-game acceptance of this candidate is **pending**. Live asset tables still require the existing data checks. ES/PT-BR/FR game acceptance and native-speaker review remain pending.
- The global VERIFIED build fingerprint is deliberately unchanged. Adding this patch to independently audited features does not certify the whole game or future patches.

The supplied log ends with NullReferenceException stacks in game `OnDestroy`/`OnDisable` callbacks, followed by engine shutdown. Those stacks contain no ItemIntelligence frame; this alone does not establish which component caused teardown ordering. An isolated 59.6 ms cold F2 open, an unmapped AztecAltar profile and a missing strict icon for efw_aec-beta are recorded, without attributing them to this patch.

## Game check

Follow `INSTRUCTIONS_RU.md`. Confirm the candidate startup marker, then `Exact103Pricing=True` on this assembly. Compare station prices and a multi-unit total, the same equipped weapon's fire-mode tooltip, Loot modifiers/body sources and persisted MCM/window preferences. Keep DEV item **3781927679** for this check. Public item **3780078201** is not published by this package.
