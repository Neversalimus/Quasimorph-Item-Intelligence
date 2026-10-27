using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace ItemIntelligence
{
    /// <summary>
    /// Conservative hardening layer introduced in v1.7.33-test1.
    /// This partial contains maintenance/diagnostic behavior only. It intentionally
    /// does not add gameplay mutation paths or change Item Intelligence data semantics.
    /// </summary>
    public static partial class ModMain
    {
        private const string LastVerifiedGameVersion = "1.0.2.575s.d02a8d8";
        private static string _buildFingerprint = string.Empty;
        private static string _compatibilityVerdict = "UNKNOWN";
        private static int _diagnosticsHotkeyLastFrame = -1000;
        private static int _diagnosticsHotkeyConsumedFrame = -1000;

        private static string DiagnosticsReportPath
        {
            get { return Path.Combine(ConfigDirectory, "diagnostics_report.txt"); }
        }

        private static string DiagnosticsSessionStatePath
        {
            get { return Path.Combine(ConfigDirectory, "diagnostics_session_state.txt"); }
        }

        private static string DiagnosticsSessionEndPath
        {
            get { return Path.Combine(ConfigDirectory, "diagnostics_session_end.txt"); }
        }

        private static string LocalizationHealthReportPath
        {
            get { return Path.Combine(ConfigDirectory, "localization_health_report.txt"); }
        }

        private static string RegressionSelfTestPath
        {
            get { return Path.Combine(ConfigDirectory, "regression_selftest.txt"); }
        }

        private static void RefreshBuildFingerprint()
        {
            try
            {
                bool gameVersionMatch = string.Equals(
                    Application.version ?? string.Empty,
                    LastVerifiedGameVersion,
                    StringComparison.OrdinalIgnoreCase);
                bool assemblyMatch = string.Equals(
                    _compatAssemblySha256 ?? string.Empty,
                    ValidatedAssemblyCSharpSha256,
                    StringComparison.OrdinalIgnoreCase);

                bool degraded = !_compatCore || !_compatSearchCatalog || !_compatMagnum ||
                    !_compatRecipes || !_compatTrade || !_compatAmmo || !_compatDisassembly ||
                    !_compatFactions || !_compatLoot || !_compatTooltip || !_compatInputGuard;

                if (degraded)
                    _compatibilityVerdict = "DEGRADED";
                else if (gameVersionMatch && assemblyMatch)
                    _compatibilityVerdict = "VERIFIED";
                else
                    _compatibilityVerdict = "UNVERIFIED-COMPATIBLE";

                string shortHash = string.IsNullOrEmpty(_compatAssemblySha256)
                    ? "NOHASH"
                    : (_compatAssemblySha256.Length <= 12
                        ? _compatAssemblySha256
                        : _compatAssemblySha256.Substring(0, 12));

                _buildFingerprint =
                    (Application.version ?? string.Empty) + "|" + shortHash + "|" + _compatibilityVerdict;

                VerboseLog("[ItemIntelligence] Build fingerprint: " + _buildFingerprint +
                    "; lastVerifiedGame=" + LastVerifiedGameVersion + ".");
            }
            catch (Exception ex)
            {
                _compatibilityVerdict = "UNKNOWN";
                Debug.LogWarning("[ItemIntelligence] Build fingerprint failed: " + ex.Message);
            }
        }

        private static bool IsBrowserTabCompatibilityAvailable(int tab)
        {
            switch ((BrowserTabId)tab)
            {
                case BrowserTabId.Overview: return _compatCore;
                case BrowserTabId.Magnum: return ShowMagnumUses && _compatMagnum;
                case BrowserTabId.Recipes: return (ShowRecipes && _compatRecipes) || _compatDisassembly;
                case BrowserTabId.Trade: return (ShowSources || ShowTradeInformation) && _compatTrade;
                case BrowserTabId.Ammo: return ShowAmmoRelations && _compatAmmo;
                case BrowserTabId.Factions: return _compatFactions;
                case BrowserTabId.Loot: return ShowSources && _compatLoot;
                default: return true;
            }
        }

        private static void HandleDiagnosticsHotkey()
        {
            try
            {
                bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                if (!ctrl || !shift) return;

                int frame = Time.frameCount;
                if (frame - _diagnosticsHotkeyLastFrame <= 2) return;

                if (Input.GetKeyDown(KeyCode.F10))
                {
                    _diagnosticsHotkeyLastFrame = frame;
                    _diagnosticsHotkeyConsumedFrame = frame;
                    WriteDiagnosticsReportSafe("ManualCtrlShiftF10");
                    VerboseLog("[ItemIntelligence] Diagnostics exported: " + DiagnosticsReportPath);
                }
                else if (ModderMode && Input.GetKeyDown(KeyCode.F11))
                {
                    _diagnosticsHotkeyLastFrame = frame;
                    _diagnosticsHotkeyConsumedFrame = frame;
                    RunReadOnlySelfTestSafe("ManualCtrlShiftF11");
                    VerboseLog("[ItemIntelligence] Modder read-only self-test exported: " + RegressionSelfTestPath);
                }
            }
            catch { }
        }

        private static bool ShouldWriteAutomaticDiagnostics()
        {
            // Synchronous report construction/disk I/O is diagnostic work, not a normal
            // lifecycle requirement. Keep automatic exports for degraded compatibility
            // or explicit Modder Mode; healthy builds remain available through Ctrl+Shift+F10.
            return ModderMode || string.Equals(
                _compatibilityVerdict, "DEGRADED", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveDiagnosticsReportPath(string trigger)
        {
            if (string.Equals(trigger, "ManualCtrlShiftF10", StringComparison.Ordinal))
                return DiagnosticsReportPath;
            if (string.Equals(trigger, "MainMenuStarted", StringComparison.Ordinal))
                return DiagnosticsSessionEndPath;
            return DiagnosticsSessionStatePath;
        }

        private static string ResolveDiagnosticsReportKind(string trigger)
        {
            if (string.Equals(trigger, "ManualCtrlShiftF10", StringComparison.Ordinal))
                return "MANUAL";
            if (string.Equals(trigger, "MainMenuStarted", StringComparison.Ordinal))
                return "SESSION_END";
            return "SESSION_STATE";
        }

        private static void WriteDiagnosticsReportSafe(string trigger)
        {
            try
            {
                Directory.CreateDirectory(ConfigDirectory);
                RefreshBuildFingerprintIfNeeded();
                string reportPath = ResolveDiagnosticsReportPath(trigger);

                List<string> lines = new List<string>();
                lines.Add("Item Intelligence Diagnostics");
                lines.Add("TimestampLocal=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                lines.Add("Trigger=" + (trigger ?? string.Empty));
                lines.Add("ReportKind=" + ResolveDiagnosticsReportKind(trigger));
                lines.Add("OutputFile=" + Path.GetFileName(reportPath));
                lines.Add("ModVersion=" + Version);
                lines.Add("ReadOnlyKnowledgePolicy=" + ReadOnlyKnowledgePolicy);
                lines.Add("ModderModeExplicitSpawnException=" + ModderModeExplicitSpawnException);
                lines.Add("GameVersion=" + (Application.version ?? string.Empty));
                lines.Add("LastVerifiedGameVersion=" + LastVerifiedGameVersion);
                lines.Add("AssemblyCSharpSHA256=" + (_compatAssemblySha256 ?? string.Empty));
                lines.Add("ValidatedAssemblySHA256=" + ValidatedAssemblyCSharpSha256);
                lines.Add("BuildStatus=" + (_compatBuildStatus ?? string.Empty));
                lines.Add("CompatibilityVerdict=" + (_compatibilityVerdict ?? string.Empty));
                lines.Add("BuildFingerprint=" + (_buildFingerprint ?? string.Empty));
                lines.Add("");
                lines.Add("[CompatibilityModules]");
                AddCompatibilityReportLine(lines, "Core");
                AddCompatibilityReportLine(lines, "SearchCatalog");
                AddCompatibilityReportLine(lines, "Magnum");
                AddCompatibilityReportLine(lines, "Recipes");
                AddCompatibilityReportLine(lines, "Trade");
                AddCompatibilityReportLine(lines, "Ammo");
                AddCompatibilityReportLine(lines, "Disassembly");
                AddCompatibilityReportLine(lines, "Factions");
                AddCompatibilityReportLine(lines, "Loot");
                AddCompatibilityReportLine(lines, "Tooltip");
                AddCompatibilityReportLine(lines, "InputGuard");
                lines.Add("");
                lines.Add("[RuntimeIndexes]");
                lines.Add("IndexesBuilt=" + _indexesBuilt);
                lines.Add("KnownItems=" + KnownItemIds.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("ItemRecords=" + ItemRecordsById.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("MagnumLinks=" + MagnumUses.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("RecipesUsedIn=" + UsedInRecipes.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("RecipesCraftedFrom=" + CraftedFromRecipes.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("SpaceObjects=" + SpaceObjectRecordsById.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("RuntimeFactions=" + RuntimeFactionsById.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("MarketStations=" + MarketStations.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("MarketEntries=" + MarketEntries.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("AmmoItemsWithWeapons=" + CompatibleWeaponsByAmmo.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("DisassemblyItems=" + DisassemblyOutputsByItem.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("DisassemblyReverseItems=" + DisassemblySourcesByOutputItem.Count.ToString(CultureInfo.InvariantCulture));
                int disassemblyReverseLinks = 0;
                foreach (KeyValuePair<string, List<DisassemblySource>> pair in DisassemblySourcesByOutputItem)
                    if (pair.Value != null) disassemblyReverseLinks += pair.Value.Count;
                lines.Add("DisassemblyReverseLinks=" + disassemblyReverseLinks.ToString(CultureInfo.InvariantCulture));
                lines.Add("FactionTechItems=" + FactionTechUnlocksByItem.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("LootWarmupRequested=" + _lootWarmupRequested);
                lines.Add("LootWarmupActive=" + _lootWarmupActive);
                lines.Add("LootWarmupComplete=" + _lootWarmupComplete);
                lines.Add("LootContainerProfiles=" + _lootContainerProfileCount.ToString(CultureInfo.InvariantCulture));
                lines.Add("LootContainerMappedProfiles=" + _lootContainerMappedProfileCount.ToString(CultureInfo.InvariantCulture));
                lines.Add("LootContainerUnmappedProfiles=" + _lootContainerUnmappedProfileCount.ToString(CultureInfo.InvariantCulture));
                lines.Add("LootContainerUnmappedProfileIds=" + (LootUnmappedContainerProfileIds.Count == 0 ? "<none>" : string.Join(",", LootUnmappedContainerProfileIds.ToArray())));
                lines.Add("LootContainerIndexedProfiles=" + _lootContainerIndexedProfileCount.ToString(CultureInfo.InvariantCulture));
                lines.Add("LootContainerEmptyProfiles=" + _lootContainerEmptyProfileCount.ToString(CultureInfo.InvariantCulture));
                lines.Add("LootContainerDescriptorLinks=" + _lootContainerDescriptorLinkCount.ToString(CultureInfo.InvariantCulture));
                lines.Add("LootContainerItemLinks=" + _lootContainerItemLinkCount.ToString(CultureInfo.InvariantCulture));
                lines.Add("LootMultiProfilePhysicalContainers=" + LootMultiProfilePhysicalContainerIds.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("AmmoWarmupComplete=" + _ammoWarmupComplete);
                lines.Add("AmmoWarmupItemBuffer=" + AmmoWarmupItems.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("AmmoWarmupWeaponBuffer=" + AmmoWarmupWeapons.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("DisassemblyWarmupComplete=" + _disassemblyWarmupComplete);
                lines.Add("DisassemblyWarmupBuffer=" + DisassemblyWarmupItems.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("FactionTechWarmupComplete=" + _factionTechWarmupComplete);
                lines.Add("FactionTechWarmupBuffer=" + FactionTechWarmupFactions.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("");
                lines.Add("[UIAndLocalization]");
                lines.Add("InspectorEnabled=" + InspectorEnabled);
                lines.Add("InspectorOpen=" + _inspectorOpen);
                lines.Add("InspectorHotkey=" + InspectorKeyName);
                lines.Add("InterfaceIconsEnabled=" + ShowInterfaceIcons);
                lines.Add("EnhancedReadability=" + EnhancedReadability);
                lines.Add("InterfaceIconLayoutActive=" + BrowserInterfaceIconLayoutEnabled);
                lines.Add("InterfaceIconBindings=" + BrowserInterfaceIconBindings.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("InterfaceIconSprites=" + BrowserInterfaceIconSprites.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("InspectorItemId=" + (_inspectorItemId ?? string.Empty));
                lines.Add("BrowserTab=" + BrowserNavigation.Tab.ToString(CultureInfo.InvariantCulture));
                lines.Add("BrowserScrollOffset=" + BrowserNavigation.ScrollOffset.ToString(CultureInfo.InvariantCulture));
                lines.Add("CatalogOpen=" + _browserCatalogOpen);
                lines.Add("CatalogScope=" + _browserCatalogScope);
                lines.Add("CatalogDataFilter=" + _browserCatalogDataFilter);
                lines.Add("CatalogSort=" + _browserCatalogSortMode +
                    (_browserCatalogSortDescending ? ":Descending" : ":Ascending"));
                lines.Add("CatalogFavorites=" + BrowserFavoriteItemIds.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("CatalogRecent=" + BrowserRecentItemIds.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("BrowserBackDepth=" + BrowserNavigation.HistoryCount.ToString(CultureInfo.InvariantCulture));
                lines.Add("GameLanguage=" + (UiText.Language ?? string.Empty));
                lines.Add("LocalizationFile=" + (UiText.FileName ?? string.Empty));
                lines.Add("LocalizationEnglishKeys=" + UiText.EnglishCount.ToString(CultureInfo.InvariantCulture));
                lines.Add("LocalizationActiveKeys=" + UiText.ActiveCount.ToString(CultureInfo.InvariantCulture));
                lines.Add("LocalizationMissingKeys=" + UiText.MissingCount.ToString(CultureInfo.InvariantCulture));
                lines.Add("LocalizationDuplicateKeys=" + UiText.Duplicates.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("LocalizationMalformedLines=" + UiText.MalformedLines.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("LocalizationUtf8Failures=" + UiText.Utf8Failures.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("");
                lines.Add("[RequiredData]");
                lines.Add("DistinctFailures=" + RequiredData.DistinctFailures.ToString(CultureInfo.InvariantCulture));
                lines.Add("LimitReached=" + RequiredData.LimitReached);
                lines.Add("");
                lines.Add("[MemoryHygiene]");
                lines.Add("ManagedBytesApprox=" + GC.GetTotalMemory(false).ToString(CultureInfo.InvariantCulture));
                lines.Add("LocalizationCache=" + GameText.KeyCount.ToString(CultureInfo.InvariantCulture));
                lines.Add("ResolvedUiTextCache=" + UiText.ResolvedCount.ToString(CultureInfo.InvariantCulture));
                lines.Add("SuppressedRaycasters=" + SuppressedRaycasters.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("");
                lines.Add("ManualExportHotkey=Ctrl+Shift+F10");
                lines.Add("ReadOnlySelfTestHotkey=" + (ModderMode ? "Ctrl+Shift+F11 (Modder Mode)" : "disabled"));

                if (string.Equals(trigger, "ManualCtrlShiftF10", StringComparison.Ordinal))
                    AppendSelectedEnemyImplantDiagnostics(lines);
                File.WriteAllLines(reportPath, lines.ToArray(), Encoding.UTF8);
                WriteLocalizationHealthReportSafe(true);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ItemIntelligence] Diagnostics export failed: " + ex.Message);
            }
        }

        private static void RefreshBuildFingerprintIfNeeded()
        {
            if (string.IsNullOrEmpty(_buildFingerprint))
                RefreshBuildFingerprint();
        }

        private static void RunReadOnlySelfTestSafe(string trigger)
        {
            try
            {
                Directory.CreateDirectory(ConfigDirectory);
                EnsureRuntimeIndexesReady();
                RunCompatibilityShieldStatic();
                RefreshBuildFingerprint();

                List<string> lines = new List<string>();
                int failures = 0;
                lines.Add("Item Intelligence Safety Regression Self-Test");
                lines.Add("TimestampLocal=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                lines.Add("Trigger=" + (trigger ?? string.Empty));
                lines.Add("ModVersion=" + Version);
                lines.Add("GameVersion=" + (Application.version ?? string.Empty));
                lines.Add("CompatibilityVerdict=" + (_compatibilityVerdict ?? string.Empty));
                lines.Add("");

                AddSelfTest(lines, "OrdinaryReadOnlyPolicy", ReadOnlyKnowledgePolicy, ref failures,
                    "Normal mode has no mutation path; only an explicit MCM Modder Mode spawn action is exempt.");
                AddSelfTest(lines, "ModderSpawnExceptionNarrow", ModderModeExplicitSpawnException, ref failures,
                    "The exception adds one selected item to cargo or clone inventory and does not touch economy/story/factions.");
                AddSelfTest(lines, "CoreCompatibility", _compatCore, ref failures,
                    _compatCore ? "OK" : GetCompatibilityReason("Core"));
                AddSelfTest(lines, "InputGuardCompatibility", _compatInputGuard, ref failures,
                    _compatInputGuard ? "OK" : GetCompatibilityReason("InputGuard"));
                AddSelfTest(lines, "KnownItems", KnownItemIds.Count > 0, ref failures,
                    "Count=" + KnownItemIds.Count.ToString(CultureInfo.InvariantCulture));
                AddSelfTest(lines, "ItemRecords", ItemRecordsById.Count > 0, ref failures,
                    "Count=" + ItemRecordsById.Count.ToString(CultureInfo.InvariantCulture));
                EnsureExternalUiTranslations();
                AddSelfTest(lines, "LocalizationEnglishFallback", UiText.EnglishCount > 0,
                    ref failures, "Keys=" + UiText.EnglishCount.ToString(CultureInfo.InvariantCulture));
                AddSelfTest(lines, "LocalizationUTF8", UiText.Utf8Failures.Count == 0, ref failures,
                    "Failures=" + UiText.Utf8Failures.Count.ToString(CultureInfo.InvariantCulture));
                AddSelfTest(lines, "LocalizationDuplicateKeys", UiText.Duplicates.Count == 0, ref failures,
                    "Duplicates=" + UiText.Duplicates.Count.ToString(CultureInfo.InvariantCulture));
                AddSelfTest(lines, "LocalizationMalformedLines", UiText.MalformedLines.Count == 0, ref failures,
                    "Malformed=" + UiText.MalformedLines.Count.ToString(CultureInfo.InvariantCulture));

                lines.Add("");
                lines.Add("ModuleStatus=" + (_compatibilityVerdict ?? string.Empty));
                lines.Add("Failures=" + failures.ToString(CultureInfo.InvariantCulture));
                lines.Add("Result=" + (failures == 0 ? "PASS" : "CHECK"));
                lines.Add("");
                lines.Add("This self-test is intentionally read-only. It does not automate screen transitions.");
                lines.Add("Use REGRESSION_MATRIX.md for MainMenu/Space/Dungeon/Starmap/Bramfatura manual scenarios.");

                File.WriteAllLines(RegressionSelfTestPath, lines.ToArray(), Encoding.UTF8);
                WriteDiagnosticsReportSafe("SelfTest");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ItemIntelligence] Read-only self-test failed to run: " + ex.Message);
            }
        }

        private static void AddSelfTest(List<string> lines, string name, bool pass, ref int failures, string detail)
        {
            if (!pass) failures++;
            lines.Add(name + "=" + (pass ? "PASS" : "CHECK") +
                (string.IsNullOrEmpty(detail) ? string.Empty : " | " + detail));
        }

        private static void RunConservativeMemoryHygiene(string reason)
        {
            try
            {
                int localizationBefore = GameText.KeyCount;
                int itemDisplayBefore = GameText.ItemCount;
                int perkDisplayBefore = GameText.PerkCount;
                int resolvedBefore = UiText.ResolvedCount;

                // Only transient string/result caches are cleared. Reflection metadata is
                // intentionally retained because it is immutable and expensive to rediscover.
                GameText.ClearResults();
                UiText.ClearResolvedCache();
                BrowserLines.Clear();

                VerboseLog("[ItemIntelligence] Memory hygiene " + (reason ?? string.Empty) +
                    ": LocalizationCache " + localizationBefore.ToString(CultureInfo.InvariantCulture) + "->0" +
                    ", ItemDisplayCache " + itemDisplayBefore.ToString(CultureInfo.InvariantCulture) + "->0" +
                    ", MagnumDisplayCache " + perkDisplayBefore.ToString(CultureInfo.InvariantCulture) + "->0" +
                    ", ResolvedUiTextCache " + resolvedBefore.ToString(CultureInfo.InvariantCulture) + "->0.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ItemIntelligence] Memory hygiene checkpoint failed: " + ex.Message);
            }
        }

        private static void WriteLocalizationHealthReportSafe(bool force)
        {
            if (!UiText.HealthDirty && !force) return;
            if (!force && !ModderMode && !UiText.HasLocalizationHealthProblems())
            {
                UiText.AcknowledgeHealth();
                return;
            }
            try
            {
                Directory.CreateDirectory(ConfigDirectory);
                List<string> lines = new List<string>();
                lines.Add("Item Intelligence Localization Health");
                lines.Add("ModVersion=" + Version);
                lines.Add("GameLanguage=" + (UiText.Language ?? string.Empty));
                lines.Add("ActiveFile=" + (UiText.FileName ?? string.Empty));
                lines.Add("EnglishFallbackKeys=" + UiText.EnglishCount.ToString(CultureInfo.InvariantCulture));
                lines.Add("ActiveKeys=" + UiText.ActiveCount.ToString(CultureInfo.InvariantCulture));
                lines.Add("MissingKeys=" + UiText.MissingCount.ToString(CultureInfo.InvariantCulture));
                lines.Add("DuplicateKeys=" + UiText.Duplicates.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("MalformedLines=" + UiText.MalformedLines.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("Utf8Failures=" + UiText.Utf8Failures.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("");

                AppendHealthSection(lines, "Missing", UiText.MissingKeys);
                AppendHealthSection(lines, "Duplicates", UiText.Duplicates);
                AppendHealthSection(lines, "Malformed", UiText.MalformedLines);
                AppendHealthSection(lines, "UTF8", UiText.Utf8Failures);

                File.WriteAllLines(LocalizationHealthReportPath, lines.ToArray(), Encoding.UTF8);
                UiText.AcknowledgeHealth();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ItemIntelligence] Localization health report write failed: " + ex.Message);
            }
        }

        private static void AppendHealthSection(List<string> lines, string name, IEnumerable<string> values)
        {
            if (lines == null) return;
            lines.Add("[" + (name ?? string.Empty) + "]");
            if (values != null)
            {
                foreach (string value in values)
                    lines.Add(value ?? string.Empty);
            }
            lines.Add("");
        }
    }
}
