using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;
using MGSC;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // Secret Data is a Factions feature even though BrowserUI renders it. Keep its
        // action contract and session selection state with the owning feature.
        private const string SecretDataFactionActionPrefix = "QII_SECRET_DATA_FACTION:";
        private const string SecretDataBackAction = "QII_SECRET_DATA_BACK";
        private static string _secretDataSelectedFactionId = string.Empty;
        private static bool _secretDataContractLogged;


        // v1.7.36-test2: feature-owned state moved out of Runtime.cs.
        // Declaration ownership only; lifecycle and behavior are unchanged.

        // v1.7.36-test8: save-owned faction/difficulty service references live
        // with the feature that consumes them.
        private static object _factionsState;
        private static object _difficultyState;

        private static readonly Dictionary<string, object> RuntimeFactionsById =
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Sprite> FactionSmallIcons =
            new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> FactionIconMisses =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static Sprite _quasimorphFallbackFactionIcon;
        private static bool _factionTradeSchemaLogged;

        // v1.6.1 faction technology index. One faction is processed per frame.
        private static readonly Dictionary<string, List<FactionTechUnlock>> FactionTechUnlocksByItem =
            new Dictionary<string, List<FactionTechUnlock>>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<DataEntry> FactionTechWarmupFactions = new List<DataEntry>();
        private static int _factionTechWarmupIndex;
        private static bool _factionTechWarmupActive;
        private static bool _factionTechWarmupComplete;
        private static bool _factionTechSchemaLogged;
        private static bool _factionTechApiLogged;
        private static bool _factionTechApiWarningLogged;
        private static MethodInfo _factionGetTradeItemsMethod;
        private static MethodInfo _factionGetTechLevelLimitMethod;
        private static Type _factionTradeCategoryType;
        private static object _factionDropCollection;
        private static bool _factionEnabledContractResolved;
        private static bool _factionEnabledContractLogged;
        private static MethodInfo _factionsIsEnabledFactionMethod;
        private static readonly Dictionary<string, FactionRewardPoolSnapshot> FactionRewardPoolCache =
            new Dictionary<string, FactionRewardPoolSnapshot>(StringComparer.OrdinalIgnoreCase);
        private static void InitializeFactionSpaceSessionState()
        {
            _factionsState = null;
            _difficultyState = null;
        }

        private static void ResetFactionMenuSessionState()
        {
            _factionsState = null;
            _difficultyState = null;
            RuntimeFactionsById.Clear();
            FactionRewardPoolCache.Clear();
            _quasimorphFallbackFactionIcon = null;
        }

        // v1.7.36-test5: factions own their runtime visual/reward/technology
        // index reset. Behavior and reset order are unchanged.
        private static void ResetFactionIndexState()
        {
            RuntimeFactionsById.Clear();
            FactionSmallIcons.Clear();
            FactionIconMisses.Clear();
            _factionTradeSchemaLogged = false;

            FactionTechUnlocksByItem.Clear();
            FactionTechWarmupFactions.Clear();
            _factionTechWarmupIndex = 0;
            _factionTechWarmupActive = false;
            _factionTechWarmupComplete = false;
            _factionTechSchemaLogged = false;
            _factionTechApiLogged = false;
            _factionTechApiWarningLogged = false;
            _factionGetTradeItemsMethod = null;
            _factionGetTechLevelLimitMethod = null;
            _factionTradeCategoryType = null;
            _factionDropCollection = null;
            _factionEnabledContractResolved = false;
            _factionEnabledContractLogged = false;
            _factionsIsEnabledFactionMethod = null;
            FactionRewardPoolCache.Clear();
        }

        private static void StartFactionFeatureWarmup()
        {
            if (!_compatFactions) return;
            try { StartFactionTechWarmup(); }
            catch (Exception ex)
            {
                StopFactionFeatureFrameWork();
                TripCompatibilityFeatureRuntime("Factions", ex);
            }
        }

        private static void TickFactionFeatureFrameWork()
        {
            if (!_compatFactions) return;
            try { TickFactionTechWarmup(); }
            catch (Exception ex)
            {
                StopFactionFeatureFrameWork();
                TripCompatibilityFeatureRuntime("Factions", ex);
            }
        }

        private static void StopFactionFeatureFrameWork()
        {
            _factionTechWarmupActive = false;
            FactionTechWarmupFactions.Clear();
            _factionTechWarmupIndex = 0;
        }

        private static string GetFactionWarmupStatus()
        {
            return !_compatFactions
                ? "disabled"
                : (_factionTechWarmupActive ? "pending" : "complete");
        }

        private static void StartFactionTechWarmup()
        {
            FactionTechUnlocksByItem.Clear();
            FactionTechWarmupFactions.Clear();
            _factionTechWarmupIndex = 0;
            _factionTechWarmupActive = false;
            _factionTechWarmupComplete = false;
            _factionTechSchemaLogged = false;

            object factions = GetStaticMember(typeof(Data), "Factions");
            if (factions == null)
            {
                _factionTechWarmupComplete = true;
                return;
            }

            List<DataEntry> entries = EnumerateData(factions);
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].Value != null)
                    FactionTechWarmupFactions.Add(entries[i]);
            }

            _factionTechWarmupActive = FactionTechWarmupFactions.Count > 0;
            _factionTechWarmupComplete = !_factionTechWarmupActive;

            Debug.Log("[ItemIntelligence] Faction technology warmup queued: " +
                FactionTechWarmupFactions.Count.ToString(CultureInfo.InvariantCulture) + " factions.");
        }

        private static void TickFactionTechWarmup()
        {
            if (!_factionTechWarmupActive) return;

            // Faction count is small, but keep the work incremental anyway.
            int processed = 0;
            while (_factionTechWarmupIndex < FactionTechWarmupFactions.Count && processed < 1)
            {
                DataEntry entry = FactionTechWarmupFactions[_factionTechWarmupIndex++];
                try { IndexFactionTechnology(entry); }
                catch (Exception ex)
                {
                    if (!_factionTechSchemaLogged)
                    {
                        _factionTechSchemaLogged = true;
                        Debug.LogWarning("[ItemIntelligence] Faction technology record skipped: " + ex.Message);
                    }
                }
                processed++;
            }

            if (_factionTechWarmupIndex < FactionTechWarmupFactions.Count) return;

            _factionTechWarmupActive = false;
            _factionTechWarmupComplete = true;
            int links = 0;
            foreach (KeyValuePair<string, List<FactionTechUnlock>> pair in FactionTechUnlocksByItem)
                if (pair.Value != null) links += pair.Value.Count;

            Debug.Log("[ItemIntelligence] Faction technology warmup complete: items=" +
                FactionTechUnlocksByItem.Count.ToString(CultureInfo.InvariantCulture) +
                ", links=" + links.ToString(CultureInfo.InvariantCulture) + ".");

            FactionTechWarmupFactions.Clear();
            _factionTechWarmupIndex = 0;

            if (_inspectorOpen && _browserTab == (int)BrowserTabId.Factions)
                RenderBrowser(_inspectorItemId);
        }

        private static void IndexFactionTechnology(DataEntry entry)
        {
            if (entry == null || entry.Value == null) return;

            object factionRecord = entry.Value;
            string factionId = FirstNonEmpty(
                GetStringMember(factionRecord, "Id"),
                GetStringMember(factionRecord, "FactionId"),
                entry.Key);
            if (string.IsNullOrEmpty(factionId)) return;

            object runtimeFaction = ResolveFactionById(factionId);
            if (runtimeFaction == null || !EnsureFactionRewardApi(runtimeFaction)) return;

            int maxTech = 10;
            try
            {
                if (Data.Global != null) maxTech = Math.Max(0, Data.Global.MaxTechLevel);
            }
            catch { }

            string[] categoryNames = new string[] { "Equipment", "Chips", "Consumables" };
            HashSet<object> seenRecords = new HashSet<object>(ReferenceComparer.Instance);

            for (int c = 0; c < categoryNames.Length; c++)
            {
                object categoryValue = ParseFactionTradeCategory(categoryNames[c]);
                if (categoryValue == null) continue;

                IEnumerable records = InvokeFactionTradeItems(runtimeFaction, maxTech, categoryValue);
                if (records == null) continue;

                int scannedRecords = 0;
                foreach (object record in records)
                {
                    if (++scannedRecords > 4096) break;
                    if (record == null || seenRecords.Contains(record)) continue;
                    seenRecords.Add(record);

                    int techLevel = 0;
                    int parsedTechLevel;
                    if (TryToInt(GetMember(record, "TechLevel"), out parsedTechLevel))
                        techLevel = Math.Max(0, parsedTechLevel);

                    // Mirror TechLevelRewardsPanel: the vanilla faction-tech window
                    // displays ContentIds[0] for a ContentDropRecord. Using the same
                    // canonical reward ID keeps our unlock/chance rows identical to it.
                    string itemId = GetFactionRewardRecordItemId(record);
                    if (string.IsNullOrEmpty(itemId) || !KnownItemIds.Contains(itemId))
                        continue;

                    AddFactionTechUnlock(itemId, factionId, techLevel);
                }
            }
        }

        private static bool EnsureFactionRewardApi(object runtimeFaction)
        {
            if (runtimeFaction == null) return false;
            if (_factionGetTradeItemsMethod != null && _factionTradeCategoryType != null &&
                _factionGetTechLevelLimitMethod != null && _factionDropCollection != null)
                return true;

            object dropCollection = null;
            try { dropCollection = Data.FactionDrop; } catch { dropCollection = null; }
            if (dropCollection == null) return false;

            MethodInfo getTradeItems = null;
            MethodInfo getTechLevelLimit = null;
            Type categoryType = null;

            try
            {
                MethodInfo[] methods = dropCollection.GetType().GetMethods(InstanceFlags);
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo candidate = methods[i];
                    if (candidate == null) continue;
                    ParameterInfo[] parameters = candidate.GetParameters();

                    if (string.Equals(candidate.Name, "GetTradeItems", StringComparison.Ordinal) &&
                        parameters != null && parameters.Length == 4 &&
                        parameters[1].ParameterType == typeof(int) &&
                        parameters[3].ParameterType == typeof(bool) &&
                        parameters[0].ParameterType.IsInstanceOfType(runtimeFaction) &&
                        parameters[2].ParameterType != null && parameters[2].ParameterType.IsEnum)
                    {
                        getTradeItems = candidate;
                        categoryType = parameters[2].ParameterType;
                    }
                    else if (string.Equals(candidate.Name, "GetTechLevelLimit", StringComparison.Ordinal) &&
                        parameters != null && parameters.Length == 1 &&
                        parameters[0].ParameterType.IsInstanceOfType(runtimeFaction))
                    {
                        getTechLevelLimit = candidate;
                    }
                }
            }
            catch
            {
                getTradeItems = null;
                getTechLevelLimit = null;
                categoryType = null;
            }

            if (getTradeItems == null || categoryType == null || getTechLevelLimit == null)
            {
                if (!_factionTechApiWarningLogged)
                {
                    _factionTechApiWarningLogged = true;
                    Debug.LogWarning("[ItemIntelligence] Faction reward API unresolved: GetTradeItems/GetTechLevelLimit not found.");
                }
                return false;
            }

            _factionDropCollection = dropCollection;
            _factionGetTradeItemsMethod = getTradeItems;
            _factionGetTechLevelLimitMethod = getTechLevelLimit;
            _factionTradeCategoryType = categoryType;

            if (!_factionTechApiLogged)
            {
                _factionTechApiLogged = true;
                Debug.Log("[ItemIntelligence] Faction reward API resolved: " +
                    dropCollection.GetType().FullName + ".GetTradeItems, category=" +
                    categoryType.FullName + ".");
            }
            return true;
        }

        private static int GetFactionTechLevelLimit(object runtimeFaction)
        {
            if (runtimeFaction == null || !EnsureFactionRewardApi(runtimeFaction)) return -1;
            try
            {
                object raw = _factionGetTechLevelLimitMethod.Invoke(
                    _factionDropCollection, new object[] { runtimeFaction });
                int value;
                if (TryToInt(raw, out value)) return value;
            }
            catch { }
            return -1;
        }

        private static string GetFactionRewardRecordItemId(object record)
        {
            if (record == null) return string.Empty;
            object rawContentIds = GetMember(record, "ContentIds");
            IList list = rawContentIds as IList;
            if (list != null && list.Count > 0)
            {
                object raw = list[0];
                return raw as string ?? Convert.ToString(raw, CultureInfo.InvariantCulture);
            }

            IEnumerable enumerable = rawContentIds as IEnumerable;
            if (enumerable != null && !(rawContentIds is string))
            {
                foreach (object raw in enumerable)
                    return raw as string ?? Convert.ToString(raw, CultureInfo.InvariantCulture);
            }
            return string.Empty;
        }

        private static float GetFactionRewardRecordWeight(object record)
        {
            double value;
            if (record != null && TryToDoubleSafe(GetMember(record, "Weight"), out value) && value > 0.0)
                return (float)value;
            return 0f;
        }

        private static FactionRewardPoolSnapshot GetFactionRewardPoolSnapshot(string factionId)
        {
            if (string.IsNullOrEmpty(factionId)) return null;
            object faction = ResolveFactionById(factionId);
            if (faction == null || !EnsureFactionRewardApi(faction)) return null;

            int currentTech = GetCurrentFactionTechLevel(factionId);
            int techLimit = GetFactionTechLevelLimit(faction);
            if (currentTech < 0 || techLimit < 0) return null;
            int effectiveTech = Math.Max(0, Math.Min(currentTech, techLimit));

            FactionRewardPoolSnapshot cached;
            if (FactionRewardPoolCache.TryGetValue(factionId, out cached) && cached != null &&
                cached.CurrentTech == currentTech && cached.TechLimit == techLimit &&
                cached.EffectiveTech == effectiveTech)
                return cached;

            FactionRewardPoolSnapshot snapshot =
                new FactionRewardPoolSnapshot(factionId, currentTech, techLimit, effectiveTech);

            string[] categoryNames = new string[] { "Equipment", "Chips", "Consumables" };

            for (int c = 0; c < categoryNames.Length; c++)
            {
                object categoryValue = ParseFactionTradeCategory(categoryNames[c]);
                if (categoryValue == null) continue;
                IEnumerable records = InvokeFactionTradeItems(faction, effectiveTech, categoryValue);
                if (records == null) continue;

                int scanned = 0;
                foreach (object record in records)
                {
                    if (++scanned > 4096) break;
                    if (record == null) continue;

                    // Match FactionTechnologyWindow.GetTotalWeight exactly: vanilla
                    // sums every ContentDropRecord returned by all three AddRange calls.
                    float weight = GetFactionRewardRecordWeight(record);
                    if (weight <= 0f) continue;
                    snapshot.TotalWeight += weight;

                    string rewardItemId = GetFactionRewardRecordItemId(record);
                    if (string.IsNullOrEmpty(rewardItemId)) continue;

                    float existing;
                    snapshot.ItemWeights.TryGetValue(rewardItemId, out existing);
                    snapshot.ItemWeights[rewardItemId] = existing + weight;
                }
            }

            FactionRewardPoolCache[factionId] = snapshot;
            return snapshot;
        }

        private static int ResolveFactionAvailabilityForCurrentSave(string factionId)
        {
            object rawFaction = ResolveFactionById(factionId);
            Faction faction = rawFaction as Faction;
            Factions factions = _factionsState as Factions;
            if (faction == null || factions == null) return -1;

            if (!_factionEnabledContractResolved)
            {
                _factionEnabledContractResolved = true;
                _factionsIsEnabledFactionMethod = AccessTools.Method(
                    typeof(Factions), "IsEnabledFaction", new Type[] { typeof(Faction) });
                if (!_factionEnabledContractLogged)
                {
                    _factionEnabledContractLogged = true;
                    if (_factionsIsEnabledFactionMethod != null)
                        Debug.Log("[ItemIntelligence][FactionAvailability] exactGate=Factions.IsEnabledFaction(Faction).");
                    else
                        Debug.LogWarning("[ItemIntelligence][FactionAvailability] exact current-save gate unavailable; faction reward rows are not hidden by guesswork.");
                }
            }

            if (_factionsIsEnabledFactionMethod == null) return -1;
            try
            {
                object raw = _factionsIsEnabledFactionMethod.Invoke(factions, new object[] { faction });
                if (raw is bool) return (bool)raw ? 1 : 0;
            }
            catch { }
            return -1;
        }

        private static FactionRewardView BuildFactionRewardView(FactionTechUnlock unlock, string itemId)
        {
            if (unlock == null) return null;
            FactionRewardPoolSnapshot snapshot = GetFactionRewardPoolSnapshot(unlock.FactionId);
            int currentTech = snapshot == null ? GetCurrentFactionTechLevel(unlock.FactionId) : snapshot.CurrentTech;
            int techLimit = snapshot == null ? -1 : snapshot.TechLimit;

            int state = 3; // unknown
            float percent = 0f;

            if (currentTech >= 0 && currentTech < unlock.TechLevel)
            {
                state = 1; // locked by current tech
            }
            else if (currentTech >= unlock.TechLevel && techLimit >= 0 && techLimit < unlock.TechLevel)
            {
                state = 2; // vanilla TechLevelRewardsPanel: unavailable, low reputation
            }
            else if (snapshot != null)
            {
                state = 0;
                float itemWeight;
                if (snapshot.TotalWeight > 0f && snapshot.ItemWeights.TryGetValue(itemId, out itemWeight))
                    percent = Mathf.Clamp(itemWeight / snapshot.TotalWeight * 100f, 0f, 100f);
            }

            return new FactionRewardView(
                unlock.FactionId, unlock.TechLevel, currentTech, techLimit, percent, state);
        }

        private static void AddFactionTechUnlock(string itemId, string factionId, int level)
        {
            if (string.IsNullOrEmpty(itemId) || string.IsNullOrEmpty(factionId)) return;
            List<FactionTechUnlock> list;
            if (!FactionTechUnlocksByItem.TryGetValue(itemId, out list))
            {
                list = new List<FactionTechUnlock>();
                FactionTechUnlocksByItem[itemId] = list;
            }

            for (int i = 0; i < list.Count; i++)
            {
                FactionTechUnlock existing = list[i];
                if (existing != null && string.Equals(existing.FactionId, factionId, StringComparison.OrdinalIgnoreCase))
                {
                    if (level < existing.TechLevel) existing.TechLevel = level;
                    return;
                }
            }

            list.Add(new FactionTechUnlock(factionId, level));
        }

        private static List<string> GetRelevantMobFactionIds(string mobClassId, object mobRecord)
        {
            List<string> result = new List<string>();
            Dictionary<string, int> byFaction;
            if (!string.IsNullOrEmpty(mobClassId) &&
                LootEnemyMinSpawnTechByFaction.TryGetValue(mobClassId, out byFaction) &&
                byFaction != null)
            {
                foreach (KeyValuePair<string, int> pair in byFaction)
                    AddUniqueTextValue(result, pair.Key);
            }

            if (mobRecord != null)
            {
                AddUniqueTextValue(result, GetStringMember(mobRecord, "DefaultItemFactionTag"));
            }
            return result;
        }

        private static string GetRepresentativeMobFactionId(string mobClassId)
        {
            if (string.IsNullOrEmpty(mobClassId)) return string.Empty;
            object mobRecord = FindLootDataRecord("MobClasses", mobClassId);
            string preferred = GetStringMember(mobRecord, "DefaultItemFactionTag");
            if (!string.IsNullOrEmpty(preferred)) return preferred;
            List<string> ids = GetRelevantMobFactionIds(mobClassId, mobRecord);
            for (int i = 0; i < ids.Count; i++)
                if (!string.IsNullOrEmpty(ids[i])) return ids[i];

            // Some Quasimorph-only units have no normal corporation/faction emblem.
            // Return a synthetic visual ID so the Loot table can still show a stable,
            // recognizable Quasimorph marker without inventing gameplay affiliation.
            string nature = ConvertToStableString(GetMember(mobRecord, "NatureType"));
            string creatureClass = ConvertToStableString(GetMember(mobRecord, "CreatureClass"));
            string probe = (mobClassId + "|" + nature + "|" + creatureClass).ToLowerInvariant();
            if (probe.IndexOf("quasi", StringComparison.Ordinal) >= 0)
                return "QII_QUASIMORPH";
            return string.Empty;
        }

        private static bool IsSecretDataItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;
            try
            {
                StoryVars storyVars = Data.StoryVars;
                if (storyVars != null && !string.IsNullOrEmpty(storyVars.TutorialAncomQuestItemId))
                    return string.Equals(itemId, storyVars.TutorialAncomQuestItemId, StringComparison.OrdinalIgnoreCase);
            }
            catch { }

            // Current vanilla ID fallback. This is only used before StoryVars is available;
            // reward contents themselves are never hard-coded.
            return string.Equals(itemId, "quest_ancom_data", StringComparison.OrdinalIgnoreCase);
        }

        private static int GetSecretDataReputationReward()
        {
            try
            {
                StoryVars storyVars = Data.StoryVars;
                if (storyVars != null) return storyVars.TutorialAncomDataReputationReward;
            }
            catch { }
            return int.MinValue;
        }

        private static List<AnComDataRewardRecord> GetSecretDataRewardRecords()
        {
            try { return Data.AnComDataRewards; }
            catch { return null; }
        }

        private static AnComDataRewardRecord FindSecretDataRewardRecord(
            List<AnComDataRewardRecord> rewards, string factionId)
        {
            if (rewards == null || string.IsNullOrEmpty(factionId)) return null;
            for (int i = 0; i < rewards.Count; i++)
            {
                AnComDataRewardRecord record = rewards[i];
                if (record != null && string.Equals(record.Id, factionId, StringComparison.OrdinalIgnoreCase))
                    return record;
            }
            return null;
        }

        private static bool HasSecretDataStoryHandler(string factionId)
        {
            if (string.IsNullOrEmpty(factionId)) return false;
            try
            {
                return AccessTools.TypeByName("MGSC.AnComData_space_" + factionId) != null;
            }
            catch { return false; }
        }

        private static string FormatSecretDataReputation(int reputation)
        {
            return reputation == int.MinValue
                ? "?"
                : (reputation >= 0 ? "+" : string.Empty) + reputation.ToString(CultureInfo.InvariantCulture);
        }

        private static void BuildBrowserSecretDataRewards()
        {
            BrowserLines.Add(BrowserLine.FullSection(Ui("ui.secret_data_rewards")));

            List<AnComDataRewardRecord> rewards = GetSecretDataRewardRecords();
            if (rewards == null || rewards.Count == 0)
            {
                AddWrappedBrowserNote("ui.secret_data_no_rewards", 72, 86);
                return;
            }

            int reputation = GetSecretDataReputationReward();
            string reputationText = FormatSecretDataReputation(reputation);
            if (!_secretDataContractLogged)
            {
                _secretDataContractLogged = true;
                Debug.Log("[ItemIntelligence] Secret Data rewards resolved: records=" +
                    rewards.Count.ToString(CultureInfo.InvariantCulture) +
                    ", reputation=" + reputationText +
                    ", source=Data.AnComDataRewards.");
            }

            if (!string.IsNullOrEmpty(_secretDataSelectedFactionId))
            {
                AnComDataRewardRecord selected =
                    FindSecretDataRewardRecord(rewards, _secretDataSelectedFactionId);
                if (selected == null)
                {
                    _secretDataSelectedFactionId = string.Empty;
                    _browserPage = 0;
                    BuildBrowserSecretDataRewards();
                    return;
                }

                BrowserLines.Add(BrowserLine.Station(
                    Ui("ui.secret_data_back"),
                    string.Empty,
                    SecretDataBackAction,
                    string.Empty,
                    0));

                BrowserLines.Add(BrowserLine.FullSection(ResolveFactionDisplayName(selected.Id)));
                BrowserLines.Add(BrowserLine.Normal(
                    Ui("ui.secret_data_reputation"),
                    reputationText));

                if (HasSecretDataStoryHandler(selected.Id))
                    AddWrappedBrowserNote("ui.secret_data_story_effect", 72, 86);

                BrowserLines.Add(BrowserLine.FullSection(Ui("ui.secret_data_package")));

                List<string> items = selected.Items;
                if (items == null || items.Count == 0)
                {
                    BrowserLines.Add(BrowserLine.Note(Ui("ui.none")));
                    return;
                }

                Dictionary<string, int> counts =
                    new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                List<string> orderedIds = new List<string>();
                for (int i = 0; i < items.Count; i++)
                {
                    string rewardItemId = items[i];
                    if (string.IsNullOrEmpty(rewardItemId)) continue;
                    int count;
                    if (counts.TryGetValue(rewardItemId, out count))
                        counts[rewardItemId] = count + 1;
                    else
                    {
                        counts[rewardItemId] = 1;
                        orderedIds.Add(rewardItemId);
                    }
                }

                for (int i = 0; i < orderedIds.Count; i++)
                {
                    string rewardItemId = orderedIds[i];
                    int count = counts[rewardItemId];
                    BrowserLines.Add(BrowserLine.Item(
                        rewardItemId,
                        count > 1 ? "x" + count.ToString(CultureInfo.InvariantCulture) : string.Empty));
                }
                return;
            }

            AddWrappedBrowserNote("ui.secret_data_rewards_note", 72, 86);

            List<AnComDataRewardRecord> ordered = new List<AnComDataRewardRecord>();
            for (int i = 0; i < rewards.Count; i++)
            {
                AnComDataRewardRecord record = rewards[i];
                if (record == null || string.IsNullOrEmpty(record.Id)) continue;
                ordered.Add(record);
            }
            ordered.Sort(delegate(AnComDataRewardRecord a, AnComDataRewardRecord b)
            {
                return string.Compare(
                    ResolveFactionDisplayName(a.Id),
                    ResolveFactionDisplayName(b.Id),
                    StringComparison.CurrentCultureIgnoreCase);
            });

            for (int i = 0; i < ordered.Count; i++)
            {
                AnComDataRewardRecord record = ordered[i];
                int totalItems = record.Items == null ? 0 : record.Items.Count;
                string itemCount = totalItems.ToString(CultureInfo.InvariantCulture) + " " + Ui("ui.secret_data_items");
                string right = itemCount + "  •  " + reputationText + " " + Ui("ui.secret_data_rep") +
                    "  •  " + Ui("ui.secret_data_view");

                BrowserLines.Add(BrowserLine.Station(
                    ResolveFactionDisplayName(record.Id),
                    right,
                    SecretDataFactionActionPrefix + record.Id,
                    record.Id,
                    0));
            }
        }

        private static void BuildBrowserFactionTechnology(string itemId)
        {
            bool ru = IsRussian();

            if (IsSecretDataItem(itemId))
            {
                BuildBrowserSecretDataRewards();
                return;
            }

            _secretDataSelectedFactionId = string.Empty;

            if (!_compatFactions)
            {
                AddCompatibilityUnavailableLine("Factions");
                return;
            }
            EnsureTradeStateDependencies();

            BrowserLines.Add(BrowserLine.Section(Ui("ui.faction_rewards")));

            if (_factionTechWarmupActive)
            {
                int total = Math.Max(1, FactionTechWarmupFactions.Count);
                int percent = Math.Min(100, (_factionTechWarmupIndex * 100) / total);
                BrowserLines.Add(BrowserLine.Note((Ui("ui.faction_index")) +
                    percent.ToString(CultureInfo.InvariantCulture) + "%"));
            }

            List<FactionTechUnlock> unlocks;
            if (!FactionTechUnlocksByItem.TryGetValue(itemId, out unlocks) || unlocks == null || unlocks.Count == 0)
            {
                BrowserLines.Add(BrowserLine.Note(Ui("ui.this_item_was_not_found_in_faction_reward_pools")));
                return;
            }

            List<FactionRewardView> views = new List<FactionRewardView>();
            int unavailableForSave = 0;
            for (int i = 0; i < unlocks.Count; i++)
            {
                FactionTechUnlock unlock = unlocks[i];
                if (unlock == null) continue;
                int availability = ResolveFactionAvailabilityForCurrentSave(unlock.FactionId);
                if (availability == 0)
                {
                    unavailableForSave++;
                    continue;
                }

                FactionRewardView view = BuildFactionRewardView(unlock, itemId);
                if (view != null) views.Add(view);
            }

            if (views.Count == 0 && unavailableForSave > 0)
            {
                BrowserLines.Add(BrowserLine.Note(Ui("ui.no_active_faction_reward_in_current_save")));
                return;
            }

            views.Sort(delegate(FactionRewardView a, FactionRewardView b)
            {
                bool aa = a.State == 0;
                bool ba = b.State == 0;
                if (aa != ba) return aa ? -1 : 1;
                int chance = b.RewardPercent.CompareTo(a.RewardPercent);
                if (chance != 0) return chance;
                int state = a.State.CompareTo(b.State);
                if (state != 0) return state;
                int tech = a.UnlockTech.CompareTo(b.UnlockTech);
                if (tech != 0) return tech;
                return string.Compare(
                    ResolveFactionDisplayName(a.FactionId),
                    ResolveFactionDisplayName(b.FactionId),
                    StringComparison.CurrentCultureIgnoreCase);
            });

            BrowserLines.Add(BrowserLine.FactionRewardHeader(
                Ui("ui.faction_2"),
                Ui("ui.reward"),
                Ui("ui.unlock"),
                Ui("ui.current_tech"),
                Ui("ui.status_2")));

            for (int i = 0; i < views.Count; i++)
            {
                FactionRewardView view = views[i];

                string stateText;
                if (view.State == 1)
                    stateText = Ui("ui.tech_lock");
                else if (view.State == 2)
                    stateText = Ui("ui.low_rep");
                else if (view.State == 3)
                    stateText = "?";
                else
                    stateText = Ui("ui.available_2");

                bool available = view.State == 0;
                BrowserLines.Add(BrowserLine.FactionReward(
                    ResolveFactionDisplayName(view.FactionId),
                    FormatFactionRewardPercent(view),
                    "T" + view.UnlockTech.ToString(CultureInfo.InvariantCulture),
                    "T" + (view.CurrentTech >= 0
                        ? view.CurrentTech.ToString(CultureInfo.InvariantCulture)
                        : "?"),
                    stateText,
                    view.FactionId,
                    available));
            }
        }

        private static string FormatFactionRewardPercent(FactionRewardView view)
        {
            if (view == null || view.State != 0) return "—";
            if (view.RewardPercent <= 0f) return "0%";
            if (view.RewardPercent < 1f) return "<1%";
            return Mathf.Clamp(Mathf.RoundToInt(view.RewardPercent), 1, 100)
                .ToString(CultureInfo.InvariantCulture) + "%";
        }

        private static int GetCurrentFactionTechLevel(string factionId)
        {
            object faction = ResolveFactionById(factionId);
            if (faction == null) return -1;
            int value;
            if (TryToInt(GetMember(faction, "CurrentTechLevel"), out value)) return value;
            if (TryToInt(GetMember(faction, "TechLevel"), out value)) return value;
            if (TryToInt(GetMember(faction, "Level"), out value)) return value;
            return -1;
        }

        private static string ResolveFactionDisplayName(string factionId)
        {
            if (IsRussian() && !string.IsNullOrEmpty(factionId))
            {
                string exactRussian = InvokeLocalizationRaw("faction." + factionId + ".name");
                if (!string.IsNullOrEmpty(exactRussian) && ContainsCyrillic(exactRussian))
                    return NormalizeGameText(exactRussian);
            }

            object runtime = ResolveFactionById(factionId);
            object record = FindDataFactionRecord(factionId);
            string raw = FirstNonEmpty(
                GetStringMember(runtime, "Name"),
                GetStringMember(runtime, "Title"),
                GetStringMember(runtime, "NameId"),
                GetStringMember(record, "Name"),
                GetStringMember(record, "Title"),
                GetStringMember(record, "NameId"),
                factionId);

            string localized = LocalizeGenericId(raw);
            if (string.IsNullOrEmpty(localized) || string.Equals(localized, raw, StringComparison.OrdinalIgnoreCase))
                localized = HumanizeIdentifier(raw);
            return NormalizeGameText(localized);
        }

        private static object ResolveFactionById(string factionId)
        {
            if (string.IsNullOrEmpty(factionId)) return null;

            object cached;
            if (RuntimeFactionsById.TryGetValue(factionId, out cached) && cached != null)
                return cached;

            EnsureTradeStateDependencies();
            if (_factionsState == null) return null;

            try
            {
                MethodInfo[] methods = _factionsState.GetType().GetMethods(InstanceFlags);
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (!string.Equals(method.Name, "Get", StringComparison.Ordinal)) continue;

                    ParameterInfo[] p = method.GetParameters();
                    object faction = null;

                    if (p.Length == 2 &&
                        p[0].ParameterType == typeof(string) &&
                        p[1].ParameterType == typeof(bool))
                    {
                        faction = method.Invoke(_factionsState, new object[] { factionId, false });
                    }
                    else if (p.Length == 1 && p[0].ParameterType == typeof(string))
                    {
                        faction = method.Invoke(_factionsState, new object[] { factionId });
                    }

                    if (faction != null)
                    {
                        RuntimeFactionsById[factionId] = faction;
                        return faction;
                    }
                }
            }
            catch { }

            return null;
        }

        private static object ResolveStationFaction(object station)
        {
            if (station == null) return null;

            string factionId = GetStringMember(station, "OwnerFactionId");
            return ResolveFactionById(factionId);
        }

        private static int ResolveFactionRelationState(string factionId, object faction)
        {
            if (faction == null) return 2;

            object[] targets = new object[] { faction, _factionsState };
            for (int t = 0; t < targets.Length; t++)
            {
                object target = targets[t];
                if (target == null) continue;

                try
                {
                    MethodInfo[] methods = target.GetType().GetMethods(InstanceFlags | StaticFlags);
                    for (int i = 0; i < methods.Length; i++)
                    {
                        MethodInfo method = methods[i];
                        if (!string.Equals(method.Name, "GetRelationType", StringComparison.Ordinal))
                            continue;

                        object[] args;
                        if (!TryBuildFactionRelationArguments(
                                method.GetParameters(), factionId, faction, out args))
                            continue;

                        try
                        {
                            object raw = method.Invoke(method.IsStatic ? null : target, args);
                            int parsed = ParseFactionRelationToken(raw);
                            if (parsed != int.MinValue)
                                return parsed;
                        }
                        catch { }
                    }
                }
                catch { }
            }

            string[] relationMembers = new string[]
            {
                "RelationType", "Relation", "PlayerRelation",
                "FactionRelation", "ReputationLevel", "ReputationRange"
            };

            for (int i = 0; i < relationMembers.Length; i++)
            {
                object raw = GetMember(faction, relationMembers[i]);
                int parsed = ParseFactionRelationToken(raw);
                if (parsed != int.MinValue)
                    return parsed;
            }

            object reputationRaw = FirstNonNull(
                GetMember(faction, "FactionReputation"),
                GetMember(faction, "PlayerReputation"),
                GetMember(faction, "Reputation"));

            double reputation;
            if (TryToDoubleSafe(reputationRaw, out reputation))
            {
                if (reputation > 0.0001) return 1;
                if (reputation < -0.0001) return -1;
                return 0;
            }

            // 2 = unresolved. Keep it distinct from a proven neutral relation (0).
            return 2;
        }

        private static bool TryBuildFactionRelationArguments(
            ParameterInfo[] parameters, string factionId, object faction, out object[] args)
        {
            args = new object[parameters == null ? 0 : parameters.Length];
            if (parameters == null || parameters.Length == 0)
                return true;

            object reputationRaw = FirstNonNull(
                GetMember(faction, "FactionReputation"),
                GetMember(faction, "PlayerReputation"),
                GetMember(faction, "Reputation"));

            double reputation;
            bool hasReputation = TryToDoubleSafe(reputationRaw, out reputation);

            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterInfo p = parameters[i];
                Type type = p.ParameterType;

                if (faction != null && type.IsInstanceOfType(faction))
                {
                    args[i] = faction;
                    continue;
                }

                if (_factionsState != null && type.IsInstanceOfType(_factionsState))
                {
                    args[i] = _factionsState;
                    continue;
                }

                if (type == typeof(string))
                {
                    args[i] = factionId;
                    continue;
                }

                if (hasReputation &&
                    (type == typeof(float) || type == typeof(double) ||
                     type == typeof(decimal) || type == typeof(int) ||
                     type == typeof(long) || type == typeof(short)))
                {
                    try
                    {
                        args[i] = Convert.ChangeType(reputation, type, CultureInfo.InvariantCulture);
                        continue;
                    }
                    catch { return false; }
                }

                if (type == typeof(bool))
                {
                    args[i] = false;
                    continue;
                }

                if (p.IsOptional)
                {
                    args[i] = p.DefaultValue;
                    continue;
                }

                return false;
            }

            return true;
        }

        private static int ParseFactionRelationToken(object raw)
        {
            if (raw == null) return int.MinValue;

            string token;
            try { token = raw.ToString(); }
            catch { return int.MinValue; }

            if (string.IsNullOrEmpty(token)) return int.MinValue;
            token = token.Trim().ToLowerInvariant();

            if (token.IndexOf("enemy", StringComparison.Ordinal) >= 0 ||
                token.IndexOf("hostile", StringComparison.Ordinal) >= 0 ||
                token.IndexOf("war", StringComparison.Ordinal) >= 0 ||
                token.IndexOf("bad", StringComparison.Ordinal) >= 0 ||
                token.IndexOf("negative", StringComparison.Ordinal) >= 0)
                return -1;

            if (token.IndexOf("friend", StringComparison.Ordinal) >= 0 ||
                token.IndexOf("friendly", StringComparison.Ordinal) >= 0 ||
                token.IndexOf("ally", StringComparison.Ordinal) >= 0 ||
                token.IndexOf("allied", StringComparison.Ordinal) >= 0 ||
                token.IndexOf("good", StringComparison.Ordinal) >= 0 ||
                token.IndexOf("positive", StringComparison.Ordinal) >= 0)
                return 1;

            if (token.IndexOf("neutral", StringComparison.Ordinal) >= 0 ||
                token.IndexOf("peace", StringComparison.Ordinal) >= 0 ||
                token.IndexOf("normal", StringComparison.Ordinal) >= 0)
                return 0;

            return int.MinValue;
        }

        private static object FindDataFactionRecord(string factionId)
        {
            if (string.IsNullOrEmpty(factionId)) return null;

            object container = GetStaticMember(typeof(Data), "Factions");
            if (container == null) return null;

            IDictionary dictionary = container as IDictionary;
            if (dictionary != null)
            {
                foreach (DictionaryEntry entry in dictionary)
                {
                    string key = ConvertToStableString(entry.Key);
                    if (string.Equals(key, factionId, StringComparison.OrdinalIgnoreCase))
                        return entry.Value;
                }
            }

            IEnumerable enumerable = container as IEnumerable;
            if (enumerable != null && !(container is string))
            {
                int count = 0;
                foreach (object entry in enumerable)
                {
                    if (++count > 512) break;

                    string key = ConvertToStableString(GetMember(entry, "Key"));
                    object value = GetMember(entry, "Value");

                    if (string.Equals(key, factionId, StringComparison.OrdinalIgnoreCase))
                        return value ?? entry;

                    string id = FirstNonEmpty(
                        GetStringMember(entry, "Id"),
                        GetStringMember(entry, "FactionId"),
                        GetStringMember(value, "Id"),
                        GetStringMember(value, "FactionId"));

                    if (string.Equals(id, factionId, StringComparison.OrdinalIgnoreCase))
                        return value ?? entry;
                }
            }

            return null;
        }

        private static Sprite TryResolveFactionSmallIcon(string factionId, object faction)
        {
            if (string.IsNullOrEmpty(factionId)) return null;

            Sprite cached;
            if (FactionSmallIcons.TryGetValue(factionId, out cached) && cached != null)
                return cached;

            if (FactionIconMisses.Contains(factionId))
                return null;

            List<object> nodes = new List<object>();
            HashSet<object> seen = new HashSet<object>(ReferenceComparer.Instance);

            AddFactionVisualNode(nodes, seen, faction);
            AddFactionVisualNode(nodes, seen, FindDataFactionRecord(factionId));

            for (int i = 0; i < nodes.Count && i < 24; i++)
            {
                object node = nodes[i];
                if (node == null) continue;

                Sprite direct = node as Sprite;
                if (direct != null)
                {
                    FactionSmallIcons[factionId] = direct;
                    return direct;
                }

                Sprite resolved = TryInvokeItemIconResolver(node);
                if (resolved != null)
                {
                    FactionSmallIcons[factionId] = resolved;
                    return resolved;
                }

                string[] iconNames = new string[]
                {
                    "SmallIcon", "Icon", "FactionIcon", "FlagIcon",
                    "Logo", "Emblem", "TooltipIcon", "TooltipIconTag",
                    "FactionTypeIcon", "IconTag", "Sprite"
                };

                for (int n = 0; n < iconNames.Length; n++)
                {
                    object token = GetMember(node, iconNames[n]);
                    if (token == null) continue;

                    resolved = ResolveIconToken(token, 0);
                    if (resolved != null)
                    {
                        FactionSmallIcons[factionId] = resolved;
                        return resolved;
                    }
                }

                string[] childNames = new string[]
                {
                    "Record", "FactionRecord", "Descriptor", "FactionDescriptor",
                    "CachedData", "Data", "Visual", "Visuals"
                };

                for (int n = 0; n < childNames.Length && nodes.Count < 24; n++)
                    AddFactionVisualNode(nodes, seen, GetMember(node, childNames[n]));
            }

            Sprite fallback = TryBuildFactionFallbackIcon(factionId);
            if (fallback != null)
            {
                FactionSmallIcons[factionId] = fallback;
                return fallback;
            }

            FactionIconMisses.Add(factionId);

            if (!_factionTradeSchemaLogged)
            {
                _factionTradeSchemaLogged = true;
                Debug.Log("[ItemIntelligence] Faction trade visuals: owner=" + factionId +
                    ", runtimeType=" + (faction == null ? "null" : faction.GetType().FullName) +
                    ", icon=unresolved.");
            }

            return null;
        }

        private static Sprite TryBuildFactionFallbackIcon(string factionId)
        {
            if (string.IsNullOrEmpty(factionId)) return null;
            string key = factionId.Trim().ToLowerInvariant();
            if (key.IndexOf("quasimorph", StringComparison.Ordinal) < 0 &&
                key.IndexOf("quasi", StringComparison.Ordinal) < 0)
                return null;

            if (_quasimorphFallbackFactionIcon != null) return _quasimorphFallbackFactionIcon;

            Texture2D tex = new Texture2D(16, 16, TextureFormat.ARGB32, false);
            tex.filterMode = FilterMode.Point;
            Color clear = new Color(0f, 0f, 0f, 0f);
            Color main = new Color(0.66f, 0.94f, 0.86f, 1f);
            Color accent = new Color(0.22f, 0.74f, 0.66f, 1f);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                    tex.SetPixel(x, y, clear);

            int[,] mask = new int[,]
            {
                {7,1},{6,2},{7,2},{8,2},{5,3},{6,3},{8,3},{9,3},{4,4},{5,4},{9,4},{10,4},
                {3,5},{4,5},{10,5},{11,5},{2,6},{3,6},{11,6},{12,6},{2,7},{12,7},{2,8},{12,8},
                {2,9},{3,9},{11,9},{12,9},{3,10},{4,10},{10,10},{11,10},{4,11},{5,11},{9,11},{10,11},
                {5,12},{6,12},{8,12},{9,12},{6,13},{7,13},{8,13},{7,14}
            };
            for (int i = 0; i < mask.GetLength(0); i++)
                tex.SetPixel(mask[i,0], mask[i,1], main);
            tex.SetPixel(7,7, accent);
            tex.SetPixel(8,7, accent);
            tex.SetPixel(7,8, accent);
            tex.SetPixel(8,8, accent);
            tex.Apply(false, true);

            _quasimorphFallbackFactionIcon = Sprite.Create(tex, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f), 16f);
            _quasimorphFallbackFactionIcon.name = "QII_QuasimorphFactionFallback";
            return _quasimorphFallbackFactionIcon;
        }

        private static void AddFactionVisualNode(List<object> nodes, HashSet<object> seen, object value)
        {
            if (nodes == null || seen == null || value == null) return;
            if (seen.Contains(value)) return;

            seen.Add(value);
            nodes.Add(value);
        }
    }
}
