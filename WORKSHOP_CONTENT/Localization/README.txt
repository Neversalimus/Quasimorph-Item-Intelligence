Item Intelligence - UI languages
================================

Built in: English (en.lang), Russian (ru.lang), German (de.lang), Polish (pl.lang),
Simplified Chinese (zh-Hans.lang), Spanish (es.lang), Brazilian Portuguese
(pt-BR.lang), and French (fr.lang). Each of the eight languages and
TranslationTemplate.lang has 660 keys. Brazilian Portuguese uses the actual game
identifier BrazilianPortugal; it does not claim European Portuguese (pt-PT).

MCM language selection
----------------------
Auto (Game) follows the selected Quasimorph language. Manual options change QII UI
only. Game-provided item, enemy, faction, station and research names still follow
the game. The config.ini key is UiLanguage. MCM captions refresh on game restart;
the QII browser refreshes immediately after saving settings.

Create a community translation
-------------------------------
Copy TranslationTemplate.lang, rename it, and set @language to exact game aliases,
for example Japanese;ja or Korean;ko. Translate only values after the TAB. Preserve
keys, {KEY}, numeric placeholders such as {0}, and leading/trailing spaces used
in composed phrases. Save as UTF-8 and restart Quasimorph.

Selection and fallback
-----------------------
An exact community language match overrides the built-in file for that language.
In Auto mode only, one community file with @force=true may override the built-in
or fallback choice when there is no exact community match. It cannot override a
manual selection of a different language. Multiple forced files do not force a choice.
Missing values fall back individually to en.lang and are reported in Player.log.
Duplicate keys, malformed rows and invalid UTF-8 are also reported.

Fonts and wrapping
-------------------
QII uses Quasimorph font presets for the selected UI language. Mixed UI/game scripts
use a private font asset with a separate fallback list. Global fonts are untouched.
CJK notes wrap without requiring spaces. Rendering still needs an in-game check
with the installed game and actual resolution; community scripts need suitable
font glyphs in the game.
