# Item Intelligence 1.7.43.2-test5

For Quasimorph 1.0.4.590s.969afc6. DEV validation build, retaining the user-verified test4 fixes.

Materials can still be valuable after ship research is finished. Magnum now lists their uses in ranged weapons, melee weapons, every armor slot, clone genomes and class modifications. Overview offers direct links to the relevant Magnum tab. The Magnum catalog filter includes these items even when no remaining research uses them.

Equipment links use each production receipt's `ModifyItemsGrades`. A nonempty override replaces the corresponding `MagnumProjectPrices.ItemsGrades` table; an empty override uses the default. Non-equipment recipes are excluded. Each resource links to the exact equipment whose upgrade configuration includes it. No use is inferred from item names or crafting ingredients. Price grades are not displayed as quantities, and research requirement, owned and surplus calculations are unchanged.

Pact ritual components and items installed on clones (implants and augmentations) also show their specific operations. These links follow `PactComponentRecord`, `ImplantRecord` and `AugmentationRecord`, matching the native ritual selection and installation paths. Training and rank progression use time/experience, so they do not create invented resource requirements.

The metadata adapter is gated to the audited game assembly and reads public typed records. It constructs no gameplay projects, spends no resources and changes no save data. All eight interface languages include the new labels.

Validation:

- Full build contracts and automated suites passed, including 90 production upgrade-use assertions, 5,689 localization assertions and the existing weapon/browser regressions.
- Installed tables contain 532 production receipts and 350 nonempty upgrade overrides. The production reader, using the installed game's actual record types and parser, matched all 1,786 expected material/category/equipment links across 61 project materials.
- Tests cover default replacement, all armor slots, nonpositive grades, unknown items, duplicate and malformed rows, bounds, live refresh, settings, session reset, Overview navigation and completed-research cases. Hash checks preserve the three existing research calculation/rendering method bodies.
- The complete mod compiled against the installed game and its 13-file package was staged successfully. Live Unity interaction with the new upgrade section remains a manual check.

Quick live check: inspect `medical_components`, `medium_chip`, `prototype_schematics`, `armor_container`, `class_chip` and `precious_metals`. Check the upgrade block in Overview and Magnum, follow a listed equipment link, and verify that completed ship research does not hide these uses.
