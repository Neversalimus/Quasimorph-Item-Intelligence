> Historical test1 snapshot. For the completed test2 text/game review and stable decision, see [LocalizationReview17426.md](LocalizationReview17426.md) and [ReleaseAcceptance17426.md](ReleaseAcceptance17426.md).

# Spanish, Brazilian Portuguese and French — 1.7.42.6-test1

Date: 2026-09-11. Runtime marker: `LatinLanguagesTest01`.
Base: stable source `13fa5e012ec8a87d7e372c96af28a2f852012380` (1.7.42.5).
Destination: DEV Workshop item `3781927679`, stage `C:\QM_Workshop\ItemIntelligence_DEV`.

## Scope

Adds 657 translated UI values in each of Spanish, Brazilian Portuguese and French: 1971 new values. Eight language files plus the community template now have the same 657 keys. The existing EN/RU/DE/PL/ZH/template files are byte-identical to the base commit.

The translation covers catalog filters, overview, Magnum, recipes, Trade, weapons/ammunition, factions, loot sources and conditions, optional Modder Mode, MCM, window size and zoom controls. Keys, placeholders, query syntax, and compositional leading/trailing spaces are preserved. The new compact catalog labels are at most eight characters. Three corresponding Workshop description drafts are included in `Workshop/` for use after acceptance and publication.

## Language selection and presentation

| MCM choice | Game identifier | File | Number culture |
| --- | --- | --- | --- |
| Español | Spanish | es.lang | es-ES |
| Português (Brasil) | BrazilianPortugal | pt-BR.lang | pt-BR |
| Français | French | fr.lang | fr-FR |

The unusual `BrazilianPortugal` identifier was read from `MGSC.Localization.Lang` in the supplied game assembly's decompiled source. `Spanish` and `French` are also actual enum members. Portuguese support is explicitly Brazilian; neither generic `pt` nor `pt-PT` is declared as a Brazilian alias.

New options follow the existing six MCM choices, preserving their order. Manual preferences accept the display names, game enum names and documented locale aliases. Auto uses game metadata. New files are registered as built-in dictionaries, so exact community overrides and Auto-only force behavior retain their existing priority.

All three languages use decimal commas. Spanish and French include a space before the percent sign; Brazilian Portuguese does not. Compact time units are localized, including French `j` for days. Long notes use the conservative Latin wrap fallback. No gameplay probability method or game compatibility fingerprint changed.

Game-provided item, enemy, station, faction and research names remain in the game language; search continues to use those names. F2 refreshes after MCM settings are saved. MCM's registered captions refresh after a game restart. Existing private font fallback ownership is retained.

## Observed local verification

| Check | Result |
| --- | --- |
| Static build contracts, TEST mode | PASS |
| Existing production behavior | 660 assertions passed |
| Body/implant behavior | 4923 assertions passed |
| Frozen release workflow | 32 assertions passed; native transports mocked |
| Production localization behavior | 5385 assertions passed |
| MCM font ownership/lifecycle | 38 assertions passed |
| Browser viewport geometry | 54917 assertions passed |
| C# 5 compilation | 112 production files, 118 supplied dependencies, 0 errors, 0 warnings |
| Key/token/whitespace parity | 657 keys in all 9 language/template files |
| Existing language preservation | EN/RU/DE/PL/ZH/template byte-identical to base |
| Advanced search tokens | Preserved in all three new dictionaries |
| Workshop description syntax | Three drafts with balanced BBCode tags |

Localization tests execute production reflection, selection, parser, cache, formatting, wrapping and private font methods. Each shipped key is resolved in each of the eight languages without an English fallback. Additional cases cover real enum names, saved preference aliases, manual switching while the game remains Russian, unsupported European Portuguese, force priority, and Latin UI fonts combined with Chinese game names. The game, Unity, TMP and MCM services in these tests are fixtures.

Compilation target: Quasimorph `1.0.4.581s.2952480`.
Assembly-CSharp SHA256: `BE78036434737521BB43DABBD934B579A08DC3E8370B834AEE36E40932F56FE0`.
Observed output is retained in `Localization17426_Checks.txt`; dictionary hashes and structured scope are in `Localization17426.json`.

## Remaining acceptance

ES/PT-BR/FR have not yet been run in Unity. Check all three languages in MCM and F2, saved selection after a full restart, Auto detection, accents, expanded Trade/Loot notes, and large view at 1920×1080 with 150–200% zoom. The Russian setup document supplies the precise DEV route.

Native-speaker terminology and style review are pending. Automated parity and format checks establish completeness and integration, not linguistic or visual perfection. Historical Test3–Test5 evidence for 1.7.42.5 is retained as evidence for that base; it is not new-language acceptance. Its independent compatibility and performance limits remain in effect.

The source package prepares DEV only. No Workshop upload or GitHub publication was performed for this candidate.
