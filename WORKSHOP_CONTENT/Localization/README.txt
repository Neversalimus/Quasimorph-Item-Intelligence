Item Intelligence - Community Localization
=========================================

Item Intelligence UI text is loaded from UTF-8 .lang files.
Game item, enemy, faction, station and research names continue to use Quasimorph localization when available.

FILES
-----
en.lang                    Built-in English UI.
ru.lang                    Built-in Russian UI.
TranslationTemplate.lang   Complete template for a community translation.

CREATE A TRANSLATION
--------------------
1. Copy TranslationTemplate.lang to a new file, for example Japanese.lang.
2. Change the first line to aliases reported by the game, for example:
   @language=Japanese;ja
   @language=Korean;ko
   @language=ChineseSimplified;schinese;zhcn
3. Translate only the text after the TAB. Do not change localization keys.
4. Save the file as UTF-8 and place it in ItemIntelligence/Localization/.
5. Restart Quasimorph.

FORMAT
------
key<TAB>translated text

Example:
tab.overview<TAB>1 OVERVIEW

LANGUAGE DETECTION
------------------
Item Intelligence first reads Quasimorph's selected-language metadata. If a distinct language token is not exposed, one community file may use:
   @force=true
That file is used only when no exact language match is available.

FALLBACKS
---------
Missing community keys fall back to en.lang and are reported in Player.log. If the English key is also missing, the UI displays [key].

VALIDATION
----------
Community files are decoded as strict UTF-8. Duplicate keys and malformed lines are reported. Missing keys fall back individually to English.

CJK / IDEOGRAPHIC LANGUAGES
---------------------------
Chinese, Japanese, Korean and other scripts are accepted by .lang files. Rendering depends on glyphs available in Quasimorph's TextMeshPro fonts and fallbacks.
