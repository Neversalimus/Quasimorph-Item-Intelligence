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
        private const string LastVerifiedGameVersion = "1.0.2.573s.9f33900";
        private static string _buildFingerprint = string.Empty;
        private static string _compatibilityVerdict = "UNKNOWN";
        private static int _diagnosticsHotkeyLastFrame = -1000;
        private static int _diagnosticsHotkeyConsumedFrame = -1000;

        private static readonly List<string> LocalizationHealthDuplicateKeys = new List<string>();
        private static readonly List<string> LocalizationHealthMalformedLines = new List<string>();
        private static readonly List<string> LocalizationHealthUtf8Failures = new List<string>();
        private static bool _localizationHealthDirty = true;

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

                Debug.Log("[ItemIntelligence] Build fingerprint: " + _buildFingerprint +
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
                    Debug.Log("[ItemIntelligence] Diagnostics exported: " + DiagnosticsReportPath);
                }
                else if (Input.GetKeyDown(KeyCode.F11))
                {
                    _diagnosticsHotkeyLastFrame = frame;
                    _diagnosticsHotkeyConsumedFrame = frame;
                    RunReadOnlySelfTestSafe("ManualCtrlShiftF11");
                    Debug.Log("[ItemIntelligence] Read-only self-test exported: " + RegressionSelfTestPath);
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
                lines.Add("LootContainerFallbackProfiles=" + _lootContainerFallbackProfileCount.ToString(CultureInfo.InvariantCulture));
                lines.Add("LootContainerFallbackProfileIds=" + (LootFallbackContainerProfileIds.Count == 0 ? "<none>" : string.Join(",", LootFallbackContainerProfileIds.ToArray())));
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
                lines.Add("InterfaceIconLayoutActive=" + BrowserInterfaceIconLayoutEnabled);
                lines.Add("InterfaceIconBindings=" + BrowserInterfaceIconBindings.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("InterfaceIconSprites=" + BrowserInterfaceIconSprites.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("InspectorItemId=" + (_inspectorItemId ?? string.Empty));
                lines.Add("BrowserTab=" + _browserTab.ToString(CultureInfo.InvariantCulture));
                lines.Add("BrowserPage=" + _browserPage.ToString(CultureInfo.InvariantCulture));
                lines.Add("CatalogOpen=" + _browserCatalogOpen);
                lines.Add("CatalogScope=" + _browserCatalogScope);
                lines.Add("CatalogDataFilter=" + _browserCatalogDataFilter);
                lines.Add("CatalogSort=" + _browserCatalogSortMode +
                    (_browserCatalogSortDescending ? ":Descending" : ":Ascending"));
                lines.Add("CatalogFavorites=" + BrowserFavoriteItemIds.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("CatalogRecent=" + BrowserRecentItemIds.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("BrowserBackDepth=" + BrowserItemNavigationHistory.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("GameLanguage=" + (_externalUiTranslationLanguage ?? string.Empty));
                lines.Add("LocalizationFile=" + (_externalUiTranslationFile ?? string.Empty));
                lines.Add("LocalizationEnglishKeys=" + EnglishUiFallback.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("LocalizationActiveKeys=" + ExternalUiTranslations.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("LocalizationMissingKeys=" + MissingUiTranslationKeys.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("LocalizationDuplicateKeys=" + LocalizationHealthDuplicateKeys.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("LocalizationMalformedLines=" + LocalizationHealthMalformedLines.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("LocalizationUtf8Failures=" + LocalizationHealthUtf8Failures.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("");
                lines.Add("[MemoryHygiene]");
                lines.Add("ManagedBytesApprox=" + GC.GetTotalMemory(false).ToString(CultureInfo.InvariantCulture));
                lines.Add("LocalizationCache=" + LocalizationCache.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("ResolvedUiTextCache=" + ResolvedUiTextCache.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("QuickTooltipPools=" + QuickTooltipPools.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("SuppressedRaycasters=" + SuppressedRaycasters.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("");
                lines.Add("ManualExportHotkey=Ctrl+Shift+F10");
                lines.Add("ReadOnlySelfTestHotkey=Ctrl+Shift+F11");

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
                lines.Add("Item Intelligence Read-Only Regression Self-Test");
                lines.Add("TimestampLocal=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                lines.Add("Trigger=" + (trigger ?? string.Empty));
                lines.Add("ModVersion=" + Version);
                lines.Add("GameVersion=" + (Application.version ?? string.Empty));
                lines.Add("CompatibilityVerdict=" + (_compatibilityVerdict ?? string.Empty));
                lines.Add("");

                AddSelfTest(lines, "ReadOnlyPolicy", ReadOnlyKnowledgePolicy, ref failures,
                    "No inventory/economy/story/faction mutation paths are part of Item Intelligence.");
                AddSelfTest(lines, "CoreCompatibility", _compatCore, ref failures,
                    _compatCore ? "OK" : GetCompatibilityReason("Core"));
                AddSelfTest(lines, "InputGuardCompatibility", _compatInputGuard, ref failures,
                    _compatInputGuard ? "OK" : GetCompatibilityReason("InputGuard"));
                AddSelfTest(lines, "KnownItems", KnownItemIds.Count > 0, ref failures,
                    "Count=" + KnownItemIds.Count.ToString(CultureInfo.InvariantCulture));
                AddSelfTest(lines, "ItemRecords", ItemRecordsById.Count > 0, ref failures,
                    "Count=" + ItemRecordsById.Count.ToString(CultureInfo.InvariantCulture));
                AddSelfTest(lines, "LocalizationEnglishFallback", EnglishUiFallback.Count > 0 || !_externalUiEnglishLoaded,
                    ref failures, "Keys=" + EnglishUiFallback.Count.ToString(CultureInfo.InvariantCulture));
                AddSelfTest(lines, "LocalizationUTF8", LocalizationHealthUtf8Failures.Count == 0, ref failures,
                    "Failures=" + LocalizationHealthUtf8Failures.Count.ToString(CultureInfo.InvariantCulture));
                AddSelfTest(lines, "LocalizationDuplicateKeys", LocalizationHealthDuplicateKeys.Count == 0, ref failures,
                    "Duplicates=" + LocalizationHealthDuplicateKeys.Count.ToString(CultureInfo.InvariantCulture));
                AddSelfTest(lines, "LocalizationMalformedLines", LocalizationHealthMalformedLines.Count == 0, ref failures,
                    "Malformed=" + LocalizationHealthMalformedLines.Count.ToString(CultureInfo.InvariantCulture));

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
                int localizationBefore = LocalizationCache.Count;
                int resolvedBefore = ResolvedUiTextCache.Count;

                // Only transient string/result caches are cleared. Reflection metadata is
                // intentionally retained because it is immutable and expensive to rediscover.
                LocalizationCache.Clear();
                ResolvedUiTextCache.Clear();
                MissingUiTranslationKeys.Clear();
                BrowserLines.Clear();
                PruneDeadQuickTooltipPools();

                Debug.Log("[ItemIntelligence] Memory hygiene " + (reason ?? string.Empty) +
                    ": LocalizationCache " + localizationBefore.ToString(CultureInfo.InvariantCulture) + "->0" +
                    ", ResolvedUiTextCache " + resolvedBefore.ToString(CultureInfo.InvariantCulture) + "->0" +
                    ", QuickTooltipPools=" + QuickTooltipPools.Count.ToString(CultureInfo.InvariantCulture) + ".");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ItemIntelligence] Memory hygiene checkpoint failed: " + ex.Message);
            }
        }

        private static string[] ReadUtf8LinesStrict(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return new string[0];
            try
            {
                UTF8Encoding strict = new UTF8Encoding(false, true);
                string text = File.ReadAllText(path, strict);
                text = text.Replace("\r\n", "\n").Replace('\r', '\n');
                return text.Split(new char[] { '\n' });
            }
            catch (DecoderFallbackException ex)
            {
                RecordLocalizationUtf8Failure(path, ex.Message);
                throw;
            }
        }

        private static void ResetLocalizationHealthForReload()
        {
            LocalizationHealthDuplicateKeys.Clear();
            LocalizationHealthMalformedLines.Clear();
            LocalizationHealthUtf8Failures.Clear();
            _localizationHealthDirty = true;
        }

        private static void RecordLocalizationDuplicateKey(string path, string key)
        {
            string value = Path.GetFileName(path ?? string.Empty) + ":" + (key ?? string.Empty);
            if (!LocalizationHealthDuplicateKeys.Contains(value))
                LocalizationHealthDuplicateKeys.Add(value);
            _localizationHealthDirty = true;
            Debug.LogWarning("[ItemIntelligence] Localization duplicate key: " + value);
        }

        private static void RecordLocalizationMalformedLine(string path, int line)
        {
            string value = Path.GetFileName(path ?? string.Empty) + ":line " + line.ToString(CultureInfo.InvariantCulture);
            if (!LocalizationHealthMalformedLines.Contains(value))
                LocalizationHealthMalformedLines.Add(value);
            _localizationHealthDirty = true;
            Debug.LogWarning("[ItemIntelligence] Localization malformed line: " + value);
        }

        private static void RecordLocalizationUtf8Failure(string path, string detail)
        {
            string value = Path.GetFileName(path ?? string.Empty) + ":" + (detail ?? "invalid UTF-8");
            if (!LocalizationHealthUtf8Failures.Contains(value))
                LocalizationHealthUtf8Failures.Add(value);
            _localizationHealthDirty = true;
            Debug.LogWarning("[ItemIntelligence] Localization file is not valid UTF-8: " + value);
        }

        private static void MarkLocalizationHealthDirty()
        {
            _localizationHealthDirty = true;
        }

        private static bool HasLocalizationHealthProblems()
        {
            return MissingUiTranslationKeys.Count > 0 ||
                LocalizationHealthDuplicateKeys.Count > 0 ||
                LocalizationHealthMalformedLines.Count > 0 ||
                LocalizationHealthUtf8Failures.Count > 0;
        }

        private static void WriteLocalizationHealthReportSafe(bool force)
        {
            if (!_localizationHealthDirty && !force) return;
            if (!force && !ModderMode && !HasLocalizationHealthProblems())
            {
                _localizationHealthDirty = false;
                return;
            }
            try
            {
                Directory.CreateDirectory(ConfigDirectory);
                List<string> lines = new List<string>();
                lines.Add("Item Intelligence Localization Health");
                lines.Add("ModVersion=" + Version);
                lines.Add("GameLanguage=" + (_externalUiTranslationLanguage ?? string.Empty));
                lines.Add("ActiveFile=" + (_externalUiTranslationFile ?? string.Empty));
                lines.Add("EnglishFallbackKeys=" + EnglishUiFallback.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("ActiveKeys=" + ExternalUiTranslations.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("MissingKeys=" + MissingUiTranslationKeys.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("DuplicateKeys=" + LocalizationHealthDuplicateKeys.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("MalformedLines=" + LocalizationHealthMalformedLines.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("Utf8Failures=" + LocalizationHealthUtf8Failures.Count.ToString(CultureInfo.InvariantCulture));
                lines.Add("");

                AppendHealthSection(lines, "Missing", MissingUiTranslationKeys);
                AppendHealthSection(lines, "Duplicates", LocalizationHealthDuplicateKeys);
                AppendHealthSection(lines, "Malformed", LocalizationHealthMalformedLines);
                AppendHealthSection(lines, "UTF8", LocalizationHealthUtf8Failures);

                File.WriteAllLines(LocalizationHealthReportPath, lines.ToArray(), Encoding.UTF8);
                _localizationHealthDirty = false;
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
