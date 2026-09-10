Item Intelligence 1.7.42.5 adds more languages, a larger browser view and corrected implant source information.

- Added German, Polish and Simplified Chinese, with an independent interface-language selector in MCM. Item and location names continue to follow the game's language.
- Added a large window covering 94% of the screen and 100–200% zoom. Normal and large views remember their own settings.
- Improved columns, scrolling, catalog layout and item links at larger scales.
- Corrected enemy implant sources to account for available body parts, compatible tissues, occupied sockets and preset implants. Sources affected by random body changes are marked Conditional; recovery by amputation keeps its separate chance.
- Fixed missing Chinese settings in MCM after restarting the game, including Chinese text in the language selector.
- Improved fitting of long German buttons and the All category label.

Test5's production code is retained with only the release version and runtime marker changed. Game evidence includes EN/ZH layouts at 2560×1440, PL/DE layouts at 1920×1080 and Chinese MCM persistence after a full restart. Review scope and remaining limits are documented in `Support/ReleaseAcceptance17425.md`.

Reviewed game: Quasimorph 1.0.4.581s.2952480. Numerical compatibility remains checked separately for each feature; unsupported calculations retain their existing safeguards.
