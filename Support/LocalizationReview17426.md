# Localization release review — 1.7.42.6

Reviewed on 2026-09-11 against the accepted test2 dictionaries. All 657 values in each of Spanish, Brazilian Portuguese and French were read side by side with English (1971 translated values). Existing EN/RU/DE/PL/ZH and the template are unchanged. The review found no missing translation or material semantic error that blocks release. This is a source/text review by the coding assistant, not a native-speaker certification.

The distinction between item-pool membership and a guaranteed reward, enemy generation and encountering an enemy, successful implant installation and amputation recovery, conditional body sources and exact percentages is preserved. Container placement is not presented as an extra independent drop roll. Corpse bonuses are described as separate rolls. The Baron consumption bound is not described as a prediction of AI actions. Modder actions explicitly describe real changes to the current save.

All eight languages and the template contain 657 keys. The existing production localization suite passes 5385 assertions for actual key resolution, selection, aliases, formatting, fallbacks and ownership with game/TMP fixtures. Placeholders, advanced-search operators and compositional whitespace are preserved. New languages correctly use decimal commas; ES/FR use a space before %, PT-BR does not. French days use j.

## Game evidence

The supplied log loads es.lang, pt-BR.lang and fr.lang with 657 entries after saving their MCM choices. Fifteen supplied screenshots cover RU/ES/PT-BR/FR, normal 100–125% and expanded 150% views at a logged 1920×1080. They show readable new-language accents and mixed Russian game names, catalog, recipes, Trade, Magnum, Scavenger and expanded Loot notes. Game-provided names remaining Russian are the intended manual-language behavior.

The complete filename/hash inventory and individual observations are in RuntimeAcceptance17426Test2.json. One pair of French loot screenshots is nearly identical; the file count is not a count of independent tests. Full restart of each new language, Auto under a newly selected game language and every MCM caption were not shown by this evidence.

## Small presentation correction

French CATALOGUE and long armor/equipment captions in French and Brazilian Portuguese clip beside icons in test2. Stable source enables TMP autosizing only for the catalog launcher and catalog controls, bounded between 8 and their existing 9.5–10.5 logical font sizes (launcher maximum 10). Window zoom continues to scale the entire interface. Button geometry, translations, hit targets and game calculations are unchanged. The final correction compiles against the game DLLs and retains existing geometry checks; final Unity pixels are not claimed as verified.

Terminology is understandable and internally consistent. Stylistic refinements from native speakers remain welcome after release. Neither automated coverage nor this review promises flawless idiom in every context.
