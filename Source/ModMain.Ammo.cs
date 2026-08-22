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

        // v1.7.36-test2: feature-owned state moved out of Runtime.cs.
        // Declaration ownership only; lifecycle and behavior are unchanged.

        private static readonly Dictionary<string, WeaponInfo> WeaponsByItem = new Dictionary<string, WeaponInfo>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, List<string>> CompatibleWeaponsByAmmo = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        // FireMode visuals are properties of the FireMode record, not of a specific weapon.
        // Cache one Sprite per raw FireMode id instead of duplicating the same reference
        // for every weapon->mode relation (841 links / 89 runtime records on 1.0.1).
        private static readonly Dictionary<string, Sprite> WeaponModeIconsByRawId = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, object> WeaponModeRecordsById = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, List<string>> WeaponModeIdsByItem = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> WeaponModeRawIdByKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> WeaponModeItemIdByKey = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly HashSet<string> WeaponModeIconMisses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Production datadisks ("chips") are discovered from the same item descriptor graph
        // already traversed for ammo/weapon relationships, so this adds no second item scan.
        private static readonly Dictionary<string, List<string>> DatadisksByUnlockedItem = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        // Forward view of the same datadisk graph used by recipe chip indicators.
        // It is populated during the existing ammo/datadisk warmup, so Overview adds no second item scan.
        private static readonly Dictionary<string, List<string>> ItemsUnlockedByDatadisk = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        // Canonical MGSC.DatadiskRecord.UnlockIds probability cache. Duplicates are
        // preserved as hit counts because ItemFactory chooses a uniform raw list index.
        private static readonly Dictionary<string, Dictionary<string, int>> UnlockHitCountsByDatadisk =
            new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, int> UnlockPoolSizeByDatadisk =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        // The percentage UI is enabled only after a tiny read-only IL check proves the current
        // vanilla selection path still uses UnlockIds -> Count -> Random.Range -> get_Item -> SetUnlockId.
        private static bool _chipUnlockChanceContractChecked;
        private static bool _chipUnlockChanceContractVerified;
        private static string _chipUnlockChanceContractReason = "not checked";
        private static readonly HashSet<string> ProductionDatadiskItemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static object _unlockedProductionItems;
        private static bool _unlockedProductionResolveAttempted;
        private static Sprite _qiiUnlockedMarkerSprite;
        private static Sprite _qiiLockedMarkerSprite;
        private static Sprite _qiiNoDatadiskSprite;

        private static readonly List<KeyValuePair<string, object>> AmmoWarmupItems = new List<KeyValuePair<string, object>>();
        private static readonly Dictionary<string, HashSet<string>> AmmoWarmupKeysByItem = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<WeaponInfo> AmmoWarmupWeapons = new List<WeaponInfo>();
        private static readonly HashSet<string> AmmoFinalizeCompatibleBuffer =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static int _ammoWarmupIndex;
        private static int _ammoWarmupPhase; // 0 analyze item records, 1 finalize weapon/ammo compatibility
        private static int _ammoFinalizeWeaponIndex;
        private static bool _ammoWarmupActive;
        private static bool _ammoWarmupComplete;
        private static int _ammoMeleeDescriptorSuppressedWeapons;
        private static void ResetAmmoRuntimeSessionState()
        {
            _unlockedProductionItems = null;
            _unlockedProductionResolveAttempted = false;
        }

        // v1.7.36-test5: module-owned reset entry points. These contain the exact
        // assignments previously performed inline by Runtime.ClearIndexes().
        private static void ResetAmmoKnowledgeIndexState()
        {
            DatadisksByUnlockedItem.Clear();
            ItemsUnlockedByDatadisk.Clear();
            UnlockHitCountsByDatadisk.Clear();
            UnlockPoolSizeByDatadisk.Clear();
            ProductionDatadiskItemIds.Clear();
        }

        private static void ResetAmmoWeaponIndexState()
        {
            WeaponsByItem.Clear();
            CompatibleWeaponsByAmmo.Clear();
            WeaponModeIconsByRawId.Clear();
            WeaponModeRecordsById.Clear();
            WeaponModeStatsByRawId.Clear();
            WeaponModeStatsByKey.Clear();
            WeaponModeIdsByItem.Clear();
            WeaponModeRawIdByKey.Clear();
            WeaponModeItemIdByKey.Clear();
            ResetWeaponModeScatterCache();
            WeaponModeIconMisses.Clear();
            // These buffers retain references to every item record and analyzed weapon.
            // Clear them at the index/session boundary rather than waiting for the next
            // Space warmup to overwrite them.
            AmmoWarmupItems.Clear();
            AmmoWarmupKeysByItem.Clear();
            AmmoWarmupWeapons.Clear();
            AmmoFinalizeCompatibleBuffer.Clear();
            AmmoFinalizeCompatibleBuffer.Clear();
            _ammoWarmupIndex = 0;
            _ammoWarmupPhase = 0;
            _ammoFinalizeWeaponIndex = 0;
            _ammoMeleeDescriptorSuppressedWeapons = 0;
            _ammoWarmupActive = false;
            _ammoWarmupComplete = false;
        }

        private static void StartAmmoFeatureWarmup()
        {
            if (!_compatAmmo) return;
            try { StartAmmoIndexWarmup(); }
            catch (Exception ex)
            {
                StopAmmoFeatureFrameWork();
                TripCompatibilityFeatureRuntime("Ammo", ex);
            }
        }

        private static void TickAmmoFeatureFrameWork()
        {
            if (!_compatAmmo) return;
            try { TickAmmoIndexWarmup(); }
            catch (Exception ex)
            {
                StopAmmoFeatureFrameWork();
                TripCompatibilityFeatureRuntime("Ammo", ex);
            }
        }

        private static void StopAmmoFeatureFrameWork()
        {
            _ammoWarmupActive = false;
            AmmoWarmupItems.Clear();
            AmmoWarmupKeysByItem.Clear();
            AmmoWarmupWeapons.Clear();
            AmmoFinalizeCompatibleBuffer.Clear();
            _ammoWarmupIndex = 0;
            _ammoWarmupPhase = 0;
            _ammoFinalizeWeaponIndex = 0;
        }

        private static string GetAmmoWarmupStatus()
        {
            return !_compatAmmo
                ? "disabled"
                : (_ammoWarmupActive ? "pending" : "complete");
        }

        private static void StartAmmoIndexWarmup()
        {
            WeaponsByItem.Clear();
            CompatibleWeaponsByAmmo.Clear();
            WeaponModeIconsByRawId.Clear();
            WeaponModeRecordsById.Clear();
            WeaponModeStatsByRawId.Clear();
            WeaponModeStatsByKey.Clear();
            WeaponModeIdsByItem.Clear();
            WeaponModeRawIdByKey.Clear();
            WeaponModeItemIdByKey.Clear();
            ResetWeaponModeScatterCache();
            WeaponModeIconMisses.Clear();
            BuildWeaponModeRecordIndex();
            BuildWeaponModeRelationIndex();
            AmmoWarmupItems.Clear();
            AmmoWarmupKeysByItem.Clear();
            AmmoWarmupWeapons.Clear();
            _ammoWarmupIndex = 0;
            _ammoWarmupPhase = 0;
            _ammoFinalizeWeaponIndex = 0;
            _ammoWarmupComplete = false;

            foreach (KeyValuePair<string, object> pair in ItemRecordsById)
                AmmoWarmupItems.Add(pair);

            _ammoWarmupActive = AmmoWarmupItems.Count > 0;
            if (!_ammoWarmupActive)
            {
                _ammoWarmupComplete = true;
                return;
            }

            Debug.Log("[ItemIntelligence] Ammo descriptor warmup queued: " + AmmoWarmupItems.Count + " item records.");
        }

        private static void TickAmmoIndexWarmup()
        {
            if (!_ammoWarmupActive) return;

            const double frameBudgetMs = 1.25;
            long started = System.Diagnostics.Stopwatch.GetTimestamp();

            if (_ammoWarmupPhase == 0)
            {
                int budget = 18;
                while (budget-- > 0 && _ammoWarmupIndex < AmmoWarmupItems.Count &&
                       !PerformanceBudgetExceeded(started, frameBudgetMs))
                {
                    KeyValuePair<string, object> pair = AmmoWarmupItems[_ammoWarmupIndex++];
                    AnalyzeAmmoItemGraph(pair.Key, pair.Value);
                }

                if (_ammoWarmupIndex < AmmoWarmupItems.Count) return;
                _ammoWarmupPhase = 1;
                _ammoFinalizeWeaponIndex = 0;
                // Finalization gets its own frame slice. Do not let the last analyzed
                // record inherit an unbounded weapon x ammo compatibility pass.
                return;
            }

            if (_ammoWarmupPhase == 1)
            {
                int weaponBudget = 12;
                while (weaponBudget-- > 0 && _ammoFinalizeWeaponIndex < AmmoWarmupWeapons.Count &&
                       !PerformanceBudgetExceeded(started, frameBudgetMs))
                {
                    FinalizeAmmoWarmupWeapon(AmmoWarmupWeapons[_ammoFinalizeWeaponIndex++]);
                }
                if (_ammoFinalizeWeaponIndex < AmmoWarmupWeapons.Count) return;
                CompleteAmmoWarmup();
            }
        }

        private static void CompleteAmmoWarmup()
        {
            _ammoWarmupActive = false;
            _ammoWarmupComplete = true;

            Debug.Log("[ItemIntelligence] Ammo descriptor warmup complete: itemRecords=" + ItemRecordsById.Count +
                ", weapons=" + WeaponsByItem.Count +
                ", ammoItems=" + AmmoWarmupKeysByItem.Count +
                ", ammoItemsWithWeapons=" + CompatibleWeaponsByAmmo.Count +
                ", weaponModeRecords=" + WeaponModeRecordsById.Count +
                ", weaponModeStats=" + WeaponModeStatsByRawId.Count +
                ", weaponsWithModes=" + CountWeaponsWithModes().ToString(CultureInfo.InvariantCulture) +
                ", weaponModes=" + CountIndexedWeaponModes().ToString(CultureInfo.InvariantCulture) +
                ", modeIcons=" + WeaponModeIconsByRawId.Count +
                ", datadiskUnlockedItems=" + DatadisksByUnlockedItem.Count +
                ", datadiskUnlockSources=" + ItemsUnlockedByDatadisk.Count +
                ", chipPoolsCanonical=" + UnlockPoolSizeByDatadisk.Count.ToString(CultureInfo.InvariantCulture) +
                ", chipPoolSource=MGSC.DatadiskRecord.UnlockIds" +
                ", meleeDescriptorSuppressedWeapons=" + _ammoMeleeDescriptorSuppressedWeapons.ToString(CultureInfo.InvariantCulture) + ".");
            AuditAmmoRelationsAfterWarmup();

            if (WeaponsByItem.Count == 0)
            {
                LogItemGraphDiagnostic("anc_shotgun_1");
                LogItemGraphDiagnostic("battery_basic_ammo");
            }

            AmmoWarmupItems.Clear();
            AmmoWarmupKeysByItem.Clear();
            AmmoWarmupWeapons.Clear();
            _ammoWarmupIndex = 0;
            _ammoWarmupPhase = 0;
            _ammoFinalizeWeaponIndex = 0;

            if (_inspectorOpen && (BrowserNavigation.Tab == (int)BrowserTabId.Ammo || BrowserNavigation.Tab == (int)BrowserTabId.Overview))
                RenderBrowser(_inspectorItemId);
        }

        private static void AnalyzeAmmoItemGraph(string itemId, object record)
        {
            if (string.IsNullOrEmpty(itemId) || record == null) return;

            List<object> graph = BuildRelevantItemGraph(record, 4, 72);
            AnalyzeDatadiskGraph(itemId, graph);

            object weaponPayload = null;
            object ammoPayload = null;

            for (int i = 0; i < graph.Count; i++)
            {
                object node = graph[i];
                if (node == null) continue;
                Type type = node.GetType();
                string typeName = type.Name ?? string.Empty;

                bool weaponishType =
                    typeName.IndexOf("WeaponRecord", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    typeName.IndexOf("WeaponDescriptor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    typeName.IndexOf("Firearm", StringComparison.OrdinalIgnoreCase) >= 0;

                bool weaponishMembers =
                    HasMemberNamed(type, "RequiredAmmo") ||
                    HasMemberNamed(type, "RequiredAmmoType") ||
                    HasMemberNamed(type, "DefaultAmmoId") ||
                    HasMemberNamed(type, "OverrideAmmo");

                if (weaponPayload == null && (weaponishType || weaponishMembers) &&
                    typeName.IndexOf("Throwing", StringComparison.OrdinalIgnoreCase) < 0)
                    weaponPayload = node;

                bool ammoishType =
                    typeName.IndexOf("AmmoRecord", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    typeName.IndexOf("AmmoDescriptor", StringComparison.OrdinalIgnoreCase) >= 0;

                bool ammoishMembers =
                    HasMemberNamed(type, "AmmoType") ||
                    HasMemberNamed(type, "CurrentAmmoType");

                if (ammoPayload == null && (ammoishType || ammoishMembers) && !weaponishMembers)
                    ammoPayload = node;
            }

            if (weaponPayload != null)
            {
                object nestedWeapon = FirstNonNull(
                    GetMember(weaponPayload, "WeaponRecord"),
                    GetMember(weaponPayload, "Record"));
                if (nestedWeapon != null)
                {
                    Type nestedType = nestedWeapon.GetType();
                    if (HasMemberNamed(nestedType, "RequiredAmmo") ||
                        HasMemberNamed(nestedType, "RequiredAmmoType") ||
                        nestedType.Name.IndexOf("WeaponRecord", StringComparison.OrdinalIgnoreCase) >= 0)
                        weaponPayload = nestedWeapon;
                }
            }

            if (ammoPayload != null)
            {
                HashSet<string> itemKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < graph.Count; i++)
                {
                    object node = graph[i];
                    if (node == null) continue;
                    Type t = node.GetType();
                    string n = t.Name ?? string.Empty;
                    if (n.IndexOf("Ammo", StringComparison.OrdinalIgnoreCase) < 0 &&
                        !HasMemberNamed(t, "AmmoType") &&
                        !HasMemberNamed(t, "CurrentAmmoType"))
                        continue;

                    HashSet<string> keys = ExtractAmmoKeysFromRecord(node, false);
                    foreach (string key in keys) itemKeys.Add(key);
                    AddAmmoKeys(GetMember(node, "AmmoType"), itemKeys, 0);
                    AddAmmoKeys(GetMember(node, "CurrentAmmoType"), itemKeys, 0);
                }

                if (itemKeys.Count > 0)
                    AmmoWarmupKeysByItem[itemId] = itemKeys;
            }

            if (weaponPayload == null) return;

            HashSet<string> requiredKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> directAmmoIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, int> overrides = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < graph.Count; i++)
            {
                object node = graph[i];
                if (node == null) continue;
                Type t = node.GetType();
                string n = t.Name ?? string.Empty;
                bool weaponNode =
                    object.ReferenceEquals(node, weaponPayload) ||
                    n.IndexOf("Weapon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    HasMemberNamed(t, "RequiredAmmo") ||
                    HasMemberNamed(t, "RequiredAmmoType");
                if (!weaponNode) continue;

                HashSet<string> keys = ExtractAmmoKeysFromRecord(node, true);
                foreach (string key in keys) requiredKeys.Add(key);
                AddAmmoKeys(GetMember(node, "RequiredAmmoType"), requiredKeys, 0);
                AddAmmoKeys(GetMember(node, "RequiredAmmo"), requiredKeys, 0);

                string defaultAmmo = FirstNonEmpty(
                    GetStringMember(node, "DefaultAmmoId"),
                    GetItemIdDeep(GetMember(node, "DefaultAmmo"), 0));
                if (!string.IsNullOrEmpty(defaultAmmo)) directAmmoIds.Add(defaultAmmo);

                Dictionary<string, int> nodeOverrides = ExtractItemQuantities(GetMember(node, "OverrideAmmo"));
                foreach (KeyValuePair<string, int> ov in nodeOverrides)
                {
                    directAmmoIds.Add(ov.Key);
                    AddQuantity(overrides, ov.Key, ov.Value);
                }

                object requiredAmmo = GetMember(node, "RequiredAmmo");
                string directRequired = GetItemIdDeep(requiredAmmo, 0);
                if (!string.IsNullOrEmpty(directRequired) && KnownItemIds.Contains(directRequired))
                    directAmmoIds.Add(directRequired);

                Dictionary<string, int> requiredItems = ExtractItemQuantities(requiredAmmo);
                foreach (string id in requiredItems.Keys)
                    if (KnownItemIds.Contains(id)) directAmmoIds.Add(id);
            }

            WeaponInfo info = new WeaponInfo(itemId, requiredKeys, directAmmoIds, overrides);
            CollectWeaponModes(itemId, record, graph, weaponPayload, info);
            WeaponsByItem[itemId] = info;
            AmmoWarmupWeapons.Add(info);
        }

        private static int CountIndexedWeaponModes()
        {
            int count = 0;
            for (int i = 0; i < AmmoWarmupWeapons.Count; i++)
            {
                WeaponInfo info = AmmoWarmupWeapons[i];
                if (info != null && info.Modes != null) count += info.Modes.Count;
            }
            return count;
        }

        private static int CountWeaponsWithModes()
        {
            int count = 0;
            for (int i = 0; i < AmmoWarmupWeapons.Count; i++)
            {
                WeaponInfo info = AmmoWarmupWeapons[i];
                if (info != null && info.Modes != null && info.Modes.Count > 0) count++;
            }
            return count;
        }

        private static bool ShouldSuppressDescriptorAmmoForMelee(WeaponInfo weapon)
        {
            if (weapon == null || weapon.Modes == null || weapon.Modes.Count == 0) return false;
            bool sawStaticMelee = false;
            for (int i = 0; i < weapon.Modes.Count; i++)
            {
                WeaponModeDescriptor mode = weapon.Modes[i];
                if (mode == null) continue;
                if (mode.Stats != null && mode.Stats.AmmoPerShot > 0) return false;
                WeaponRecord record = ResolveWeaponModeWeaponRecord(mode.Key);
                if (record == null) continue;
                if (!record.IsMelee) return false;
                sawStaticMelee = true;
            }
            return sawStaticMelee;
        }

        private static void FinalizeAmmoWarmupWeapon(WeaponInfo weapon)
        {
            if (weapon == null) return;
            AmmoFinalizeCompatibleBuffer.Clear();
            HashSet<string> compatible = AmmoFinalizeCompatibleBuffer;
            bool suppressMeleeInference = ShouldSuppressDescriptorAmmoForMelee(weapon);
            if (suppressMeleeInference && (weapon.RequiredAmmoKeys.Count > 0 || weapon.DirectAmmoIds.Count > 0))
                _ammoMeleeDescriptorSuppressedWeapons++;

            if (!suppressMeleeInference)
            {
                foreach (string direct in weapon.DirectAmmoIds)
                    if (KnownItemIds.Contains(direct)) compatible.Add(direct);

                foreach (string overrideId in weapon.OverrideAmmo.Keys)
                    if (KnownItemIds.Contains(overrideId)) compatible.Add(overrideId);
            }
            else
            {
                // Static non-energy melee does not consume ammunition. Vanilla records may
                // still carry DefaultAmmo/OverrideAmmo/RequiredAmmo relation ids for attack
                // profiles or internal variants; none of those are player ammunition.
                // Energy/resource-consuming melee is preserved because AmmoPerShot > 0
                // makes ShouldSuppressDescriptorAmmoForMelee return false.
                weapon.CompatibleAmmo.Clear();
            }

            if (weapon.RequiredAmmoKeys.Count > 0 && !suppressMeleeInference)
            {
                foreach (KeyValuePair<string, HashSet<string>> ammo in AmmoWarmupKeysByItem)
                {
                    if (AmmoKeysIntersect(weapon.RequiredAmmoKeys, ammo.Value))
                        compatible.Add(ammo.Key);
                }
            }

            foreach (string ammoId in compatible)
            {
                List<string> existing;
                bool duplicate = CompatibleWeaponsByAmmo.TryGetValue(ammoId, out existing) &&
                                 existing != null &&
                                 existing.Contains(weapon.ItemId);
                if (!duplicate)
                    AddToList(CompatibleWeaponsByAmmo, ammoId, weapon.ItemId);

                if (!weapon.CompatibleAmmo.Contains(ammoId))
                    weapon.CompatibleAmmo.Add(ammoId);
            }
        }

        private static void CollectWeaponModes(string itemId, object itemRoot, List<object> graph, object weaponPayload, WeaponInfo info)
        {
            if (string.IsNullOrEmpty(itemId) || graph == null || weaponPayload == null || info == null) return;

            List<object> candidates = new List<object>();

            // test18: first consume the exact relation index built from native Data weapon/
            // attack tables. No relation is accepted unless the item id exists in the live
            // item table and the mode id exists in the live FireMode table.
            List<string> indexedModeIds;
            if (WeaponModeIdsByItem.TryGetValue(itemId, out indexedModeIds) && indexedModeIds != null)
            {
                for (int i = 0; i < indexedModeIds.Count; i++)
                {
                    object indexedRecord;
                    if (WeaponModeRecordsById.TryGetValue(indexedModeIds[i], out indexedRecord) && indexedRecord != null)
                        AddModeCandidateUnique(candidates, indexedRecord);
                }
            }

            // test17: the first implementation knew the global FireMode table but linked
            // zero modes because weapon records do not consistently expose the relation
            // through members literally named FireMode/AttackMode. Search only this
            // weapon's bounded record graph for exact references to ids that are already
            // proven members of Data's FireMode tables. This stays runtime-driven and
            // cannot invent a mode from a substring or damage-type association.
            HashSet<object> directSeen = new HashSet<object>(ReferenceComparer.Instance);
            int directBudget = 240;
            CollectKnownWeaponModeReferences(candidates, weaponPayload, 0, directSeen, ref directBudget);
            for (int i = 0; i < graph.Count && directBudget > 0; i++)
            {
                object graphNode = graph[i];
                if (graphNode == null) continue;
                CollectKnownWeaponModeReferences(candidates, graphNode, 0, directSeen, ref directBudget);
            }

            // Keep the older semantic path as a secondary source for builds/mods that
            // expose actual FireMode record objects instead of ids.
            AddWeaponModeCandidatesFromObject(candidates, weaponPayload, 0);
            for (int i = 0; i < graph.Count; i++)
            {
                object node = graph[i];
                if (node == null || object.ReferenceEquals(node, weaponPayload)) continue;
                if (LooksLikeWeaponModeNode(node))
                    AddModeCandidateUnique(candidates, node);
            }

            // Last conservative fallback: if neither the native relation index nor the
            // semantic traversal found anything, exhaustively walk only this item's data
            // record graph. Acceptance is still exact-id-only, so unrelated text/damage
            // associations cannot fabricate a mode. The fallback is bounded and runs inside
            // the already time-sliced ammo warmup.
            if (candidates.Count == 0 && itemRoot != null)
            {
                HashSet<object> exhaustiveSeen = new HashSet<object>(ReferenceComparer.Instance);
                int exhaustiveBudget = 1200;
                CollectKnownWeaponModeReferencesExhaustive(candidates, itemRoot, 0, exhaustiveSeen, ref exhaustiveBudget);
            }

            HashSet<string> seenModeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int ordinal = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                WeaponModeDescriptor mode = BuildWeaponModeDescriptor(itemId, candidates[i], ordinal++);
                if (mode == null || string.IsNullOrEmpty(mode.Label)) continue;

                // Different variants can intentionally share the same human label
                // (for example precise/strong melee modes). Dedupe by the actual
                // runtime FireMode id, never by presentation text.
                string identity = string.IsNullOrEmpty(mode.RawId) ? mode.Key : mode.RawId;
                if (!seenModeIds.Add(identity)) continue;

                info.Modes.Add(mode);
            }
        }

        private static void BuildWeaponModeRelationIndex()
        {
            WeaponModeIdsByItem.Clear();
            if (WeaponModeRecordsById.Count == 0 || KnownItemIds.Count == 0) return;

            int started = Environment.TickCount;

            try
            {
                List<MemberInfo> members = GetStaticDataMembers();
                for (int i = 0; i < members.Count; i++)
                {
                    MemberInfo member = members[i];
                    if (member == null) continue;
                    string memberName = member.Name ?? string.Empty;
                    Type declaredType = GetMemberDeclaredType(member);
                    string declaredName = declaredType == null ? string.Empty : (declaredType.Name ?? string.Empty);
                    string semantic = memberName + " " + declaredName;
                    if (semantic.IndexOf("Weapon", StringComparison.OrdinalIgnoreCase) < 0 &&
                        semantic.IndexOf("Firearm", StringComparison.OrdinalIgnoreCase) < 0 &&
                        semantic.IndexOf("Attack", StringComparison.OrdinalIgnoreCase) < 0 &&
                        semantic.IndexOf("FireMode", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    object collection = null;
                    try { collection = GetMemberValue(null, member); } catch { }
                    if (collection == null || collection is string) continue;

                    List<DataEntry> records;
                    try { records = EnumerateData(collection); } catch { continue; }
                    if (records == null || records.Count == 0) continue;

                    for (int r = 0; r < records.Count; r++)
                    {
                        DataEntry entry = records[r];
                        object record = entry == null ? null : entry.Value;
                        if (record == null) continue;

                        string itemId = ResolveDirectWeaponItemId(entry, record);
                        if (string.IsNullOrEmpty(itemId)) continue;

                        HashSet<string> modeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        HashSet<object> visited = new HashSet<object>(ReferenceComparer.Instance);
                        int budget = 500;
                        CollectExactWeaponModeIds(modeIds, record, 0, visited, ref budget);
                        if (modeIds.Count == 0) continue;

                        foreach (string modeId in modeIds)
                            AddUniqueString(WeaponModeIdsByItem, itemId, modeId);
                    }

                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ItemIntelligence] Weapon mode relation index failed safely: " + ex.GetType().Name + ": " + ex.Message);
            }

            int elapsed = unchecked(Environment.TickCount - started);
            if (elapsed < 0) elapsed = 0;
            int relationLinks = CountWeaponModeRelationLinks();
            if (relationLinks > 0)
            {
                Debug.Log("[ItemIntelligence] Weapon mode relation index: weapons=" +
                    WeaponModeIdsByItem.Count.ToString(CultureInfo.InvariantCulture) +
                    ", links=" + relationLinks.ToString(CultureInfo.InvariantCulture) +
                    ", build=" + elapsed.ToString(CultureInfo.InvariantCulture) + " ms.");
            }
        }

        private static int CountWeaponModeRelationLinks()
        {
            int total = 0;
            foreach (KeyValuePair<string, List<string>> pair in WeaponModeIdsByItem)
                if (pair.Value != null) total += pair.Value.Count;
            return total;
        }

        private static string ResolveDirectWeaponItemId(DataEntry entry, object record)
        {
            if (record == null) return string.Empty;
            string[] candidates = new string[]
            {
                entry == null ? string.Empty : entry.Key,
                GetStringMember(record, "ItemId"),
                GetStringMember(record, "WeaponItemId"),
                GetStringMember(record, "WeaponId"),
                GetStringMember(record, "Id"),
                GetItemIdDeep(GetMember(record, "Item"), 0),
                GetItemIdDeep(GetMember(record, "Weapon"), 0)
            };
            for (int i = 0; i < candidates.Length; i++)
            {
                string candidate = candidates[i];
                if (string.IsNullOrEmpty(candidate)) continue;
                candidate = candidate.Trim();
                if (KnownItemIds.Contains(candidate)) return candidate;
            }
            return string.Empty;
        }

        private static void CollectExactWeaponModeIds(
            HashSet<string> result, object value, int depth, HashSet<object> visited, ref int budget)
        {
            if (result == null || value == null || budget <= 0 || depth > 5) return;
            budget--;

            string direct = value as string;
            if (direct != null)
            {
                direct = direct.Trim();
                if (WeaponModeRecordsById.ContainsKey(direct)) result.Add(direct);
                return;
            }

            Type type = value.GetType();
            if (type.IsEnum || type.IsPrimitive || value is decimal || value is DateTime || value is Guid) return;
            if (value is Type || value is MemberInfo || value is Delegate || value is UnityEngine.Object) return;

            if (LooksLikeWeaponModeNode(value))
            {
                string recordId = FirstNonEmpty(
                    GetStringMember(value, "Id"),
                    GetStringMember(value, "ModeId"),
                    GetStringMember(value, "FireModeId"));
                if (!string.IsNullOrEmpty(recordId) && WeaponModeRecordsById.ContainsKey(recordId)) result.Add(recordId);
            }

            IDictionary dict = value as IDictionary;
            if (dict != null)
            {
                int count = 0;
                foreach (DictionaryEntry entry in dict)
                {
                    if (++count > 64 || budget <= 0) break;
                    CollectExactWeaponModeIds(result, entry.Key, depth + 1, visited, ref budget);
                    CollectExactWeaponModeIds(result, entry.Value, depth + 1, visited, ref budget);
                }
                return;
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null && !(value is string))
            {
                int count = 0;
                foreach (object entry in enumerable)
                {
                    if (++count > 64 || budget <= 0) break;
                    CollectExactWeaponModeIds(result, entry, depth + 1, visited, ref budget);
                }
                return;
            }

            if (visited != null)
            {
                if (visited.Contains(value)) return;
                visited.Add(value);
            }

            List<MemberInfo> members = GetReadableMembers(type);
            for (int i = 0; i < members.Count && budget > 0; i++)
            {
                object nested = null;
                try { nested = GetMemberValue(value, members[i]); } catch { }
                if (nested == null || object.ReferenceEquals(nested, value)) continue;
                CollectExactWeaponModeIds(result, nested, depth + 1, visited, ref budget);
            }
        }

        private static void CollectKnownWeaponModeReferencesExhaustive(
            List<object> candidates, object value, int depth, HashSet<object> visited, ref int budget)
        {
            if (candidates == null || value == null || budget <= 0 || depth > 6) return;
            budget--;

            string directId = value as string;
            if (directId != null)
            {
                object directRecord;
                if (WeaponModeRecordsById.TryGetValue(directId.Trim(), out directRecord) && directRecord != null)
                    AddModeCandidateUnique(candidates, directRecord);
                return;
            }

            Type type = value.GetType();
            if (type.IsEnum || type.IsPrimitive || value is decimal || value is DateTime || value is Guid) return;
            if (value is Type || value is MemberInfo || value is Delegate || value is UnityEngine.Object) return;

            if (LooksLikeWeaponModeNode(value)) AddModeCandidateUnique(candidates, value);

            IDictionary dict = value as IDictionary;
            if (dict != null)
            {
                int n = 0;
                foreach (DictionaryEntry entry in dict)
                {
                    if (++n > 64 || budget <= 0) break;
                    CollectKnownWeaponModeReferencesExhaustive(candidates, entry.Key, depth + 1, visited, ref budget);
                    CollectKnownWeaponModeReferencesExhaustive(candidates, entry.Value, depth + 1, visited, ref budget);
                }
                return;
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null && !(value is string))
            {
                int n = 0;
                foreach (object entry in enumerable)
                {
                    if (++n > 64 || budget <= 0) break;
                    CollectKnownWeaponModeReferencesExhaustive(candidates, entry, depth + 1, visited, ref budget);
                }
                return;
            }

            if (visited != null)
            {
                if (visited.Contains(value)) return;
                visited.Add(value);
            }

            List<MemberInfo> members = GetReadableMembers(type);
            for (int i = 0; i < members.Count && budget > 0; i++)
            {
                object nested = null;
                try { nested = GetMemberValue(value, members[i]); } catch { }
                if (nested == null || object.ReferenceEquals(nested, value)) continue;
                CollectKnownWeaponModeReferencesExhaustive(candidates, nested, depth + 1, visited, ref budget);
            }
        }

        private static void CollectKnownWeaponModeReferences(
            List<object> candidates, object value, int depth, HashSet<object> visited, ref int budget)
        {
            if (candidates == null || value == null || budget <= 0 || depth > 3) return;
            budget--;

            string directId = value as string;
            if (directId != null)
            {
                object directRecord;
                if (WeaponModeRecordsById.TryGetValue(directId.Trim(), out directRecord) && directRecord != null)
                    AddModeCandidateUnique(candidates, directRecord);
                return;
            }

            Type type = value.GetType();
            if (IsSimple(type)) return;

            if (LooksLikeWeaponModeNode(value))
                AddModeCandidateUnique(candidates, value);

            IDictionary dict = value as IDictionary;
            if (dict != null)
            {
                int n = 0;
                foreach (DictionaryEntry entry in dict)
                {
                    if (++n > 32 || budget <= 0) break;
                    if (entry.Key != null) CollectKnownWeaponModeReferences(candidates, entry.Key, depth + 1, visited, ref budget);
                    if (entry.Value != null) CollectKnownWeaponModeReferences(candidates, entry.Value, depth + 1, visited, ref budget);
                }
                return;
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null && !(value is string))
            {
                int n = 0;
                foreach (object entry in enumerable)
                {
                    if (++n > 32 || budget <= 0) break;
                    if (entry != null) CollectKnownWeaponModeReferences(candidates, entry, depth + 1, visited, ref budget);
                }
                return;
            }

            if (value is UnityEngine.Object) return;
            if (visited != null)
            {
                if (visited.Contains(value)) return;
                visited.Add(value);
            }

            List<MemberInfo> members = GetReadableMembers(type);
            for (int i = 0; i < members.Count && budget > 0; i++)
            {
                MemberInfo member = members[i];
                if (member == null) continue;
                object nested = GetMemberValue(value, member);
                if (nested == null || object.ReferenceEquals(nested, value)) continue;

                // Always inspect direct strings/collections because acceptance still
                // requires an exact key from the 89-record runtime mode table. For
                // arbitrary nested objects, follow only weapon/action/record semantics.
                if (nested is string || nested is IEnumerable || nested is IDictionary)
                {
                    CollectKnownWeaponModeReferences(candidates, nested, depth + 1, visited, ref budget);
                    continue;
                }

                string name = member.Name ?? string.Empty;
                Type memberType = GetMemberDeclaredType(member);
                string typeName = memberType == null ? string.Empty : (memberType.Name ?? string.Empty);
                bool follow =
                    name.IndexOf("Mode", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Attack", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Action", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Fire", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Shot", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Weapon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Descriptor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Record", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    string.Equals(name, "Value", StringComparison.OrdinalIgnoreCase) ||
                    typeName.IndexOf("Mode", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    typeName.IndexOf("Attack", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    typeName.IndexOf("Weapon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    typeName.IndexOf("Descriptor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    typeName.IndexOf("Record", StringComparison.OrdinalIgnoreCase) >= 0;

                if (follow)
                    CollectKnownWeaponModeReferences(candidates, nested, depth + 1, visited, ref budget);
            }
        }

        private static void AddWeaponModeCandidatesFromObject(List<object> candidates, object value, int depth)
        {
            if (candidates == null || value == null || depth > 3) return;

            string directId = value as string;
            if (!string.IsNullOrEmpty(directId))
            {
                object record;
                if (WeaponModeRecordsById.TryGetValue(directId, out record) && record != null)
                    AddModeCandidateUnique(candidates, record);
                return;
            }

            if (LooksLikeWeaponModeNode(value)) AddModeCandidateUnique(candidates, value);

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null && !(value is IDictionary))
            {
                foreach (object entry in enumerable)
                    AddWeaponModeCandidatesFromObject(candidates, entry, depth + 1);
                return;
            }

            List<MemberInfo> members = GetReadableMembers(value.GetType());
            for (int i = 0; i < members.Count; i++)
            {
                string name = members[i].Name ?? string.Empty;
                bool modeMember =
                    name.IndexOf("FireMode", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("AttackMode", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("AltFire", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("SecondaryFire", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("NoAmmo", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Empty", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("MeleeMode", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("MeleeAttack", StringComparison.OrdinalIgnoreCase) >= 0;
                if (!modeMember) continue;

                object nested = GetMemberValue(value, members[i]);
                if (nested == null || object.ReferenceEquals(nested, value)) continue;
                AddWeaponModeCandidatesFromObject(candidates, nested, depth + 1);
            }
        }

        private static void BuildWeaponModeRecordIndex()
        {
            WeaponModeRecordsById.Clear();
            try
            {
                List<MemberInfo> members = GetStaticDataMembers();
                for (int i = 0; i < members.Count; i++)
                {
                    string memberName = members[i].Name ?? string.Empty;
                    if (memberName.IndexOf("FireMode", StringComparison.OrdinalIgnoreCase) < 0 &&
                        memberName.IndexOf("AttackMode", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    object collection = GetMemberValue(null, members[i]);
                    if (collection == null || collection is string) continue;
                    List<DataEntry> records = EnumerateData(collection);
                    for (int r = 0; r < records.Count; r++)
                    {
                        object record = records[r].Value;
                        if (record == null) continue;
                        string id = FirstNonEmpty(
                            GetStringMember(record, "Id"),
                            GetStringMember(record, "ModeId"),
                            GetStringMember(record, "FireModeId"),
                            records[r].Key);
                        if (string.IsNullOrEmpty(id)) continue;
                        if (!WeaponModeRecordsById.ContainsKey(id)) WeaponModeRecordsById.Add(id, record);
                    }
                }
            }
            catch { }

            BuildWeaponModeStatsIndex();

            Debug.Log("[ItemIntelligence] Weapon modes: runtime records=" +
                WeaponModeRecordsById.Count.ToString(CultureInfo.InvariantCulture) +
                ", staticStats=" + WeaponModeStatsByRawId.Count.ToString(CultureInfo.InvariantCulture) + ".");
        }

        private static bool LooksLikeWeaponModeNode(object node)
        {
            if (node == null) return false;
            Type type = node.GetType();
            string typeName = type.Name ?? string.Empty;
            if (typeName.IndexOf("Projectile", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (typeName.IndexOf("Explosion", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (typeName.IndexOf("Damage", StringComparison.OrdinalIgnoreCase) >= 0) return false;

            if (typeName.IndexOf("FireMode", StringComparison.OrdinalIgnoreCase) >= 0 ||
                typeName.IndexOf("AttackMode", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            if (HasMemberNamed(type, "FireModeId") || HasMemberNamed(type, "ModeId") || HasMemberNamed(type, "BurstCount") ||
                HasMemberNamed(type, "ShotCount") || HasMemberNamed(type, "ShotsCount") || HasMemberNamed(type, "IsAutomatic"))
                return true;

            return false;
        }

        private static void AddModeCandidateUnique(List<object> candidates, object candidate)
        {
            if (candidates == null || candidate == null) return;
            for (int i = 0; i < candidates.Count; i++)
                if (object.ReferenceEquals(candidates[i], candidate)) return;
            candidates.Add(candidate);
        }

        private static WeaponModeDescriptor BuildWeaponModeDescriptor(string itemId, object node, int ordinal)
        {
            if (node == null) return null;
            string rawId = FirstNonEmpty(
                GetStringMember(node, "Id"),
                GetStringMember(node, "ModeId"),
                GetStringMember(node, "FireModeId"),
                GetStringMember(node, "AttackModeId"),
                GetStringMember(node, "ActionId"),
                GetStringMember(node, "Name"),
                GetStringMember(node, "Title"),
                node.GetType().Name);
            string label = BuildWeaponModeLabel(rawId, node);
            if (string.IsNullOrEmpty(label)) return null;
            string context = BuildWeaponModeContext(rawId, node);
            Sprite icon = null;
            if (!string.IsNullOrEmpty(rawId))
            {
                if (!WeaponModeIconsByRawId.TryGetValue(rawId, out icon) || icon == null)
                {
                    icon = ResolveWeaponModeIconCheap(rawId, node);
                    if (icon != null) WeaponModeIconsByRawId[rawId] = icon;
                }
            }
            else
            {
                icon = ResolveWeaponModeIconCheap(rawId, node);
            }
            string key = itemId + "::mode::" + ordinal.ToString(CultureInfo.InvariantCulture) + "::" + (string.IsNullOrEmpty(rawId) ? label : rawId);
            if (!string.IsNullOrEmpty(rawId)) WeaponModeRawIdByKey[key] = rawId;
            WeaponModeItemIdByKey[key] = itemId ?? string.Empty;
            // Vanilla HudFiremodePanel resolves the id through Data.Firemodes and passes
            // that canonical FireModeRecord into TooltipFactory.BuildFiremodeTooltip.
            // Therefore known raw ids must prefer the canonical record projection; a
            // weapon-local node is only a compatibility fallback when no FireMode id exists.
            WeaponModeStaticStats stats = null;
            if (!string.IsNullOrEmpty(rawId))
                WeaponModeStatsByRawId.TryGetValue(rawId, out stats);
            if (stats == null) stats = ProjectWeaponModeStats(node);
            if (stats != null) WeaponModeStatsByKey[key] = stats;
            return new WeaponModeDescriptor(key, rawId, label, context, stats);
        }

        private static string BuildWeaponModeLabel(string rawId, object node)
        {
            string explicitLabel = FirstNonEmpty(
                GetStringMember(node, "DisplayName"),
                GetStringMember(node, "Title"),
                GetStringMember(node, "Label"),
                GetStringMember(node, "Name"));

            string localized = string.Empty;
            if (!string.IsNullOrEmpty(rawId))
            {
                localized = LocalizeCandidates(new string[]
                {
                    "firemode." + rawId + ".name",
                    "fire_mode." + rawId + ".name",
                    "weapon.firemode." + rawId + ".name",
                    "attackmode." + rawId + ".name",
                    rawId
                }, rawId);
                if (string.Equals(localized, rawId, StringComparison.OrdinalIgnoreCase)) localized = string.Empty;
            }

            if (!string.IsNullOrEmpty(explicitLabel) &&
                !string.Equals(explicitLabel, rawId, StringComparison.OrdinalIgnoreCase))
                return NormalizeGameText(explicitLabel);
            if (!string.IsNullOrEmpty(localized)) return NormalizeGameText(localized);

            string normalized = (rawId ?? string.Empty).Trim();
            string lower = normalized.ToLowerInvariant();
            int suffixCount = GetWeaponModeCountHint(node);

            // For ranged fire modes the numeric suffix is the most useful stable fallback
            // when vanilla does not expose a localized title. Melee families keep their
            // semantic verb so we do not mislabel stab/chop/slash variants as bursts.
            bool meleeFamily = lower.Contains("melee") || lower.Contains("claw") || lower.Contains("slash") ||
                               lower.Contains("stab") || lower.Contains("chop") || lower.Contains("smash") ||
                               lower.Contains("punch") || lower.Contains("strike") || lower.Contains("breaker");

            if (lower.Contains("stock") || lower.Contains("butt") || lower.Contains("bash"))
                return Ui("ui.mode_buttstroke");

            if (meleeFamily)
            {
                string family = normalized;
                int underscore = family.IndexOf('_');
                if (underscore > 0) family = family.Substring(0, underscore);
                string familyLower = family.ToLowerInvariant();
                if (familyLower == "stab") return Ui("ui.mode_stab");
                if (familyLower == "slash") return Ui("ui.mode_slash");
                if (familyLower == "chop") return Ui("ui.mode_chop");
                if (familyLower == "smash") return Ui("ui.mode_smash");
                if (familyLower == "punch") return Ui("ui.mode_punch");
                return Ui("ui.mode_melee");
            }

            if (suffixCount > 0)
                return suffixCount == 1
                    ? Ui("ui.mode_single_shot")
                    : Ui("ui.mode_shots") + " " + suffixCount.ToString(CultureInfo.InvariantCulture);

            if (lower.Contains("auto")) return Ui("ui.mode_auto");
            if (lower.Contains("burst") || lower.Contains("volley")) return Ui("ui.mode_burst");
            if (lower.Contains("launcher")) return Ui("ui.mode_launcher");
            if (lower.Contains("throw")) return Ui("ui.mode_throw");
            if (string.IsNullOrEmpty(normalized)) return string.Empty;
            return HumanizeModeIdentifier(normalized);
        }

        private static string BuildWeaponModeContext(string rawId, object node)
        {
            string lower = ((rawId ?? string.Empty) + " " + node.GetType().Name).ToLowerInvariant();
            // Conservative: do not infer "No ammo" merely because a mode is melee.
            // Only explicit empty/no-ammo/butt-stock semantics get that annotation.
            if (lower.Contains("noammo") || lower.Contains("no_ammo") || lower.Contains("empty") ||
                lower.Contains("stock") || lower.Contains("butt") || lower.Contains("bash"))
                return Ui("ui.mode_when_empty");
            return string.Empty;
        }

        private static int GetWeaponModeCountHint(object node)
        {
            if (node == null) return 0;
            int value = GetIntMember(node, "BurstCount", -1);
            if (value <= 0) value = GetIntMember(node, "ShotCount", -1);
            if (value <= 0) value = GetIntMember(node, "ShotsCount", -1);
            if (value <= 0) value = GetIntMember(node, "ProjectilesCount", -1);
            if (value > 0 && value < 100) return value;

            string rawId = FirstNonEmpty(GetStringMember(node, "Id"), GetStringMember(node, "ModeId"), GetStringMember(node, "FireModeId"), string.Empty);
            if (!string.IsNullOrEmpty(rawId))
            {
                string[] parts = rawId.Split(new char[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = parts.Length - 1; i >= 0; i--)
                {
                    int parsed;
                    if (int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) && parsed >= 1 && parsed < 100)
                        return parsed;
                }
            }
            return 0;
        }

        private static Sprite ResolveWeaponModeIconCheap(string rawId, object node)
        {
            if (node == null) return null;

            // FireModeRecord itself only contains combat numbers on 1.0.1. The actual
            // presentation object is exposed through FireModeView / ContentDescriptor.
            // Resolve that exact descriptor first; this is both cheaper and much stronger
            // evidence than searching arbitrary loaded UI Images.
            Sprite descriptorSprite = ResolveWeaponModeDescriptorIcon(rawId, node);
            if (descriptorSprite != null) return descriptorSprite;

            string[] exactMembers = new string[]
            {
                "SmallIcon", "Icon", "ModeIcon", "FireModeIcon", "UiIcon", "Sprite", "SpriteRef"
            };
            for (int i = 0; i < exactMembers.Length; i++)
            {
                object raw = GetMember(node, exactMembers[i]);
                Sprite sprite = ResolveIconToken(raw, 0);
                if (sprite != null) return sprite;
            }

            List<MemberInfo> members = GetReadableMembers(node.GetType());
            for (int i = 0; i < members.Count; i++)
            {
                string name = members[i].Name ?? string.Empty;
                if (name.IndexOf("icon", StringComparison.OrdinalIgnoreCase) < 0 &&
                    name.IndexOf("sprite", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                object raw = GetMemberValue(node, members[i]);
                Sprite sprite = ResolveIconToken(raw, 0);
                if (sprite != null) return sprite;
            }
            return null;
        }

        private static Sprite ResolveWeaponModeDescriptorIcon(string rawId, object node)
        {
            if (node == null) return null;

            string[] descriptorMembers = new string[]
            {
                "FireModeView", "ContentDescriptor", "Descriptor", "View"
            };

            HashSet<object> seen = new HashSet<object>(ReferenceComparer.Instance);
            for (int i = 0; i < descriptorMembers.Length; i++)
            {
                object descriptor = GetMember(node, descriptorMembers[i]);
                if (descriptor == null || object.ReferenceEquals(descriptor, node)) continue;
                Sprite sprite = ResolveWeaponModeDescriptorIconValue(descriptor, rawId, 0, seen);
                if (sprite != null)
                {
                    return sprite;
                }
            }
            return null;
        }

        private static Sprite ResolveWeaponModeDescriptorIconValue(object value, string rawId, int depth, HashSet<object> seen)
        {
            if (value == null || depth > 3) return null;

            Sprite direct = value as Sprite;
            if (direct != null) return direct;

            Image image = value as Image;
            if (image != null) return image.sprite;

            string tag = value as string;
            if (tag != null) return ResolveIconToken(tag, 0);

            Type type = value.GetType();
            if (IsSimple(type)) return null;
            if (seen != null && !type.IsValueType)
            {
                if (seen.Contains(value)) return null;
                seen.Add(value);
            }

            List<MemberInfo> members = GetReadableMembers(type);
            List<KeyValuePair<int, Sprite>> candidates = new List<KeyValuePair<int, Sprite>>();

            // First pass: only members that explicitly describe visual/icon content.
            for (int i = 0; i < members.Count; i++)
            {
                MemberInfo member = members[i];
                string name = member.Name ?? string.Empty;
                string lower = name.ToLowerInvariant();
                Type declared = GetMemberDeclaredType(member);
                bool spriteTyped = declared != null && typeof(Sprite).IsAssignableFrom(declared);
                bool imageTyped = declared != null && typeof(Image).IsAssignableFrom(declared);
                bool visualName = lower.IndexOf("icon", StringComparison.Ordinal) >= 0 ||
                                  lower.IndexOf("sprite", StringComparison.Ordinal) >= 0 ||
                                  lower.IndexOf("image", StringComparison.Ordinal) >= 0 ||
                                  lower.IndexOf("picture", StringComparison.Ordinal) >= 0;
                if (!spriteTyped && !imageTyped && !visualName) continue;

                object nested = null;
                try { nested = GetMemberValue(value, member); } catch { }
                if (nested == null || object.ReferenceEquals(nested, value)) continue;
                Sprite sprite = ResolveIconToken(nested, 0);
                if (sprite == null) continue;

                int score = 0;
                if (lower.IndexOf("firemode", StringComparison.Ordinal) >= 0) score += 80;
                if (lower.IndexOf("mode", StringComparison.Ordinal) >= 0) score += 40;
                if (lower.IndexOf("icon", StringComparison.Ordinal) >= 0) score += 100;
                if (lower.IndexOf("sprite", StringComparison.Ordinal) >= 0) score += 90;
                if (lower.IndexOf("image", StringComparison.Ordinal) >= 0) score += 70;
                if (spriteTyped) score += 80;
                if (imageTyped) score += 60;
                if (lower.IndexOf("background", StringComparison.Ordinal) >= 0 ||
                    lower.IndexOf("frame", StringComparison.Ordinal) >= 0 ||
                    lower.IndexOf("selected", StringComparison.Ordinal) >= 0 ||
                    lower.IndexOf("hover", StringComparison.Ordinal) >= 0) score -= 120;
                candidates.Add(new KeyValuePair<int, Sprite>(score, sprite));
            }

            if (candidates.Count > 0)
            {
                candidates.Sort(delegate(KeyValuePair<int, Sprite> a, KeyValuePair<int, Sprite> b)
                {
                    return b.Key.CompareTo(a.Key);
                });
                if (candidates.Count == 1 || candidates[0].Key > candidates[1].Key)
                    return candidates[0].Value;
            }

            // Second pass: follow only descriptor/view/content wrappers, never the whole
            // arbitrary object graph. This keeps the resolver deterministic and cheap.
            string[] wrapperNames = new string[]
            {
                "View", "Descriptor", "ContentDescriptor", "Visual", "Visuals", "Content", "Data"
            };
            for (int i = 0; i < wrapperNames.Length; i++)
            {
                object nested = GetMember(value, wrapperNames[i]);
                if (nested == null || object.ReferenceEquals(nested, value)) continue;
                Sprite sprite = ResolveWeaponModeDescriptorIconValue(nested, rawId, depth + 1, seen);
                if (sprite != null) return sprite;
            }

            return null;
        }

        private static Sprite ResolveWeaponModeIconOnDemand(string rawId, object node)
        {
            if (string.IsNullOrEmpty(rawId) || node == null) return null;
            return ResolveWeaponModeIconCheap(rawId, node);
        }

        private static string HumanizeModeIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            StringBuilder sb = new StringBuilder(value.Length + 8);
            char prev = '\0';
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '_' || c == '-')
                {
                    if (sb.Length > 0 && sb[sb.Length - 1] != ' ') sb.Append(' ');
                    prev = c;
                    continue;
                }
                if (char.IsUpper(c) && sb.Length > 0 && prev != ' ' && !char.IsUpper(prev)) sb.Append(' ');
                sb.Append(c);
                prev = c;
            }
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(sb.ToString().Trim());
        }

        private static int GetIntMember(object target, string memberName, int fallback)
        {
            object raw = GetMember(target, memberName);
            if (raw == null) return fallback;
            try
            {
                if (raw is int) return (int)raw;
                if (raw is short) return (short)raw;
                if (raw is byte) return (byte)raw;
                if (raw is long)
                {
                    long l = (long)raw;
                    if (l >= int.MinValue && l <= int.MaxValue) return (int)l;
                }
                return Convert.ToInt32(raw, CultureInfo.InvariantCulture);
            }
            catch { return fallback; }
        }

        private static HashSet<string> ExtractAmmoKeysFromRecord(object record, bool weaponMode)
        {
            HashSet<string> keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (record == null) return keys;
            List<MemberInfo> members = GetReadableMembers(record.GetType());
            for (int i = 0; i < members.Count; i++)
            {
                MemberInfo member = members[i];
                string name = member.Name ?? string.Empty;
                bool ammoish = name.IndexOf("Ammo", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               name.IndexOf("Caliber", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               name.IndexOf("Calibre", StringComparison.OrdinalIgnoreCase) >= 0;
                if (!ammoish) continue;
                if (name.IndexOf("Count", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Amount", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Capacity", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Current", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Max", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                if (weaponMode && name.IndexOf("DefaultAmmo", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (weaponMode && name.IndexOf("OverrideAmmo", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                AddAmmoKeys(GetMemberValue(record, member), keys, 0);
            }
            return keys;
        }

        private static void AddAmmoKeys(object value, HashSet<string> keys, int depth)
        {
            if (value == null || keys == null || depth > 3) return;
            string direct = value as string;
            if (direct != null)
            {
                AddAmmoKey(keys, direct);
                return;
            }
            Type type = value.GetType();
            if (type.IsEnum || type.IsPrimitive || value is decimal)
            {
                AddAmmoKey(keys, ConvertToStableString(value));
                return;
            }
            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null && !(value is IDictionary))
            {
                foreach (object entry in enumerable) AddAmmoKeys(entry, keys, depth + 1);
                return;
            }
            string id = FirstNonEmpty(
                GetStringMember(value, "AmmoTypeId"),
                GetStringMember(value, "AmmoType"),
                GetStringMember(value, "Caliber"),
                GetStringMember(value, "Calibre"),
                GetStringMember(value, "Id"),
                GetStringMember(value, "Name"));
            AddAmmoKey(keys, id);
        }

        private static void AddAmmoKey(HashSet<string> keys, string key)
        {
            if (keys == null || string.IsNullOrEmpty(key)) return;
            string normalized = key.Trim();
            if (normalized.Length == 0) return;
            keys.Add(normalized);
            int dot = normalized.LastIndexOf('.');
            if (dot >= 0 && dot + 1 < normalized.Length) keys.Add(normalized.Substring(dot + 1));
            int plus = normalized.LastIndexOf('+');
            if (plus >= 0 && plus + 1 < normalized.Length) keys.Add(normalized.Substring(plus + 1));
        }

        private static bool AmmoKeysIntersect(HashSet<string> a, HashSet<string> b)
        {
            if (a == null || b == null || a.Count == 0 || b.Count == 0) return false;
            foreach (string key in a) if (b.Contains(key)) return true;
            return false;
        }

        private static void BuildBrowserAmmo(string itemId)
        {
            bool ru = IsRussian();

            if (!ShowAmmoRelations) return;

            if (!_compatAmmo)
            {
                AddCompatibilityUnavailableLine("Ammo");
                return;
            }
            string relationId = ResolveStaticRelationItemId(itemId);

            if (!_ammoWarmupComplete)
            {
                int percent;
                if (_ammoWarmupPhase == 0)
                {
                    int total = Math.Max(1, AmmoWarmupItems.Count);
                    percent = Math.Min(90, (_ammoWarmupIndex * 90) / total);
                }
                else
                {
                    int totalWeapons = Math.Max(1, AmmoWarmupWeapons.Count);
                    percent = 90 + Math.Min(10, (_ammoFinalizeWeaponIndex * 10) / totalWeapons);
                }
                BrowserLines.Add(BrowserLine.Note(Ui("ammo.index_prefix") + percent + "%"));
            }
            WeaponInfo weapon;
            List<string> weapons;

            if (WeaponsByItem.TryGetValue(relationId, out weapon) && weapon != null && weapon.CompatibleAmmo.Count > 0)
            {
                if (UsesInheritedStaticRelations(itemId))
                    AddModifiedRelationBrowserNote(itemId);

                BrowserLines.Add(BrowserLine.Section(Ui("ui.ammunition")));
                for (int i = 0; i < weapon.CompatibleAmmo.Count; i++)
                    BrowserLines.Add(ItemWithProductionChip(weapon.CompatibleAmmo[i], string.Empty));
                return;
            }

            if (CompatibleWeaponsByAmmo.TryGetValue(relationId, out weapons) && weapons != null && weapons.Count > 0)
            {
                if (UsesInheritedStaticRelations(itemId))
                    AddModifiedRelationBrowserNote(itemId);

                BrowserLines.Add(BrowserLine.Section(Ui("ui.compatible_weapons")));
                for (int i = 0; i < weapons.Count; i++)
                    BrowserLines.Add(ItemWithProductionChip(weapons[i], string.Empty));
                return;
            }

            BrowserLines.Add(BrowserLine.Note(Ui("ui.no_weapon_ammo_relationships")));
        }


    }
}
