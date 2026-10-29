# Quasimorph Item Intelligence

**Item Intelligence** is an in-game item browser and reference tool for **Quasimorph**. Open it with **F2** to find items, compare sources and follow crafting, trade and equipment relationships.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3780078201) · [Downloads](https://github.com/Neversalimus/Quasimorph-Item-Intelligence/releases) · [Changelog](CHANGELOG.md)

This branch contains development build **1.7.43.4-test4** for **Quasimorph 1.0.4.590s.969afc6**.

This build isolates ship-stock polling and browser navigation from the shared UI state and adds a common local/CI check runner. It also reuses unchanged catalog ordering and reduces work when creating the browser. The published stable release is [1.7.43.3](https://github.com/Neversalimus/Quasimorph-Item-Intelligence/releases/tag/v1.7.43.3).

It also shows what perishable items turn into and which items can spoil into a resource, directly in **Overview** and **Recipes**. These are clickable, immediate transformations from the game's spoilage table. Times refer to fresh items; refrigeration pauses spoilage.

Compatibility for the new build is enabled through audited feature-owned gates. Unknown future builds continue to fail closed for exact calculations until their contracts are reviewed.

Optional verbose logging keeps `Player.log` concise in normal mode while preserving warnings, errors and a small set of lifecycle/status messages; detailed runtime, UI, index, navigation and performance diagnostics can be enabled through **MCM → Logging → Verbose logging**.

## Features

- Item search, catalog, favorites, browsing history and advanced filters.
- Item overview with live ship stock, recipes, disassembly, station production and Magnum requirements.
- Upgrade material uses and clickable equipment targets, separate from finite ship-research totals.
- Spoilage results and sources, including food and perishable quasimorph equipment, with shelf lives for fresh items.
- Live station trade information, stock-sensitive prices and batch totals on supported game builds, with direct navigation to the starmap.
- Cards and table layouts for Trade, selectable inside the tab.
- Loot sources grouped by containers, enemies, faction rewards, mission pools and special sources.
- Container chance estimates and a loot-modifier calculator for Marauder, Thrift and Field Medic.
- Enemy implant sources that account for body slots, tissue compatibility, preset implants and socket competition.
- Faction technology, weapon/ammunition links and detailed fire-mode tooltips, including exact per-mode damage types.
- Native **ALT / More details** support for item tooltips inside the Item Intelligence browser.
- A large browser window and adjustable interface scale.
- An optional enhanced readability setting for brighter labels and larger tabs and notes.
- Optional Modder Mode for creating items in ship cargo or the mission clone's inventory.

## Installation and controls

Subscribe through [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3780078201), enable Item Intelligence in the game's mod list and restart the game. Enable only one copy of the mod at a time.

Press **F2** to open the browser. MCM provides the hotkey, interface language and other settings. Hover a QII item icon and hold **ALT** to use Quasimorph's native **More details** view. The browser footer provides **Large window / Normal window** and **− / +** scale controls.

Large view covers 94% of the screen. Normal view starts at 100% scale and large view at 150%; each remembers its own 100–200% setting in 25-point steps. On small screens, the effective scale is limited to keep controls accessible.

Enable **Enhanced readability** in MCM for brighter secondary text and larger tab and note labels. It is off by default and works alongside the window-size and zoom controls.

**Verbose logging** is also optional and disabled by default. Enable it only when collecting detailed diagnostics for troubleshooting; the setting is saved across restarts.

Modder Mode is optional and creates real items in the current game. Leave it disabled when using the mod solely as a reference browser.

The **On ship** row in Overview counts the inspected item across cargo tabs, the fridge and the idle recycling storage. Items being recycled and items carried by clones are excluded. Stack quantities are summed, and upgraded variants retain their own counts. The row reads the current save during missions as well as in space and refreshes while Overview is open. **0** means none are stored; **?** means the cargo data is unavailable.

## Languages

The interface includes all 11 official game languages: **English, Russian, German, Polish, Simplified Chinese, Spanish, Brazilian Portuguese, French, Japanese, Korean and Turkish**.

Choose **Auto (Game)** in MCM to follow Quasimorph's language, or select a language for Item Intelligence separately. Item, enemy, faction and location names—and searches for those names—continue to follow the game language. The browser updates after saving settings; MCM captions refresh after restarting the game.

Community translations are supported. See the [translation guide](WORKSHOP_CONTENT/Localization/README.txt) and [template](WORKSHOP_CONTENT/Localization/TranslationTemplate.lang). Localized Workshop descriptions are in [Workshop](Workshop/README.md).

## Compatibility and probability information

Compatibility is checked separately for numerical features. After an unsupported game update, an affected exact calculation can display `?` or an unavailable explanation until its game logic has been checked.

Exact probabilities, estimates and conditional sources have different meanings. Container estimates depend on the available game data and exclude some location and generation effects. Enemy implant installation and recovery by amputation are separate chances. Sources affected by random body changes are marked **Conditional**, without an exact probability for the entire sequence of changes.

## Building from source

A Windows build requires **PowerShell 7** and a local Quasimorph installation. From the repository directory:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\Install.ps1 -GameRoot "C:\Program Files (x86)\Steam\steamapps\common\Quasimorph"
```

Use your actual game path. This compiles the development source against the installed game and stages a local test package in `_stage\ItemIntelligence` beside the source. Pass `-WorkshopStage` to choose another package directory. The installer does not replace a subscribed copy or upload to Steam. Game assemblies and compiled binaries are not stored in this repository.

A stage must be empty or contain an Item Intelligence package. Source directories, game installations, live Steam content, linked paths and folders with unrelated files are rejected. Existing packages are backed up before replacement. If an interrupted build leaves an incomplete `.tmp` folder, inspect it and choose a fresh stage path.

Run the source contracts and behavior tests without game files:

```powershell
pwsh -NoProfile -File .\Tests\Run-Checks.ps1
```

Language metadata lives in `Source/LanguageCatalog.cs`. The UI selector, loader, number formatting and build tools use this registry. `UiLocalization`, `GameLocalization` and `UiFontResolver` own their caches; the game hooks explicitly reset them on returning to the menu and on exit. Optional reflection probes remain quiet; required reads report missing members or getter failures once per schema member, with a 32-entry session limit.

`ReflectionDataReader` owns cached reflection metadata and exposes read-only member snapshots. Game values are read on every request. Metadata remains valid for the lifetime of the loaded game assemblies, so returning to the menu does not rescan it.

`ShipCargoMonitor` owns the selected item, last known count and polling deadline for one inspection. Its reader and clock input are supplied by the UI adapter; it retains no game or UI objects. Its tests compile it independently with System only, then exercise the actual Overview adapter against game fixtures.

`BrowserNavigationSessionState` owns bounded back history, the selected tab and per-tab scroll. Its collections remain private; the UI uses explicit navigation operations. Back selects an immutable snapshot, closes the old item's overlays and then restores the snapshot. Tests compile the state owner without game/UI fixtures and execute the production item, back, tab, wheel and loot-section adapters separately.

Run the complete check set, including deliberately broken temporary source copies:

```powershell
pwsh -NoProfile -File .\Tests\Run-Checks.ps1 -IncludeMutationTests
```

The runner selects TEST or RELEASE checks from the source version and propagates failed child-process exits. GitHub Actions runs the same command on main-branch pushes and pull requests using Windows and PowerShell 7, with read-only repository permissions. The mutation suites require the behavior tests to reject each deliberate bug; the source checkout is untouched. No game files, Python, staging or publication credentials are required. Actual game-font coverage and visual checks remain separate.

To check glyph coverage against your installed game's actual language enum and TMP font presets, use Python 3.12 and the pinned audit dependencies (validated with Quasimorph **1.0.4.590s.969afc6**):

```powershell
python -m venv .venv-fonts
.\.venv-fonts\Scripts\python.exe -m pip install -r .\Tests\font-coverage-requirements.txt
.\.venv-fonts\Scripts\python.exe -m unittest discover -s Tests -p test_font_coverage.py
pwsh -NoProfile -File .\Tests\Run-FontCoverage.ps1 -GameRoot "C:\Program Files (x86)\Steam\steamapps\common\Quasimorph" -Python .\.venv-fonts\Scripts\python.exe
```

The command reads local game assets and writes `_build/font-coverage.json`, including the game-assembly hash, dependency versions, translation hashes and missing characters with their source keys. It does not export game assets. Missing font presets or unsupported game-language metadata fail the audit; dynamic fonts are checked conservatively against existing atlas glyphs. For a stable release, also check the browser, tooltips and MCM in game, especially Japanese, Korean and mixed UI/game language combinations.

Maintainers can use the [release workflow](Release/README.md) to prepare and publish a new version.

Item Intelligence is a third-party mod and is not affiliated with Magnum Scriptum.
