# Item Intelligence 1.7.43.3-test4

- Fixed the clipped Turkish ammunition tab label in the normal window by reducing excess text padding.
- Moved reflection metadata and method lookup out of shared mod state. Callers receive read-only member lists; changing game values are read on every request.
- Removed redundant string allocations from cached item names, Magnum names, native translations and font signatures. Language changes and session resets retain their existing invalidation behavior.
- Replaced selected source-text checks with behavioral tests for UI setters and row reuse. They cover repeated redraws, changing values, switching languages, readability settings, dynamic content and missing UI objects.
- Added tests that intentionally break temporary source copies to verify that these regressions are detected.

Includes the translations and fixes from earlier builds in this test series. This remains a test release. Browser language changes apply after saving; MCM setting captions update after restarting the game.
