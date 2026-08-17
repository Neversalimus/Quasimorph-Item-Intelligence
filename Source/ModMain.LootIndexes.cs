using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using MGSC;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // Test13 index owner: incremental warmup, reverse-index builders and runtime source lookup.

        // Owner state: reverse indexes, incremental warmup cursors and reflection handles.
        // The reverse indexes remain lazy and incrementally warmed. No Loot table is
        // scanned from hover/render code.
        private static readonly Dictionary<string, List<LootContainerSource>> LootContainerSourcesByItem =
            new Dictionary<string, List<LootContainerSource>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, List<LootEnemySource>> LootEnemySourcesByItem =
            new Dictionary<string, List<LootEnemySource>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, List<LootAmputationSource>> LootAmputationSourcesByItem =
            new Dictionary<string, List<LootAmputationSource>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, List<LootItemMeta>> LootItemsByItemClass =
            new Dictionary<string, List<LootItemMeta>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, List<LootItemMeta>> LootItemsByWeaponClass =
            new Dictionary<string, List<LootItemMeta>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, List<LootItemMeta>> LootItemsByArmorClass =
            new Dictionary<string, List<LootItemMeta>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, List<LootItemMeta>> LootImplantsByAugmentationClass =
            new Dictionary<string, List<LootItemMeta>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, List<LootItemMeta>> LootAugmentationsByAugmentationClass =
            new Dictionary<string, List<LootItemMeta>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, List<LootItemMeta>> LootAugmentationsByRecordId =
            new Dictionary<string, List<LootItemMeta>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Dictionary<string, int>> LootEnemyMinSpawnTechByFaction =
            new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<string> LootEnemyFactionIds = new List<string>();
        private static bool _lootEnemyContextIndexReady;
        private static System.IO.StringReader _lootEnemyContextReader;
        private static bool _lootEnemyContextParseStarted;
        private static bool _lootEnemyContextInTable;
        private static int _lootEnemyContextParsedRows;
        private static readonly Dictionary<string, List<LootMissionSource>> LootBramfaturaSourcesByItem =
            new Dictionary<string, List<LootMissionSource>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, List<LootMissionSource>> LootStationTypeSourcesByItem =
            new Dictionary<string, List<LootMissionSource>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, List<LootMissionSource>> LootFactionSourcesByItem =
            new Dictionary<string, List<LootMissionSource>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, List<LootContainerDescriptor>> LootContainerDescriptorsByDropId =
            new Dictionary<string, List<LootContainerDescriptor>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, LootItemMeta> LootItemMetaById =
            new Dictionary<string, LootItemMeta>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, List<LootItemMeta>> LootItemsByCategory =
            new Dictionary<string, List<LootItemMeta>>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<string> LootWarmupItemIds = new List<string>();
        private static readonly List<string> LootWarmupContainerDropIds = new List<string>();
        private static readonly List<DataEntry> LootWarmupMobClasses = new List<DataEntry>();
        private static readonly List<DataEntry> LootWarmupBramfaturas = new List<DataEntry>();
        private static readonly List<DataEntry> LootWarmupStationTypes = new List<DataEntry>();
        private static readonly List<DataEntry> LootWarmupFactions = new List<DataEntry>();
        private static readonly List<DataEntry> LootAmputationWarmupSlots = new List<DataEntry>();
        private static int _lootAmputationWarmupIndex;
        private static bool _lootAmputationWarmupStarted;
        private static readonly System.Diagnostics.Stopwatch LootWarmupFrameTimer =
            new System.Diagnostics.Stopwatch();
        private static int _lootWarmupPhase;
        private static int _lootWarmupIndex;
        private static int _lootWarmupProcessed;
        private static int _lootWarmupTotal;
        private static bool _lootWarmupActive;
        private static bool _lootWarmupComplete;
        private static bool _lootWarmupRequested;
        private static int _lootWarmupNextFrame;
        private static DataEntry _lootMobWorkEntry;
        private static object _lootMobWorkRecord;
        private static string _lootMobWorkId = string.Empty;
        private static object _lootMobWorkWhitelist;
        private static List<EnemyLootContext> _lootMobWorkContexts;
        private static int _lootMobWorkAmmoMin;
        private static int _lootMobWorkAmmoMax;
        private static int _lootMobWorkStage;
        private static object _lootContainerDropCollection;
        private static MethodInfo _lootContainerGetDropMethod;
        private static MethodInfo _lootContainerGetDropBiomesMethod;
        private static void ResolveLootContainerMethods()
        {
            if (_lootContainerDropCollection == null) return;
            if (_lootContainerGetDropMethod != null &&
                _lootContainerGetDropBiomesMethod != null)
                return;

            MethodInfo[] methods =
                _lootContainerDropCollection.GetType().GetMethods(InstanceFlags);

            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (method == null) continue;
                ParameterInfo[] p = method.GetParameters();

                if (string.Equals(method.Name, "GetDrop", StringComparison.Ordinal) &&
                    p != null && p.Length == 2 &&
                    p[0].ParameterType == typeof(string) &&
                    p[1].ParameterType == typeof(string))
                    _lootContainerGetDropMethod = method;

                if (string.Equals(method.Name, "GetDropBiomes", StringComparison.Ordinal) &&
                    p != null && p.Length == 1 &&
                    p[0].ParameterType == typeof(string))
                    _lootContainerGetDropBiomesMethod = method;
            }

            if (_lootContainerGetDropMethod == null ||
                _lootContainerGetDropBiomesMethod == null)
                throw new MissingMethodException(
                    "ContainerItemDrop GetDrop/GetDropBiomes API not found.");
        }

        private static void EnsureLootWarmupStarted()
        {
            if (_lootWarmupComplete || _lootWarmupActive || _lootWarmupRequested)
                return;

            // Container visuals are resolved independently and lazily per visible
            // container row. Loot warmup must never scan visual resources up front.
            _lootWarmupRequested = true;
            if (_lootWarmupTotal <= 0)
            {
                _lootWarmupComplete = true;
                return;
            }

            _lootWarmupActive = true;
            _lootWarmupNextFrame = Time.frameCount;
            Debug.Log("[ItemIntelligence] Loot Sources warmup started on demand from Loot tab.");
        }

        private static void TickLootSourcesWarmup()
        {
            if (!_lootWarmupActive) return;

            // Do not spend gameplay frames on the expensive enemy reverse index. Once
            // requested, it advances only while the Loot tab is actually visible and
            // pauses immediately when F2 is closed or another tab is selected.
            if (!_inspectorOpen || _browserTab != (int)BrowserTabId.Loot) return;
            if (Time.frameCount < _lootWarmupNextFrame) return;

            // Container sources are shown first so the tab becomes useful almost
            // immediately. v1.7.10 keeps the real CPU-time budget instead of an artificially
            // tiny fixed operation count: fast CPUs can complete many cheap slices per
            // frame, while the stopwatch still caps the work to protect frame pacing.
            LootWarmupFrameTimer.Reset();
            LootWarmupFrameTimer.Start();
            System.Diagnostics.Stopwatch lootFrameTimer = LootWarmupFrameTimer;
            int budget = (_lootWarmupPhase == 1 || _lootWarmupPhase == 3) ? 64 : 12;
            double frameBudgetMs = _lootWarmupPhase == 3 ? 1.25 : 1.00;
            while (budget-- > 0 && _lootWarmupActive &&
                   lootFrameTimer.Elapsed.TotalMilliseconds < frameBudgetMs)
            {
                if (_lootWarmupPhase == 0)
                {
                    if (_lootWarmupIndex < LootWarmupContainerDropIds.Count)
                    {
                        IndexLootContainerDrop(LootWarmupContainerDropIds[_lootWarmupIndex++]);
                        _lootWarmupProcessed++;
                        continue;
                    }
                    AdvanceLootWarmupPhase();
                    break;
                }

                if (_lootWarmupPhase == 1)
                {
                    if (_lootWarmupIndex < LootWarmupItemIds.Count)
                    {
                        IndexLootItemMeta(LootWarmupItemIds[_lootWarmupIndex++]);
                        _lootWarmupProcessed++;
                        continue;
                    }
                    AdvanceLootWarmupPhase();
                    break;
                }

                if (_lootWarmupPhase == 2)
                {
                    bool completed = TickEnemyLootSpawnContextIndexSlice(lootFrameTimer, frameBudgetMs);
                    if (completed)
                    {
                        _lootWarmupProcessed++;
                        AdvanceLootWarmupPhase();
                    }
                    // The parser owns the remainder of this frame's Loot budget.
                    break;
                }

                if (_lootWarmupPhase == 3)
                {
                    if (_lootWarmupIndex < LootWarmupMobClasses.Count)
                    {
                        bool completedMob =
                            TickLootMobClassSlice(LootWarmupMobClasses[_lootWarmupIndex]);
                        // Progress is measured per actual enemy sub-step so the bar moves
                        // smoothly through the expensive phase instead of stalling on a mob.
                        _lootWarmupProcessed++;
                        if (completedMob)
                            _lootWarmupIndex++;
                        continue;
                    }
                    ResetLootMobWork();
                    AdvanceLootWarmupPhase();
                    continue;
                }

                if (_lootWarmupPhase == 4)
                {
                    if (TickLootAmputationIndexSlice())
                    {
                        _lootWarmupProcessed++;
                        AdvanceLootWarmupPhase();
                    }
                    continue;
                }

                if (_lootWarmupPhase == 5)
                {
                    if (_lootWarmupIndex < LootWarmupBramfaturas.Count)
                    {
                        IndexLootMissionCategories(LootWarmupBramfaturas[_lootWarmupIndex++], "Bramfatura");
                        _lootWarmupProcessed++;
                        continue;
                    }
                    AdvanceLootWarmupPhase();
                    continue;
                }

                if (_lootWarmupPhase == 6)
                {
                    if (_lootWarmupIndex < LootWarmupStationTypes.Count)
                    {
                        IndexLootMissionCategories(LootWarmupStationTypes[_lootWarmupIndex++], "StationType");
                        _lootWarmupProcessed++;
                        continue;
                    }
                    AdvanceLootWarmupPhase();
                    continue;
                }

                if (_lootWarmupPhase == 7)
                {
                    if (_lootWarmupIndex < LootWarmupFactions.Count)
                    {
                        IndexLootMissionCategories(LootWarmupFactions[_lootWarmupIndex++], "Faction");
                        _lootWarmupProcessed++;
                        continue;
                    }
                    AdvanceLootWarmupPhase();
                    continue;
                }

                if (_lootWarmupPhase == 8)
                {
                    if (TickLootGeneralSpawnIndexSlice())
                    {
                        _lootWarmupProcessed++;
                        AdvanceLootWarmupPhase();
                    }
                    continue;
                }

                FinishLootSourcesWarmup();
            }

            _lootWarmupNextFrame = Time.frameCount + 1;
        }

        private static void AdvanceLootWarmupPhase()
        {
            _lootWarmupPhase++;
            _lootWarmupIndex = 0;
            if (_lootWarmupPhase > 8)
                FinishLootSourcesWarmup();
        }

        private static void FinishLootSourcesWarmup()
        {
            _lootWarmupProcessed = Math.Max(_lootWarmupProcessed, _lootWarmupTotal);
            _lootWarmupActive = false;
            _lootWarmupComplete = true;

            int enemyLinks = 0;
            foreach (KeyValuePair<string, List<LootEnemySource>> pair in LootEnemySourcesByItem)
                if (pair.Value != null) enemyLinks += pair.Value.Count;

            Debug.Log(
                "[ItemIntelligence] Loot Sources warmup complete: metadata=" +
                LootItemMetaById.Count +
                ", items(container/enemy/amputation/bramfatura/station/faction)=" +
                LootContainerSourcesByItem.Count + "/" +
                LootEnemySourcesByItem.Count + "/" +
                LootAmputationSourcesByItem.Count + "/" +
                LootBramfaturaSourcesByItem.Count + "/" +
                LootStationTypeSourcesByItem.Count + "/" +
                LootFactionSourcesByItem.Count +
                ", enemyLinks=" + enemyLinks.ToString(CultureInfo.InvariantCulture) +
                ", containerProfilesIndexed=" +
                _lootContainerIndexedProfileCount.ToString(CultureInfo.InvariantCulture) + "/" +
                _lootContainerProfileCount.ToString(CultureInfo.InvariantCulture) +
                ", emptyContainerProfiles=" +
                _lootContainerEmptyProfileCount.ToString(CultureInfo.InvariantCulture) +
                ", containerItemLinks=" +
                _lootContainerItemLinkCount.ToString(CultureInfo.InvariantCulture) +
                ", generalSpawnItems=" +
                LootGeneralSpawnContainersByItem.Count.ToString(CultureInfo.InvariantCulture) +
                ", generalSpawnPairs=" +
                _lootGeneralSpawnPairCount.ToString(CultureInfo.InvariantCulture) + ".");

            if (_inspectorOpen && _browserTab == (int)BrowserTabId.Loot)
                RenderBrowser(_inspectorItemId);
        }

        private static void IndexLootContainerDrop(string dropId)
        {
            if (string.IsNullOrEmpty(dropId) ||
                _lootContainerDropCollection == null)
                return;

            ResolveLootContainerMethods();

            object rawBiomes = _lootContainerGetDropBiomesMethod.Invoke(
                _lootContainerDropCollection,
                new object[] { dropId });

            IEnumerable biomes = rawBiomes as IEnumerable;
            if (biomes == null || rawBiomes is string)
            {
                RecordLootContainerProfileIndexResult(false);
                return;
            }

            int biomeCount = 0;
            bool hasWeightedData = false;
            foreach (object rawBiome in biomes)
            {
                if (++biomeCount > 256) break;
                string biomeId = ConvertToStableString(rawBiome);
                if (string.IsNullOrEmpty(biomeId)) continue;

                object rawDrop = _lootContainerGetDropMethod.Invoke(
                    _lootContainerDropCollection,
                    new object[] { dropId, biomeId });

                IEnumerable entries = rawDrop as IEnumerable;
                if (entries == null || rawDrop is string) continue;

                List<LootWeightedItem> parsed = new List<LootWeightedItem>();
                double totalWeight = 0.0;
                int scanned = 0;

                foreach (object entry in entries)
                {
                    if (++scanned > 4096) break;
                    if (entry == null) continue;

                    double weight;
                    if (!TryToDoubleSafe(GetMember(entry, "Item1"), out weight) ||
                        weight <= 0.0)
                        continue;

                    string itemId = FirstNonEmpty(
                        GetStringMember(entry, "Item2"),
                        GetStringMember(entry, "Value"));
                    if (string.IsNullOrEmpty(itemId) ||
                        !KnownItemIds.Contains(itemId))
                        continue;

                    parsed.Add(new LootWeightedItem(itemId, weight));
                    totalWeight += weight;
                }

                if (totalWeight <= 0.0) continue;
                hasWeightedData = true;

                List<LootContainerDescriptor> descriptors;
                if (!LootContainerDescriptorsByDropId.TryGetValue(
                        dropId,
                        out descriptors) ||
                    descriptors == null ||
                    descriptors.Count == 0)
                {
                    descriptors = new List<LootContainerDescriptor>();
                    descriptors.Add(new LootContainerDescriptor(
                        dropId,
                        dropId,
                        0,
                        0,
                        false));
                }

                for (int p = 0; p < parsed.Count; p++)
                {
                    LootWeightedItem item = parsed[p];
                    float percent = (float)(item.Weight / totalWeight * 100.0);

                    for (int d = 0; d < descriptors.Count; d++)
                    {
                        LootContainerDescriptor descriptor = descriptors[d];
                        AddLootContainerSource(
                            item.ItemId,
                            new LootContainerSource(
                                descriptor.ContainerId,
                                dropId,
                                biomeId,
                                percent,
                                descriptor.MinRolls,
                                descriptor.MaxRolls,
                                descriptor.RollRangeResolved));
                    }
                }
            }
            RecordLootContainerProfileIndexResult(hasWeightedData);
        }

        private static void AddLootContainerSource(
            string itemId,
            LootContainerSource source)
        {
            if (string.IsNullOrEmpty(itemId) || source == null) return;

            List<LootContainerSource> list;
            if (!LootContainerSourcesByItem.TryGetValue(itemId, out list))
            {
                list = new List<LootContainerSource>();
                LootContainerSourcesByItem[itemId] = list;
            }

            for (int i = 0; i < list.Count; i++)
            {
                LootContainerSource existing = list[i];
                if (existing != null &&
                    string.Equals(existing.ContainerId, source.ContainerId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(existing.DropId, source.DropId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(existing.BiomeId, source.BiomeId, StringComparison.OrdinalIgnoreCase))
                {
                    if (!existing.RollRangeResolved && source.RollRangeResolved) list[i] = source;
                    return;
                }
            }

            list.Add(source);
            _lootContainerItemLinkCount++;
        }

        private static void ResetEnemyLootSpawnContextSlice()
        {
            if (_lootEnemyContextReader != null)
            {
                try { _lootEnemyContextReader.Dispose(); }
                catch { }
            }
            _lootEnemyContextReader = null;
            _lootEnemyContextParseStarted = false;
            _lootEnemyContextInTable = false;
            _lootEnemyContextParsedRows = 0;
        }

        private static bool TickEnemyLootSpawnContextIndexSlice(
            System.Diagnostics.Stopwatch frameTimer, double frameBudgetMs)
        {
            if (!_lootEnemyContextParseStarted)
            {
                LootEnemyMinSpawnTechByFaction.Clear();
                LootEnemyFactionIds.Clear();
                _lootEnemyContextIndexReady = false;
                _lootEnemyContextInTable = false;
                _lootEnemyContextParsedRows = 0;

                for (int i = 0; i < LootWarmupFactions.Count; i++)
                {
                    DataEntry entry = LootWarmupFactions[i];
                    if (entry == null || entry.Value == null) continue;
                    string factionId = FirstNonEmpty(
                        GetStringMember(entry.Value, "Id"),
                        GetStringMember(entry.Value, "FactionId"),
                        entry.Key);
                    if (!string.IsNullOrEmpty(factionId) &&
                        !LootEnemyFactionIds.Contains(factionId))
                        LootEnemyFactionIds.Add(factionId);
                }

                TextAsset asset = FindRuntimeConfigTextAsset("config_units_drops");
                if (asset == null || string.IsNullOrEmpty(asset.text))
                {
                    Debug.LogWarning("[ItemIntelligence] Enemy loot spawn-context table not found; mob defaults will be used.");
                    ResetEnemyLootSpawnContextSlice();
                    return true;
                }

                _lootEnemyContextReader = new System.IO.StringReader(asset.text);
                _lootEnemyContextParseStarted = true;
            }

            if (_lootEnemyContextReader == null)
                return true;

            try
            {
                int lineBudget = 384;
                while (lineBudget-- > 0 &&
                       (frameTimer == null || frameTimer.Elapsed.TotalMilliseconds < frameBudgetMs))
                {
                    string line = _lootEnemyContextReader.ReadLine();
                    if (line == null)
                    {
                        _lootEnemyContextIndexReady = _lootEnemyContextParsedRows > 0;
                        Debug.Log("[ItemIntelligence] Enemy loot spawn contexts: rows=" +
                            _lootEnemyContextParsedRows.ToString(CultureInfo.InvariantCulture) +
                            ", mobs=" + LootEnemyMinSpawnTechByFaction.Count.ToString(CultureInfo.InvariantCulture) +
                            ", factions=" + LootEnemyFactionIds.Count.ToString(CultureInfo.InvariantCulture) +
                            "; parser=time-sliced.");
                        ResetEnemyLootSpawnContextSlice();
                        return true;
                    }

                    if (string.IsNullOrEmpty(line)) continue;

                    if (line.StartsWith("TechLevel\t", StringComparison.OrdinalIgnoreCase))
                    {
                        _lootEnemyContextInTable = true;
                        continue;
                    }
                    if (line[0] == '#')
                    {
                        _lootEnemyContextInTable = false;
                        continue;
                    }
                    if (!_lootEnemyContextInTable) continue;

                    string[] columns = line.Split('\t');
                    if (columns.Length < 5) continue;

                    int unlockTech;
                    if (!int.TryParse(columns[0].Trim(), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out unlockTech))
                        continue;
                    unlockTech = Math.Max(1, unlockTech);

                    List<string> mobIds = new List<string>();
                    AddWeightedIdField(mobIds, columns[2]);
                    AddWeightedIdField(mobIds, columns[3]);
                    if (mobIds.Count == 0) continue;

                    List<string> allowedFactions = SplitStableIds(columns[4]);
                    if (allowedFactions.Count > 0)
                    {
                        for (int f = 0; f < allowedFactions.Count; f++)
                        {
                            string factionId = allowedFactions[f];
                            if (!string.IsNullOrEmpty(factionId) &&
                                !LootEnemyFactionIds.Contains(factionId))
                                LootEnemyFactionIds.Add(factionId);
                        }
                    }

                    for (int m = 0; m < mobIds.Count; m++)
                    {
                        string mobId = mobIds[m];
                        if (string.IsNullOrEmpty(mobId)) continue;

                        if (allowedFactions.Count > 0)
                        {
                            for (int f = 0; f < allowedFactions.Count; f++)
                                AddEnemyLootSpawnTech(mobId, allowedFactions[f], unlockTech);
                        }
                        else if (LootEnemyFactionIds.Count > 0)
                        {
                            for (int f = 0; f < LootEnemyFactionIds.Count; f++)
                                AddEnemyLootSpawnTech(mobId, LootEnemyFactionIds[f], unlockTech);
                        }
                        else
                        {
                            AddEnemyLootSpawnTech(mobId, string.Empty, unlockTech);
                        }
                    }

                    _lootEnemyContextParsedRows++;
                }

                return false;
            }
            catch (Exception ex)
            {
                LootEnemyMinSpawnTechByFaction.Clear();
                _lootEnemyContextIndexReady = false;
                Debug.LogWarning("[ItemIntelligence] Enemy loot context parse failed: " + ex.Message);
                ResetEnemyLootSpawnContextSlice();
                return true;
            }
        }

        private static void AddEnemyLootSpawnTech(string mobId, string factionId, int tech)
        {
            if (string.IsNullOrEmpty(mobId)) return;
            Dictionary<string, int> byFaction;
            if (!LootEnemyMinSpawnTechByFaction.TryGetValue(mobId, out byFaction))
            {
                byFaction = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                LootEnemyMinSpawnTechByFaction[mobId] = byFaction;
            }

            string key = factionId ?? string.Empty;
            int current;
            if (!byFaction.TryGetValue(key, out current) || tech < current)
                byFaction[key] = Math.Max(1, tech);
        }

        private static List<EnemyLootContext> BuildEnemyLootContexts(string mobClassId, object mobRecord)
        {
            List<EnemyLootContext> result = new List<EnemyLootContext>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int maxTech = 10;
            try { if (Data.Global != null) maxTech = Math.Max(1, Data.Global.MaxTechLevel); }
            catch { }

            int bonus = 0;
            TryToInt(GetMember(mobRecord, "EquipmentTechLevelBonus"), out bonus);
            string defaultFaction = GetStringMember(mobRecord, "DefaultItemFactionTag") ?? string.Empty;

            Dictionary<string, int> byFaction;
            if (LootEnemyMinSpawnTechByFaction.TryGetValue(mobClassId, out byFaction) &&
                byFaction != null && byFaction.Count > 0)
            {
                foreach (KeyValuePair<string, int> pair in byFaction)
                {
                    string factionId = string.IsNullOrEmpty(pair.Key) ? defaultFaction : pair.Key;
                    int minTech = Math.Max(1, Math.Min(maxTech, pair.Value));
                    for (int tech = minTech; tech <= maxTech; tech++)
                        AddEnemyLootContext(result, seen, factionId, tech, bonus, maxTech);
                }
            }

            // Scripted/special spawns can call SpawnMonsterFromMobClass outside the
            // location unit tables. The game falls back to DefaultItemFactionTag when
            // factionTag is empty, so retain that real generation route too.
            if (!string.IsNullOrEmpty(defaultFaction))
            {
                for (int tech = 1; tech <= maxTech; tech++)
                    AddEnemyLootContext(result, seen, defaultFaction, tech, bonus, maxTech);
            }

            if (result.Count == 0)
            {
                for (int tech = 1; tech <= maxTech; tech++)
                    AddEnemyLootContext(result, seen, defaultFaction, tech, bonus, maxTech);
            }

            return result;
        }

        private static void AddEnemyLootContext(
            List<EnemyLootContext> result,
            HashSet<string> seen,
            string factionId,
            int techLimit,
            int equipmentBonus,
            int maxTech)
        {
            int effectiveTech = Math.Max(1, Math.Min(maxTech, techLimit + equipmentBonus));
            string faction = factionId ?? string.Empty;
            string key = faction + "|" + effectiveTech.ToString(CultureInfo.InvariantCulture);
            if (!seen.Add(key)) return;
            result.Add(new EnemyLootContext(faction, Math.Max(1, techLimit), effectiveTech));
        }

        private static List<string> ResolveLootExternalItemIds(string externalId)
        {
            List<string> result = new List<string>();
            if (string.IsNullOrEmpty(externalId)) return result;

            Action<string> addKnown = delegate(string candidate)
            {
                if (string.IsNullOrEmpty(candidate) || !KnownItemIds.Contains(candidate)) return;
                for (int i = 0; i < result.Count; i++)
                    if (string.Equals(result[i], candidate, StringComparison.OrdinalIgnoreCase)) return;
                result.Add(candidate);
            };

            addKnown(externalId);
            if (externalId[0] == '*')
                addKnown(externalId.Substring(1));
            else
                addKnown("*" + externalId);

            List<LootItemMeta> linked;
            string alias = externalId[0] == '*' ? externalId.Substring(1) : externalId;
            if (LootAugmentationsByRecordId.TryGetValue(alias, out linked) && linked != null)
            {
                for (int i = 0; i < linked.Count; i++)
                {
                    LootItemMeta meta = linked[i];
                    if (meta != null) addKnown(meta.ItemId);
                }
            }
            return result;
        }

        private static void AddLootEnemySource(string itemId, LootEnemySource source)
        {
            if (string.IsNullOrEmpty(itemId) || source == null) return;
            List<LootEnemySource> list;
            if (!LootEnemySourcesByItem.TryGetValue(itemId, out list))
            {
                list = new List<LootEnemySource>();
                LootEnemySourcesByItem[itemId] = list;
            }

            for (int i = 0; i < list.Count; i++)
            {
                LootEnemySource existing = list[i];
                if (existing == null) continue;
                if (string.Equals(existing.MobClassId, source.MobClassId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(existing.Kind, source.Kind, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(existing.Detail, source.Detail, StringComparison.OrdinalIgnoreCase))
                {
                    existing.MinPercent = Math.Min(existing.MinPercent, source.MinPercent);
                    existing.MaxPercent = Math.Max(existing.MaxPercent, source.MaxPercent);
                    existing.MinCount = Math.Min(existing.MinCount, source.MinCount);
                    existing.MaxCount = Math.Max(existing.MaxCount, source.MaxCount);
                    if (source.MinTech > 0 && (existing.MinTech <= 0 || source.MinTech < existing.MinTech))
                        existing.MinTech = source.MinTech;
                    return;
                }
            }
            list.Add(source);
        }

        private static void ResetLootAmputationBuildState()
        {
            LootAmputationWarmupSlots.Clear();
            _lootAmputationWarmupIndex = 0;
            _lootAmputationWarmupStarted = false;
        }

        private static bool TickLootAmputationIndexSlice()
        {
            if (!_lootAmputationWarmupStarted)
            {
                LootAmputationSourcesByItem.Clear();
                LootAmputationWarmupSlots.Clear();
                LootAmputationWarmupSlots.AddRange(
                    EnumerateData(GetStaticMember(typeof(Data), "WoundSlots")));
                _lootAmputationWarmupIndex = 0;
                _lootAmputationWarmupStarted = true;
                if (LootAmputationWarmupSlots.Count == 0)
                {
                    ResetLootAmputationBuildState();
                    return true;
                }
            }

            if (_lootAmputationWarmupIndex < LootAmputationWarmupSlots.Count)
            {
                DataEntry entry = LootAmputationWarmupSlots[_lootAmputationWarmupIndex++];
                try { IndexLootAmputationSlot(entry); }
                catch (Exception ex)
                {
                    LogRuntimeBoundaryWarningOnce(
                        "loot.amputation.slot",
                        "One amputation loot slot could not be indexed and was skipped.",
                        ex);
                }
                return false;
            }

            ResetLootAmputationBuildState();
            return true;
        }

        private static void IndexLootAmputationSlot(DataEntry entry)
        {
            if (entry == null || entry.Value == null) return;
            string slotId = FirstNonEmpty(GetStringMember(entry.Value, "Id"), entry.Key);
            Dictionary<string, double> drops = ExtractWeightedStringMap(
                GetMember(entry.Value, "AmputatedDrop"));
            if (drops.Count == 0) return;
            double total = 0.0;
            foreach (KeyValuePair<string, double> pair in drops) total += pair.Value;
            if (total <= 0.0) return;
            foreach (KeyValuePair<string, double> pair in drops)
            {
                float conditional = (float)(pair.Value / total * 100.0);
                List<string> itemIds = ResolveLootExternalItemIds(pair.Key);
                for (int j = 0; j < itemIds.Count; j++)
                {
                    string itemId = itemIds[j];
                    List<LootAmputationSource> list;
                    if (!LootAmputationSourcesByItem.TryGetValue(itemId, out list))
                    {
                        list = new List<LootAmputationSource>();
                        LootAmputationSourcesByItem[itemId] = list;
                    }
                    list.Add(new LootAmputationSource(slotId, conditional));
                }
            }
        }

        private static void ResetLootMobWork()
        {
            _lootMobWorkEntry = null;
            _lootMobWorkRecord = null;
            _lootMobWorkId = string.Empty;
            _lootMobWorkWhitelist = null;
            _lootMobWorkContexts = null;
            _lootMobWorkAmmoMin = 0;
            _lootMobWorkAmmoMax = 0;
            _lootMobWorkStage = 0;
        }

        private static bool TickLootMobClassSlice(DataEntry entry)
        {
            if (entry == null || entry.Value == null)
            {
                ResetLootMobWork();
                return true;
            }

            if (!object.ReferenceEquals(_lootMobWorkEntry, entry))
            {
                ResetLootMobWork();
                _lootMobWorkEntry = entry;
                _lootMobWorkRecord = entry.Value;
                _lootMobWorkId = FirstNonEmpty(
                    GetStringMember(_lootMobWorkRecord, "Id"),
                    entry.Key);
                if (string.IsNullOrEmpty(_lootMobWorkId))
                {
                    ResetLootMobWork();
                    return true;
                }

                _lootMobWorkWhitelist = GetMember(_lootMobWorkRecord, "ItemCategoriesWhitelist");
                _lootMobWorkContexts = BuildEnemyLootContexts(_lootMobWorkId, _lootMobWorkRecord);

                List<string> granted = ExtractStringIds(GetMember(_lootMobWorkRecord, "GrantedItems"));
                Dictionary<string, int> grantedCounts =
                    new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < granted.Count; i++)
                {
                    string itemId = granted[i];
                    if (string.IsNullOrEmpty(itemId) || !KnownItemIds.Contains(itemId)) continue;
                    int count;
                    grantedCounts.TryGetValue(itemId, out count);
                    grantedCounts[itemId] = count + 1;
                }
                foreach (KeyValuePair<string, int> pair in grantedCounts)
                    AddLootEnemySource(pair.Key, new LootEnemySource(
                        _lootMobWorkId, 100f, 100f, "Granted", string.Empty,
                        pair.Value, pair.Value,
                        GetEarliestEnemyContextTech(_lootMobWorkContexts)));

                ReadIntRange(GetMember(_lootMobWorkRecord, "AdditAmmo"),
                    out _lootMobWorkAmmoMin, out _lootMobWorkAmmoMax);
                double grantedAmmoPositiveChance = 0.0;
                if (_lootMobWorkAmmoMax >= _lootMobWorkAmmoMin && _lootMobWorkAmmoMax >= 0)
                {
                    int totalAmmoValues = _lootMobWorkAmmoMax - _lootMobWorkAmmoMin + 1;
                    int positiveAmmoValues = _lootMobWorkAmmoMax - Math.Max(1, _lootMobWorkAmmoMin) + 1;
                    if (positiveAmmoValues < 0) positiveAmmoValues = 0;
                    if (totalAmmoValues > 0)
                        grantedAmmoPositiveChance = (double)positiveAmmoValues / totalAmmoValues;
                }
                if (grantedAmmoPositiveChance > 0.0)
                {
                    foreach (KeyValuePair<string, int> pair in grantedCounts)
                    {
                        LootItemMeta meta;
                        if (!LootItemMetaById.TryGetValue(pair.Key, out meta) || meta == null ||
                            string.IsNullOrEmpty(meta.DefaultAmmoId) || !KnownItemIds.Contains(meta.DefaultAmmoId))
                            continue;
                        AddLootEnemySource(
                            meta.DefaultAmmoId,
                            new LootEnemySource(
                                _lootMobWorkId, 0f, (float)(grantedAmmoPositiveChance * 100.0),
                                "GrantedWeaponAmmo", "slot-dependent",
                                Math.Max(0, _lootMobWorkAmmoMin), Math.Max(0, _lootMobWorkAmmoMax),
                                GetEarliestEnemyContextTech(_lootMobWorkContexts)));
                    }
                }

                _lootMobWorkStage = 1;
                return false;
            }

            if (_lootMobWorkStage == 1)
                IndexEnemyWeightedSlot(_lootMobWorkId, "Primary", "weapon",
                    GetMember(_lootMobWorkRecord, "PrimaryWeapon"), _lootMobWorkWhitelist,
                    _lootMobWorkContexts, _lootMobWorkAmmoMin, _lootMobWorkAmmoMax);
            else if (_lootMobWorkStage == 2)
                IndexEnemyWeightedSlot(_lootMobWorkId, "Secondary", "weapon",
                    GetMember(_lootMobWorkRecord, "SecondaryWeapon"), _lootMobWorkWhitelist,
                    _lootMobWorkContexts, _lootMobWorkAmmoMin, _lootMobWorkAmmoMax);
            else if (_lootMobWorkStage == 3)
                IndexEnemyWeightedSlot(_lootMobWorkId, "Head", "armor",
                    GetMember(_lootMobWorkRecord, "Head"), _lootMobWorkWhitelist,
                    _lootMobWorkContexts, 0, 0);
            else if (_lootMobWorkStage == 4)
                IndexEnemyWeightedSlot(_lootMobWorkId, "Armor", "armor",
                    GetMember(_lootMobWorkRecord, "Armor"), _lootMobWorkWhitelist,
                    _lootMobWorkContexts, 0, 0);
            else if (_lootMobWorkStage == 5)
                IndexEnemyWeightedSlot(_lootMobWorkId, "Leggings", "armor",
                    GetMember(_lootMobWorkRecord, "Leggings"), _lootMobWorkWhitelist,
                    _lootMobWorkContexts, 0, 0);
            else if (_lootMobWorkStage == 6)
                IndexEnemyWeightedSlot(_lootMobWorkId, "Boots", "armor",
                    GetMember(_lootMobWorkRecord, "Boots"), _lootMobWorkWhitelist,
                    _lootMobWorkContexts, 0, 0);
            else if (_lootMobWorkStage == 7)
                IndexEnemyAdditionalItems(_lootMobWorkId, _lootMobWorkRecord,
                    _lootMobWorkWhitelist, _lootMobWorkContexts,
                    _lootMobWorkAmmoMin, _lootMobWorkAmmoMax);
            else if (_lootMobWorkStage == 8)
                IndexEnemyAugmentationAttempts(_lootMobWorkId, _lootMobWorkRecord,
                    _lootMobWorkWhitelist, _lootMobWorkContexts);
            else if (_lootMobWorkStage == 9)
                IndexEnemyImplantAttempts(_lootMobWorkId, _lootMobWorkRecord,
                    _lootMobWorkWhitelist, _lootMobWorkContexts);

            _lootMobWorkStage++;
            if (_lootMobWorkStage > 9)
            {
                ResetLootMobWork();
                return true;
            }
            return false;
        }


        private static void IndexLootItemMeta(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) ||
                LootItemMetaById.ContainsKey(itemId))
                return;

            object itemRecord = FindLootItemRecord(itemId);
            if (itemRecord == null) return;

            HashSet<string> categories =
                ExtractStableStringSet(
                    GetMember(itemRecord, "Categories"));

            string itemClass = ConvertToStableString(
                GetMember(itemRecord, "ItemClass"));

            int tech = 0;
            TryGetExactItemTechLevel(itemId, out tech);

            string weaponClass = string.Empty;
            string armorClass = string.Empty;
            string augmentationClass = string.Empty;
            string defaultAmmoId = string.Empty;
            string equipmentSlotKind = string.Empty;
            bool isImplant = false;

            HashSet<string> augmentationRecordAliases =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            object root;
            if (ItemRecordsById.TryGetValue(itemId, out root) && root != null)
            {
                List<object> graph = BuildRelevantItemGraph(root, 3, 48);
                augmentationRecordAliases = ExtractAugmentationRecordAliases(itemId, graph);
                for (int i = 0; i < graph.Count; i++)
                {
                    object node = graph[i];
                    if (node == null) continue;
                    string typeName = node.GetType().Name ?? string.Empty;

                    if (string.IsNullOrEmpty(weaponClass))
                        weaponClass = ConvertToStableString(GetMember(node, "WeaponClass"));
                    if (string.IsNullOrEmpty(armorClass))
                        armorClass = ConvertToStableString(GetMember(node, "ArmorClass"));
                    if (string.IsNullOrEmpty(augmentationClass))
                        augmentationClass = ConvertToStableString(GetMember(node, "AugmentationClass"));
                    if (string.IsNullOrEmpty(defaultAmmoId))
                        defaultAmmoId = GetStringMember(node, "DefaultAmmoId");
                    if (string.IsNullOrEmpty(equipmentSlotKind))
                        equipmentSlotKind = ResolveEquipmentSlotKindFromNode(node, typeName, itemId, categories, itemClass);

                    if (typeName.IndexOf("ImplantRecord", StringComparison.OrdinalIgnoreCase) >= 0)
                        isImplant = true;
                }
            }

            LootItemMeta meta = new LootItemMeta(
                itemId,
                itemClass,
                Math.Max(0, tech),
                categories,
                weaponClass,
                armorClass,
                augmentationClass,
                defaultAmmoId,
                equipmentSlotKind,
                isImplant);
            LootItemMetaById[itemId] = meta;

            // Quasimorph uses a leading '*' on several hidden/recoverable inventory
            // item IDs (for example *cyborg_battle_hand), while MobClass
            // GrantedAugmentations refers to the same augmentation without '*'.
            // Keep that parser-level alias explicitly; reflection-only graph walking
            // cannot reliably recover this relationship.
            if (itemId.Length > 1 && itemId[0] == '*')
                AddLootMetaClassIndex(LootAugmentationsByRecordId, itemId.Substring(1), meta);

            foreach (string category in categories)
            {
                if (string.IsNullOrEmpty(category)) continue;

                List<LootItemMeta> list;
                if (!LootItemsByCategory.TryGetValue(category, out list))
                {
                    list = new List<LootItemMeta>();
                    LootItemsByCategory[category] = list;
                }

                list.Add(meta);
            }

            AddLootMetaClassIndex(LootItemsByItemClass, itemClass, meta);
            AddLootMetaClassIndex(LootItemsByWeaponClass, weaponClass, meta);
            AddLootMetaClassIndex(LootItemsByArmorClass, armorClass, meta);
            if (isImplant)
                AddLootMetaClassIndex(LootImplantsByAugmentationClass, augmentationClass, meta);
            else if (!string.IsNullOrEmpty(augmentationClass))
            {
                AddLootMetaClassIndex(LootAugmentationsByAugmentationClass, augmentationClass, meta);
                AddLootMetaClassIndexSet(LootAugmentationsByRecordId, augmentationRecordAliases, meta);
            }
        }

        private static void AddLootMetaClassIndexSet(
            Dictionary<string, List<LootItemMeta>> index,
            IEnumerable<string> classIds,
            LootItemMeta meta)
        {
            if (index == null || classIds == null || meta == null) return;
            foreach (string classId in classIds)
                AddLootMetaClassIndex(index, classId, meta);
        }

        private static void AddLootMetaClassIndex(
            Dictionary<string, List<LootItemMeta>> index,
            string classId,
            LootItemMeta meta)
        {
            if (index == null || meta == null || string.IsNullOrEmpty(classId)) return;
            List<LootItemMeta> list;
            if (!index.TryGetValue(classId, out list))
            {
                list = new List<LootItemMeta>();
                index[classId] = list;
            }
            list.Add(meta);
        }

        private static void IndexLootMissionCategories(
            DataEntry entry,
            string sourceKind)
        {
            if (entry == null || entry.Value == null) return;

            object sourceRecord = entry.Value;
            string sourceId = FirstNonEmpty(
                GetStringMember(sourceRecord, "Id"),
                entry.Key);
            if (string.IsNullOrEmpty(sourceId)) return;

            HashSet<string> sourceCategories =
                ExtractStableStringSet(
                    GetMember(sourceRecord, "ItemDropCategories"));
            if (sourceCategories.Count == 0) return;

            HashSet<string> forbiddenClasses =
                ExtractStableStringSet(
                    GetMember(sourceRecord, "ForbiddenItemClasses"));

            Dictionary<string, List<LootMissionSource>> target;
            if (string.Equals(sourceKind, "Bramfatura", StringComparison.Ordinal))
                target = LootBramfaturaSourcesByItem;
            else if (string.Equals(sourceKind, "StationType", StringComparison.Ordinal))
                target = LootStationTypeSourcesByItem;
            else
                target = LootFactionSourcesByItem;

            // Phase 0 already resolved each item graph once. Source processing now uses
            // the category reverse index and never scans all KnownItemIds.
            HashSet<string> seenItems =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string category in sourceCategories)
            {
                List<LootItemMeta> candidates;
                if (string.IsNullOrEmpty(category) ||
                    !LootItemsByCategory.TryGetValue(category, out candidates) ||
                    candidates == null)
                    continue;

                for (int i = 0; i < candidates.Count; i++)
                {
                    LootItemMeta meta = candidates[i];
                    if (meta == null ||
                        string.IsNullOrEmpty(meta.ItemId) ||
                        !seenItems.Add(meta.ItemId))
                        continue;

                    if (!string.IsNullOrEmpty(meta.ItemClass) &&
                        forbiddenClasses.Contains(meta.ItemClass))
                        continue;

                    AddLootMissionSource(
                        target,
                        meta.ItemId,
                        new LootMissionSource(
                            sourceId,
                            sourceKind,
                            meta.TechLevel));
                }
            }
        }

        private static void AddLootMissionSource(
            Dictionary<string, List<LootMissionSource>> map,
            string itemId,
            LootMissionSource source)
        {
            if (map == null || string.IsNullOrEmpty(itemId) || source == null)
                return;

            List<LootMissionSource> list;
            if (!map.TryGetValue(itemId, out list))
            {
                list = new List<LootMissionSource>();
                map[itemId] = list;
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null &&
                    string.Equals(list[i].SourceId, source.SourceId, StringComparison.OrdinalIgnoreCase))
                    return;
            }

            list.Add(source);
        }

        private static object FindLootItemRecord(string itemId)
        {
            return ResolveCanonicalItemMetadataRecord(itemId);
        }

    }
}
