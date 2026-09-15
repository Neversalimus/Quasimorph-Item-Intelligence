# Quasimorph Item Intelligence

**Item Intelligence** is an in-game item browser and reference tool for **Quasimorph**. Open it with **F2** to find items, compare sources and follow crafting, trade and equipment relationships.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3780078201) · [Downloads](https://github.com/Neversalimus/Quasimorph-Item-Intelligence/releases) · [Changelog](CHANGELOG.md)

This branch contains **1.7.42.8** for **Quasimorph 1.0.4.582s.c4335c5**.

This release adds optional verbose logging. Normal mode keeps `Player.log` concise while preserving warnings, errors and a small set of lifecycle/status messages; detailed runtime, UI, index, navigation and performance diagnostics can be enabled through **MCM → Logging → Verbose logging**.

## Features

- Item search, catalog, favorites, browsing history and advanced filters.
- Item overview, recipes, disassembly, station production and Magnum requirements.
- Live station trade information, stock-sensitive prices and batch totals on supported game builds, with direct navigation to the starmap.
- Cards and table layouts for Trade, selectable inside the tab.
- Loot sources grouped by containers, enemies, faction rewards, mission pools and special sources.
- Container chance estimates and a loot-modifier calculator for Marauder, Organization and Field Medic.
- Enemy implant sources that account for body slots, tissue compatibility, preset implants and socket competition.
- Faction technology, weapon/ammunition links and detailed fire-mode tooltips.
- A large browser window and adjustable interface scale.
- An optional enhanced readability setting for brighter labels and larger tabs and notes.
- Optional Modder Mode for creating items in ship cargo or the mission clone's inventory.

## Installation and controls

Subscribe through [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3780078201), enable Item Intelligence in the game's mod list and restart the game. Enable only one copy of the mod at a time.

Press **F2** to open the browser. MCM provides the hotkey, interface language and other settings. The browser footer provides **Large window / Normal window** and **− / +** scale controls.

Large view covers 94% of the screen. Normal view starts at 100% scale and large view at 150%; each remembers its own 100–200% setting in 25-point steps. On small screens, the effective scale is limited to keep controls accessible.

Enable **Enhanced readability** in MCM for brighter secondary text and larger tab and note labels. It is off by default and works alongside the window-size and zoom controls.

**Verbose logging** is also optional and disabled by default. Enable it only when collecting detailed diagnostics for troubleshooting; the setting is saved across restarts.

Modder Mode is optional and creates real items in the current game. Leave it disabled when using the mod solely as a reference browser.

## Languages

The interface includes **English, Russian, German, Polish, Simplified Chinese, Spanish, Brazilian Portuguese and French**.

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

Use your actual game path. This compiles the stable source against the installed game and stages the files in `C:\QM_Workshop\ItemIntelligence`. The installer prints the command for updating the existing public Workshop item. It does not install over a subscribed copy or upload to Steam. Game assemblies and compiled binaries are not stored in this repository.

Run the source contracts and behavior tests without game files:

```powershell
pwsh -NoProfile -File .\BUILD_AND_STAGE.ps1 -Mode RELEASE -ContractsOnly
```

Maintainers can use the [release workflow](Release/README.md) to prepare and publish a new version.

Item Intelligence is a third-party mod and is not affiliated with Magnum Scriptum.
