# Release acceptance — Item Intelligence 1.7.42.6

Decision: ready for stable source preparation, with the scope below. This document does not attest that Steam or GitHub publication has happened.

- Accepted runtime candidate: 1.7.42.6-test2 / Compat104582Test02, commit `b6604f579360522b7173242801d743e345facaf0`.
- Release marker: 1.7.42.6 / StableRelease17426.
- Current game: 1.0.4.582s.c4335c5, Assembly-CSharp SHA256 `9D0C784A764D75EC6616BD3B5744C0E581BB1CE148FD175BD8CABA8195854006`.
- User reports the candidate works and authorizes release if localization is acceptable. The complete source review of ES/PT-BR/FR and supplied game evidence found no blocking localization issue.

## What the supplied run establishes

The log confirms exact Trade pricing on the new fingerprint, verified Marauder/Organization/Field Medic values, container-save estimates, completed loot/body indexes and Scavenger eligible mission calculations. Screenshots show restored next-unit/batch values and readable ES/PT-BR/FR text. Disassembly symmetry is OK, core partialFailures is zero, all 28 container profiles are populated, and ordinary body/implant indexing completes. An explicitly requested Modder action adds one plastic item to cargo.

The source audit in Compatibility104582.md explains the narrower compatibility decision and the genuinely changed scatter formula. The global UNVERIFIED-COMPATIBLE status is retained; this release does not certify unrelated exact feature families.

## Stable promotion delta

Relative to the accepted candidate, production changes are confined to Runtime version/marker and bounded TMP autosizing for catalog launcher/category/filter labels, addressing the clipping visible in FR/PT-BR screenshots. All gameplay and compatibility source files, all nine language/template files, MCM and window geometry are byte-identical to the accepted candidate. Install.ps1 now stages the stable public item, and GameplayExactness checks its stable identity. Release scripts retain the corrected draft-list discovery and frozen retry behavior from 1.7.42.5.

## Verification

| Suite | Passed assertions |
| --- | ---: |
| Production behavior | 693 |
| Enemy body/implant | 4939 |
| Weapon scatter: modern, legacy and incompatible APIs | 48 |
| Release workflow, native transports mocked | 32 |
| Localization | 5385 |
| MCM font ownership/lifecycle | 38 |
| Viewport geometry | 54917 |
| Total | 66052 |

RELEASE contracts passed. All 112 C# files compile as C# 5 against 118 supplied dependencies of the new game, with no errors or warnings. The accepted candidate also compiled against the old 1.0.4.581 dependencies. These suites use fixtures for Unity/game services and are not 66052 game sessions. The final Windows DLL is built on the publisher's machine and its own SHA256 is captured in the frozen receipt.

## Documented limits

The log contains one known SliderWrapper.Awake exception during MCM prefab construction. The shown stack contains no QII frame and settings subsequently save, but this does not establish the entire cause. Two performance budget overruns are retained (cold 61.2 ms, warm 31.7 ms with render 22.0 ms). A single screenshot's low FPS cannot be attributed to the mod.

These screenshots do not independently verify actual transactions, augmented scatter in a mission, every new-language MCM caption after a full process restart, Auto under all new game languages, or the final catalog autosizing pixels. The scatter behavior is supported by the game assembly audit and production API tests. Existing persistence/font behavior is unchanged. Native-speaker terminology review remains welcome. Prior Chinese MCM restart evidence is historical and is not described as a new-language restart test.

See RuntimeAcceptance17426Test2.json for the exact evidence inventory and LocalizationReview17426.md for the linguistic review. Publish using the frozen payload and receipt, then verify the public runtime marker before the GitHub publisher's -WorkshopPublished acknowledgement.
