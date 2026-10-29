# Item Intelligence 1.7.43.4-test4

- Added spoilage results to Overview and Recipes. Click the result to inspect it.
- Added the reverse relation: a resource now lists the items that can spoil into it. This covers perishable food and quasimorph equipment.
- Added shelf lives for fresh items and a note that refrigeration pauses spoilage. Each step of a spoilage chain is shown separately.
- Translated the new labels into all 11 supported languages.

This is a development build that also contains the browser-state and catalog improvements from earlier 1.7.43.4 test builds.

## Validation

The DLL compiles against the installed Quasimorph build. Automated behavior checks cover both relation directions, chains, missing data, composite item records, clickable rows, localized ordering and session resets. A separate audit using the installed game's record types and parsers matches all 34 spoilage rules and their 21 distinct results.

The new pages still need an in-game visual check before stable promotion.

## In-game check

1. Search for `human_meat`. Overview and Recipes should link to `rotten_human_meat`, with **48 h** for a fresh item.
2. Open `rotten_human_meat`. It should list `human_meat` (48 h), `roasted_human_meat` (96 h), `human_organ_1` (72 h) and `throwable_heart` (48 h).
3. Open `quasi_medical_kit_1`: it becomes `human_organ_1` after 504 h. On the organ page, the next step is `rotten_human_meat` after 72 h. The kit should not appear as a direct source of rotten meat.
4. Open `quasi_bolts_ammo`: it becomes `broken_weapon` after 168 h. Check that both links open the correct page and Back returns to the previous one.
5. Check that an item without a spoilage rule has no empty spoilage section. Check the new headings and note in the chosen interface language, normal/large window and at the usual zoom.

The times are reference shelf lives, not a countdown for an individual item in the save.
