# Release workflow

These scripts build and freeze a stable version, then publish its files to the existing [Steam Workshop item](https://steamcommunity.com/sharedfiles/filedetails/?id=3780078201) and [GitHub repository](https://github.com/Neversalimus/Quasimorph-Item-Intelligence).

Use a reviewed, committed stable checkout with the matching version in the runtime, installer and build contracts. The scripts require PowerShell 7, Git, authenticated GitHub CLI access and the local game installation. DEV source is rejected.

## Prepare

From the repository root, choose a new output directory outside the checkout:

```powershell
pwsh -NoProfile -File .\Release\Prepare-Release.ps1 -GameRoot "X:\SteamLibrary\steamapps\common\Quasimorph" -OutputDirectory "C:\QM_Workshop\Frozen\ItemIntelligence-vNEXT"
```

Replace `vNEXT` with the version being prepared. Preparation checks the source and repository, runs the build and freezes the staged payload, distribution ZIP, checksums and `release-receipt.json`. The receipt records the source commit and file hashes used by publication.

Keep the source checkout, original stage and frozen directory intact until publication finishes. Release notes can be placed in `release-notes.md` next to the receipt before publishing.

## Publish

Publish the frozen `payload` folder to the existing Workshop item and verify the public copy in the game. Then run:

```powershell
pwsh -NoProfile -File .\Release\Publish-Release.ps1 -ReceiptPath "C:\QM_Workshop\Frozen\ItemIntelligence-vNEXT\release-receipt.json" -WorkshopPublished
```

`-WorkshopPublished` confirms that Workshop publication and verification have been completed. The publisher checks the frozen source and payload, updates the Git references atomically, uploads assets to a draft, downloads them for checksum verification and then publishes the release.

A retry uses the same source checkout, receipt and frozen files. Conflicting references or assets stop publication; the scripts do not replace them automatically. Keep main unchanged while a frozen release is being published. After publication, start a new version from a separate checkout instead of reusing the completed receipt for new source changes.

The workflow's local regression tests are in [Tests/Run-ReleaseTests.ps1](../Tests/Run-ReleaseTests.ps1). Those tests use simulated GitHub and Steam interactions; they do not publish anything.
