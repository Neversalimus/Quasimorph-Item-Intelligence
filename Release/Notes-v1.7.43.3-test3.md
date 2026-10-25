# Item Intelligence 1.7.43.3-test3

- Moved UI translations, game-name caches and private mixed-language fonts into separate components with explicit session cleanup.
- Centralized the 11 official languages, saved-setting aliases, file names and number cultures in one registry shared by the mod and developer tools.
- Fixed direct native translation lookups retaining cached text after the game language changes.
- Added bounded diagnostics that distinguish a missing required field from a getter failure, while leaving optional compatibility probes quiet.
- Replaced selected source-spelling checks with behavior tests, including language selection, font recovery, probability-range formatting and required data reads. Retired-code checks now inspect C# declarations; single-reference findings are advisory.
- Added a reproducible local-game font audit using the actual language enum and font presets. It checks every shipped translation without exporting game assets.

Includes all previous fixes and translations in this test series. Font coverage and automated tests do not replace an in-game layout check before a stable release.
