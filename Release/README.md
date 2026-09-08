# Frozen release workflow

These reusable scripts replace the release implementation supplied with the earlier candidate. They reject DEV source. The default destination remains public Workshop item 3780078201 and GitHub repository Neversalimus/Quasimorph-Item-Intelligence.

The v1.7.42.4 source kit includes thin version-specific launchers: preparation restores the accepted source commit from a hash-checked Git bundle into a separate checkout, then calls this workflow. Publication calls the frozen checkout's publisher. The launchers do not apply code patches or implement a second publishing mechanism. See INSTRUCTIONS_RU.md for the complete Windows sequence.

After DEV game acceptance, prepare and review a stable source commit in a Git checkout. Promotion requires a stable Runtime version and marker, the matching GameplayExactness contract, the release installer and updated release notes. The build gates intentionally reject a mixed DEV/stable source tree. Do not use the older per-version publisher alongside this workflow.

From that reviewed stable checkout:

```powershell
pwsh -NoProfile -File .\Release\Prepare-Release.ps1 -GameRoot "X:\SteamLibrary\steamapps\common\Quasimorph" -OutputDirectory "C:\QM_Workshop\Frozen\ItemIntelligence-v1.7.42.4"
```

Preparation checks origin, a clean source commit and ancestry from remote main, compiles the mod, and retains:

- The complete frozen payload, including every localization and hidden file.
- The exact distribution ZIP and SHA256SUMS.txt.
- A schema-2 receipt containing the full relative-path/SHA256/length file map, tracked source file hashes, commit, main lease and game fingerprint.

The freeze directory must be new and outside the checkout. Preparation never deletes or reclones the source checkout. Keep the original stage, frozen directory and source checkout until publication is complete. The printed next command references the actual `Publish-Release.ps1` file.

Publish the frozen payload to the existing Steam item and complete the normal Steam acceptance gate. Then:

```powershell
pwsh -NoProfile -File .\Release\Publish-Release.ps1 -ReceiptPath "C:\QM_Workshop\Frozen\ItemIntelligence-v1.7.42.4\release-receipt.json" -WorkshopPublished
```

`-WorkshopPublished` is the operator's acknowledgement of the Steam gate; the GitHub publisher does not itself check Steam. It rechecks the source and every frozen file, updates main/tag atomically with explicit leases, uploads assets into a draft release, downloads them for SHA256 verification and only then promotes the draft. It refuses different existing assets and conflicting refs. Git servers without atomic push support stop without a sequential fallback.

After a connection failure, rerun the same publish command with the same receipt. Matching remote refs/assets are reused, partial uploads continue, and a completed release is verified without replacing assets. ZIPs and receipts remain on disk after any failure. If remote main advanced or a tag/asset differs, stop and review the conflict rather than rewriting history or forcing replacement.

Local regression coverage is in `Tests/Run-ReleaseTests.ps1`. Its native transports are mocks: it does not connect to GitHub or Steam. Build contracts execute that suite along with production C# behavior tests. Game acceptance and an actual authorized remote publication remain separate checks.
