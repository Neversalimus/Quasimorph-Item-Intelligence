# Item Intelligence 1.7.43.3-test2

- Fixed language fonts getting stuck on a temporary fallback when a game font loads late. Recovery now works without switching languages or restarting.
- Added build destination checks to protect source files, game installations, linked paths and unrelated folders.
- Excluded the default local package directory from Git.
- Indexed source references once per build, preserving the existing check while avoiding repeated scans of the entire source tree.

Includes the 11-language translation update from test1. This remains a local test build.
