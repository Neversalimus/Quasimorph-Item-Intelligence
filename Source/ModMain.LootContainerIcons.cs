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
        // Test12 container-visual owner: lazy snapshots, semantic resolution, scoring and safe icon misses.
        // Owner state: resolved icons, safe misses, immutable renderer catalog and record cache.
        // Container visuals stay lazy: snapshot only Sprite + semantic names, never
        // restore the retired global Sprite scan or retain renderer/Transform objects.
        private static readonly Dictionary<string, Sprite> LootContainerIconsById =
            new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> LootContainerIconSourcesById =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> LootContainerIconMisses =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static LootContainerRendererSnapshot[] _lootContainerRendererCatalog =
            new LootContainerRendererSnapshot[0];
        private static readonly Dictionary<string, List<LootContainerRendererSnapshot>> LootContainerRenderersByStem =
            new Dictionary<string, List<LootContainerRendererSnapshot>>(StringComparer.OrdinalIgnoreCase);
        private static bool _lootContainerRendererCatalogReady;
        private static readonly Dictionary<string, object> LootContainerRecordsById =
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private static bool _lootContainerRecordCacheReady;
        private static void EnsureLootContainerIconsResolved()
        {
            // v1.7.35-test6: keep the safe lazy architecture from test3/5, but
            // snapshot semantic renderer metadata once. This avoids repeated Unity
            // Component/GameObject/Transform access while paging through Loot.
            if (_lootContainerRendererCatalogReady) return;
            _lootContainerRendererCatalogReady = true;
            int started = Environment.TickCount;
            SpriteRenderer[] renderers;
            try { renderers = Resources.FindObjectsOfTypeAll<SpriteRenderer>(); }
            catch { renderers = new SpriteRenderer[0]; }
            List<LootContainerRendererSnapshot> usable =
                new List<LootContainerRendererSnapshot>(renderers.Length);
            LootContainerRenderersByStem.Clear();
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null) continue;
                Sprite sprite = null;
                try { sprite = renderer.sprite; }
                catch { sprite = null; }
                if (sprite == null) continue;
                string objectName = string.Empty;
                try
                {
                    objectName = renderer.gameObject != null
                        ? (renderer.gameObject.name ?? string.Empty)
                        : string.Empty;
                }
                catch { objectName = string.Empty; }
                string hierarchy = BuildLootContainerRendererHierarchy(renderer);
                LootContainerRendererSnapshot snapshot = new LootContainerRendererSnapshot
                {
                    Sprite = sprite,
                    ObjectName = objectName,
                    SpriteName = sprite.name ?? string.Empty,
                    Hierarchy = hierarchy,
                    Source = "resources.SpriteRenderer[" + objectName + "]/" +
                        (sprite.name ?? string.Empty) +
                        (string.IsNullOrEmpty(hierarchy) ? string.Empty : "{path=" + hierarchy + "}")
                };
                usable.Add(snapshot);
                IndexLootContainerRendererSnapshot(snapshot);
            }
            // We no longer need the Component array after the immutable snapshot is
            // built. Only vanilla Sprite references + strings are retained.
            _lootContainerRendererCatalog = usable.ToArray();
            int elapsed = unchecked(Environment.TickCount - started);
            if (elapsed < 0) elapsed = 0;
            Debug.Log("[ItemIntelligence][ContainerIconPerf] renderer snapshot: entries=" +
                _lootContainerRendererCatalog.Length.ToString(CultureInfo.InvariantCulture) +
                ", build=" + elapsed.ToString(CultureInfo.InvariantCulture) +
                " ms; stemKeys=" + LootContainerRenderersByStem.Count.ToString(CultureInfo.InvariantCulture) +
                "; retainedComponents=0; globalSpriteScan=DISABLED.");
        }
        private static void EnsureLootContainerRecordCache()
        {
            if (_lootContainerRecordCacheReady) return;
            _lootContainerRecordCacheReady = true;
            LootContainerRecordsById.Clear();
            object containers = null;
            try { containers = GetStaticMember(typeof(Data), "ObstacleContainers"); }
            catch { containers = null; }
            List<DataEntry> entries = EnumerateData(containers);
            for (int i = 0; i < entries.Count; i++)
            {
                object record = entries[i].Value;
                if (record == null) continue;
                string id = FirstNonEmpty(GetStringMember(record, "Id"), entries[i].Key);
                if (string.IsNullOrEmpty(id)) continue;
                if (!LootContainerRecordsById.ContainsKey(id))
                    LootContainerRecordsById[id] = record;
            }
        }
        private sealed class LootContainerRendererSnapshot
        {
            public Sprite Sprite;
            public string ObjectName;
            public string SpriteName;
            public string Hierarchy;
            public string Source;
        }
        private sealed class LootContainerIconCandidate
        {
            public Sprite Sprite;
            public string Source;
            public int Score;
        }
        private static bool TryResolveLootContainerVisual(
            object record,
            string containerId,
            Sprite[] loadedSprites,
            LootContainerRendererSnapshot[] loadedRenderers,
            out Sprite icon,
            out string source,
            out string audit)
        {
            icon = null;
            source = string.Empty;
            audit = "selected=<none>; candidates=[]";
            if (record == null) return false;
            List<string> aliases = CollectLootContainerVisualAliases(record, containerId);
            List<LootContainerIconCandidate> candidates = new List<LootContainerIconCandidate>();
            HashSet<object> visited = new HashSet<object>();
            CollectLootContainerIconCandidates(record, "record", 0, visited, candidates);
            // ObstacleContainerRecord currently exposes no Sprite/GameObject references on
            // game build 1.0.1.566s.7e4da55. Fall back to already-loaded vanilla resources,
            // but only by strong semantic name matches. This never instantiates a prefab.
            CollectLoadedContainerSpriteCandidates(aliases, loadedSprites, loadedRenderers, candidates);
            candidates.Sort(delegate(LootContainerIconCandidate a, LootContainerIconCandidate b)
            {
                int score = b.Score.CompareTo(a.Score);
                if (score != 0) return score;
                return string.Compare(a.Source, b.Source, StringComparison.OrdinalIgnoreCase);
            });
            StringBuilder details = null;
            if (ModderMode)
            {
                details = new StringBuilder();
                details.Append("aliases=[");
                int aliasCount = Math.Min(6, aliases.Count);
                for (int i = 0; i < aliasCount; i++)
                {
                    if (i > 0) details.Append(" | ");
                    details.Append(aliases[i]);
                }
                if (aliases.Count > aliasCount)
                    details.Append(" | ...+").Append((aliases.Count - aliasCount).ToString(CultureInfo.InvariantCulture));
                details.Append("]; candidates=[");
                int describeCount = Math.Min(8, candidates.Count);
                for (int i = 0; i < describeCount; i++)
                {
                    if (i > 0) details.Append(" | ");
                    LootContainerIconCandidate c = candidates[i];
                    details.Append(c.Source).Append("=").Append(DescribeSprite(c.Sprite))
                        .Append("/score=").Append(c.Score.ToString(CultureInfo.InvariantCulture));
                }
                if (candidates.Count > describeCount)
                    details.Append(" | ...+").Append((candidates.Count - describeCount).ToString(CultureInfo.InvariantCulture));
                details.Append("]");
            }
            if (candidates.Count == 0)
            {
                if (ModderMode) audit = "selected=<none>; " + details.ToString();
                return false;
            }
            LootContainerIconCandidate best = SelectLootContainerRepresentative(candidates, aliases, containerId);
            // 185+ means a renderer matched at least one distinctive container token
            // plus its structural type (for example "industry"+"container"), or a
            // stronger exact/substring match. Generic-only matches never pass.
            if (best == null || best.Sprite == null || best.Score < 185)
            {
                int bestScore = candidates.Count > 0 ? candidates[0].Score : 0;
                if (ModderMode)
                    audit = "selected=<none>; bestScore=" + bestScore.ToString(CultureInfo.InvariantCulture) +
                        "; " + details.ToString();
                return false;
            }
            icon = best.Sprite;
            source = best.Source ?? string.Empty;
            if (ModderMode)
                audit = "selected=" + DescribeSprite(icon) +
                    "; source=" + source +
                    "; score=" + best.Score.ToString(CultureInfo.InvariantCulture) +
                    "; representative=deterministic;" +
                    " " + details.ToString();
            return true;
        }
        private static LootContainerIconCandidate SelectLootContainerRepresentative(
            List<LootContainerIconCandidate> candidates,
            List<string> aliases,
            string containerId)
        {
            if (candidates == null || candidates.Count == 0) return null;
            LootContainerIconCandidate selected = null;
            long selectedRank = long.MinValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                LootContainerIconCandidate candidate = candidates[i];
                if (candidate == null || candidate.Sprite == null) continue;
                if (candidate.Score < 185) continue;
                string source = candidate.Source ?? string.Empty;
                string spriteName = candidate.Sprite.name ?? string.Empty;
                string combined = source + " " + spriteName;
                if (!IsLootContainerVisualCompatible(containerId, source, spriteName)) continue;
                // Explosion/effect/UI sprites are never suitable as a representative
                // world-container icon even if their names happen to match.
                if (combined.IndexOf("explosion", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    combined.IndexOf("effect", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    combined.IndexOf("gib", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    combined.IndexOf("shadow", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                long rank = (long)candidate.Score * 10000L;
                if (source.IndexOf("SpriteRenderer[Obj]", StringComparison.OrdinalIgnoreCase) >= 0)
                    rank += 1200L;
                else if (source.IndexOf("SpriteRenderer[", StringComparison.OrdinalIgnoreCase) >= 0)
                    rank += 800L;

                if (spriteName.EndsWith("_0", StringComparison.OrdinalIgnoreCase)) rank += 220L;
                else if (spriteName.EndsWith("_1", StringComparison.OrdinalIgnoreCase)) rank += 160L;

                if (combined.IndexOf("anim", StringComparison.OrdinalIgnoreCase) >= 0) rank -= 180L;
                if (combined.IndexOf("wide", StringComparison.OrdinalIgnoreCase) >= 0) rank -= 60L;
                if (combined.IndexOf("light", StringComparison.OrdinalIgnoreCase) >= 0) rank -= 220L;
                if (combined.IndexOf("icon", StringComparison.OrdinalIgnoreCase) >= 0) rank -= 100L;

                bool wantsBig = !string.IsNullOrEmpty(containerId) &&
                    (containerId.IndexOf("big", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     containerId.IndexOf("large", StringComparison.OrdinalIgnoreCase) >= 0);
                bool wantsSmall = !string.IsNullOrEmpty(containerId) &&
                    containerId.IndexOf("small", StringComparison.OrdinalIgnoreCase) >= 0;
                bool hasBig = combined.IndexOf("big", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    combined.IndexOf("large", StringComparison.OrdinalIgnoreCase) >= 0;
                bool hasSmall = combined.IndexOf("small", StringComparison.OrdinalIgnoreCase) >= 0;
                if (wantsBig && hasBig) rank += 160L;
                else if (!wantsBig && hasBig) rank -= 60L;
                if (wantsSmall && hasSmall) rank += 160L;
                else if (!wantsSmall && hasSmall) rank -= 40L;

                // Among several equally valid skins/states, prefer the shortest
                // semantic sprite name. This reliably picks a generic base visual such
                // as IndusContainer_0 over IndusContainerWide/Anim variants.
                int semanticLength = NormalizeContainerVisualName(spriteName).Length;
                rank += Math.Max(0, 96 - Math.Min(96, semanticLength));

                if (selected == null || rank > selectedRank ||
                    (rank == selectedRank &&
                     string.Compare(source, selected.Source, StringComparison.OrdinalIgnoreCase) < 0))
                {
                    selected = candidate;
                    selectedRank = rank;
                }
            }
            return selected;
        }

        private static List<string> CollectLootContainerVisualAliases(object record, string containerId)
        {
            List<string> aliases = new List<string>();
            AddLootContainerVisualAlias(aliases, containerId);
            AddLootContainerVisualAlias(aliases, RemoveContainerVisualNoise(containerId));

            string displayName = ResolveLootContainerName(containerId);
            AddLootContainerVisualAlias(aliases, displayName);

            // v1.7.35-test5: test2's read-only resource audit exposed several
            // canonical renderer stems that are not derivable from the gameplay IDs
            // (for example data_container -> blueBookcase). Keep only mappings with
            // an obvious semantic identity. Unknown/ambiguous containers still render
            // without an icon rather than accepting a wrong sprite.
            AddCanonicalLootContainerVisualAliases(aliases, containerId);

            Type type = record != null ? record.GetType() : null;
            if (type == null) return aliases;

            FieldInfo[] fields;
            try { fields = type.GetFields(InstanceFlags); }
            catch { fields = new FieldInfo[0]; }
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (field == null || field.FieldType != typeof(string)) continue;
                if (!LooksLikeContainerVisualAliasMember(field.Name)) continue;
                string value = string.Empty;
                try { value = field.GetValue(record) as string; }
                catch { value = string.Empty; }
                AddLootContainerVisualAlias(aliases, value);
                AddLootContainerVisualAlias(aliases, RemoveContainerVisualNoise(value));
            }

            PropertyInfo[] props;
            try { props = type.GetProperties(InstanceFlags); }
            catch { props = new PropertyInfo[0]; }
            for (int i = 0; i < props.Length; i++)
            {
                PropertyInfo prop = props[i];
                if (prop == null || !prop.CanRead || prop.PropertyType != typeof(string) ||
                    prop.GetIndexParameters().Length != 0)
                    continue;
                if (!LooksLikeContainerVisualAliasMember(prop.Name)) continue;
                string value = string.Empty;
                try { value = prop.GetValue(record, null) as string; }
                catch { value = string.Empty; }
                AddLootContainerVisualAlias(aliases, value);
                AddLootContainerVisualAlias(aliases, RemoveContainerVisualNoise(value));
            }

            return aliases;
        }

        private static string[] GetCanonicalLootContainerVisualStems(string containerId)
        {
            if (string.IsNullOrEmpty(containerId)) return null;
            string id = containerId.ToLowerInvariant();
            switch (id)
            {
                case "toxic_barrel": return new string[] { "ToxicBarrel" };
                case "blood_sink": return new string[] { "sinkBlood" };
                // test8 runtime audit proves the normal water sink renderer exactly:
                // sinkNormal_0 under Obj/sink_1. Keep it distinct from blood_sink.
                case "water_sink": return new string[] { "sinkNormal", "sink_1" };
                // Runtime audit proves the actual renderer is Toilet 1_0 under toilet_1.
                case "water_toilet": return new string[] { "Toilet 1", "toilet_1" };
                case "water_tank": return new string[] { "WaterTank" };
                case "data_container": return new string[] { "blueBookcase" };
                // Generic loot groups do not expose their visual id. These are only
                // representative vanilla world props from the matching visual family.
                case "common_box": return new string[] { "BrownBox" };
                case "common_container": return new string[] { "BrownCabinet" };
                case "wooden_box": return new string[] { "wood_box_1", "woddenBox_1" };
                case "common_rack": return new string[] { "MetallRack", "IndusMetallRack" };
                case "tool_case": return new string[] { "Indus_Tool_closed" };
                case "weapon_stand": return new string[] { "WeaponStand" };
                // User-verified world visual: the weapon cases are the green suitcase
                // family. The former broad CaseBig/CaseSmall aliases also matched
                // MedicalCaseBig/MedicalSmallCase and could display a medical box.
                case "weapon_case_big": return new string[] { "GreenCaseBig", "WeaponCaseBig" };
                case "weapon_case_small": return new string[] { "GreenCaseSmall", "WeaponCaseSmall" };
                // Do NOT map ammo_case to GreenCaseSmall/BrownCaseSmall. Runtime/user
                // verification shows those are suitcase/weapon-case visuals and represent
                // a different vanilla container family. ammo_case stays unresolved until
                // the semantic alias "ammocontainer" is linked to its actual world visual.
                case "ammo_case": return null;
                case "flowers_container": return new string[] { "FlowersContainer" };
                case "industry_container_value": return new string[] { "IndusContainer" };
                case "medical_case": return new string[] { "MedicalCase" };
                case "trash_can": return new string[] { "Metal_Trash_Can" };
                case "science_container_value": return new string[] { "scienceBigContainer" };
                case "server_container": return new string[] { "server_case", "ServerStation" };
                case "aztec_chest": return new string[] { "aztec_chest" };
                case "fastfood_container": return new string[] { "VendingMachine" };
                case "snowman": return new string[] { "snowmanContainer" };
                case "armor_locker": return new string[] { "ArmorLocker" };
                case "aed_case": return new string[] { "aed_case" };
                case "extinguisher_holder": return new string[] { "extinguisher_holder" };
                case "watermelon_growbox": return new string[] { "watermelon_grow" };
                case "cabbage_growbox": return new string[] { "cabbage_grow" };
                case "matrix_box": return new string[] { "matrix_container" };
            }
            return null;
        }

        private static bool IsKnownAmbiguousLootContainerVisual(string containerId)
        {
            // Test11 runtime evidence found only weak generic medical-family matches for
            // these two records (bestScore=155, below the safe 185 threshold). The scan
            // produced no icon but blocked the main thread for 32 ms + 31 ms. Keep the
            // already-correct no-icon result without repeating that heuristic work.
            return string.Equals(containerId, "medical_container", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(containerId, "medical_holder", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLootContainerVisualCompatible(string containerId, string source, string spriteName)
        {
            if (string.IsNullOrEmpty(containerId) ||
                !containerId.StartsWith("weapon_case_", StringComparison.OrdinalIgnoreCase)) return true;

            string combined = ((source ?? string.Empty) + " " + (spriteName ?? string.Empty)).ToLowerInvariant();
            if (combined.IndexOf("medical", StringComparison.Ordinal) >= 0 ||
                combined.IndexOf("medcase", StringComparison.Ordinal) >= 0 ||
                combined.IndexOf("heal", StringComparison.Ordinal) >= 0) return false;
            if (containerId.EndsWith("_big", StringComparison.OrdinalIgnoreCase) &&
                combined.IndexOf("small", StringComparison.Ordinal) >= 0) return false;
            if (containerId.EndsWith("_small", StringComparison.OrdinalIgnoreCase) &&
                (combined.IndexOf("big", StringComparison.Ordinal) >= 0 ||
                 combined.IndexOf("large", StringComparison.Ordinal) >= 0)) return false;

            return combined.IndexOf("greencase", StringComparison.Ordinal) >= 0 ||
                (combined.IndexOf("green", StringComparison.Ordinal) >= 0 &&
                 combined.IndexOf("case", StringComparison.Ordinal) >= 0) ||
                combined.IndexOf("weaponcase", StringComparison.Ordinal) >= 0;
        }

        private static void AddCanonicalLootContainerVisualAliases(List<string> aliases, string containerId)
        {
            if (aliases == null) return;
            string[] stems = GetCanonicalLootContainerVisualStems(containerId);
            if (stems == null) return;
            for (int i = 0; i < stems.Length; i++) AddLootContainerVisualAlias(aliases, stems[i]);
        }

        private static string NormalizeLootContainerRendererStem(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            string key = NormalizeContainerVisualName(value);
            int end = key.Length;
            while (end > 0 && char.IsDigit(key[end - 1])) end--;
            if (end != key.Length) key = key.Substring(0, end);
            return key;
        }

        private static void AddLootContainerRendererStem(string value, LootContainerRendererSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Sprite == null || string.IsNullOrEmpty(value)) return;
            string key = NormalizeLootContainerRendererStem(value);
            if (key.Length < 5) return;
            List<LootContainerRendererSnapshot> list;
            if (!LootContainerRenderersByStem.TryGetValue(key, out list))
            {
                list = new List<LootContainerRendererSnapshot>();
                LootContainerRenderersByStem[key] = list;
            }
            for (int i = 0; i < list.Count; i++)
                if (object.ReferenceEquals(list[i].Sprite, snapshot.Sprite)) return;
            list.Add(snapshot);
        }

        private static void IndexLootContainerRendererSnapshot(LootContainerRendererSnapshot snapshot)
        {
            if (snapshot == null) return;
            AddLootContainerRendererStem(snapshot.ObjectName, snapshot);
            AddLootContainerRendererStem(snapshot.SpriteName, snapshot);
            if (string.IsNullOrEmpty(snapshot.Hierarchy)) return;
            string[] parts = snapshot.Hierarchy.Split('/');
            for (int i = 0; i < parts.Length; i++) AddLootContainerRendererStem(parts[i], snapshot);
        }

        private static bool TryResolveIndexedCanonicalContainerIcon(
            string containerId, out Sprite icon, out string source, out string audit)
        {
            icon = null;
            source = string.Empty;
            audit = string.Empty;
            string[] stems = GetCanonicalLootContainerVisualStems(containerId);
            if (stems == null || stems.Length == 0) return false;

            List<LootContainerRendererSnapshot> candidates = new List<LootContainerRendererSnapshot>();
            for (int i = 0; i < stems.Length; i++)
            {
                string key = NormalizeLootContainerRendererStem(stems[i]);
                List<LootContainerRendererSnapshot> indexed;
                if (string.IsNullOrEmpty(key) || !LootContainerRenderersByStem.TryGetValue(key, out indexed)) continue;
                for (int j = 0; j < indexed.Count; j++)
                {
                    LootContainerRendererSnapshot entry = indexed[j];
                    if (entry == null || entry.Sprite == null) continue;
                    if (!IsLootContainerVisualCompatible(containerId, entry.Source, entry.SpriteName)) continue;
                    string combined = (entry.Source ?? string.Empty) + " " + (entry.SpriteName ?? string.Empty);
                    if (combined.IndexOf("explosion", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        combined.IndexOf("effect", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        combined.IndexOf("shadow", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        combined.IndexOf("gib", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    bool duplicate = false;
                    for (int k = 0; k < candidates.Count; k++)
                        if (object.ReferenceEquals(candidates[k].Sprite, entry.Sprite)) { duplicate = true; break; }
                    if (!duplicate) candidates.Add(entry);
                }
            }
            if (candidates.Count == 0) return false;

            candidates.Sort(delegate(LootContainerRendererSnapshot a, LootContainerRendererSnapshot b)
            {
                int ar = 0, br = 0;
                if ((a.Source ?? string.Empty).IndexOf("SpriteRenderer[Obj]", StringComparison.OrdinalIgnoreCase) >= 0) ar += 100;
                if ((b.Source ?? string.Empty).IndexOf("SpriteRenderer[Obj]", StringComparison.OrdinalIgnoreCase) >= 0) br += 100;
                if ((a.SpriteName ?? string.Empty).EndsWith("_0", StringComparison.OrdinalIgnoreCase)) ar += 30;
                else if ((a.SpriteName ?? string.Empty).EndsWith("_1", StringComparison.OrdinalIgnoreCase)) ar += 20;
                if ((b.SpriteName ?? string.Empty).EndsWith("_0", StringComparison.OrdinalIgnoreCase)) br += 30;
                else if ((b.SpriteName ?? string.Empty).EndsWith("_1", StringComparison.OrdinalIgnoreCase)) br += 20;
                int rank = br.CompareTo(ar);
                if (rank != 0) return rank;
                return string.Compare(a.Source, b.Source, StringComparison.OrdinalIgnoreCase);
            });

            LootContainerRendererSnapshot selected = candidates[0];
            icon = selected.Sprite;
            source = selected.Source ?? string.Empty;
            if (ModderMode)
                audit = "indexedCanonical=selected; stemCount=" + stems.Length.ToString(CultureInfo.InvariantCulture) +
                    "; candidateCount=" + candidates.Count.ToString(CultureInfo.InvariantCulture) +
                    "; selected=" + DescribeSprite(icon) + "; source=" + source;
            return true;
        }

        private static string BuildWaterSinkTargetAudit()
        {
            List<string> found = new List<string>();
            for (int i = 0; i < _lootContainerRendererCatalog.Length; i++)
            {
                LootContainerRendererSnapshot e = _lootContainerRendererCatalog[i];
                if (e == null || e.Sprite == null) continue;
                string combined = ((e.ObjectName ?? string.Empty) + " " + (e.SpriteName ?? string.Empty) + " " + (e.Hierarchy ?? string.Empty)).ToLowerInvariant();
                if (combined.IndexOf("sink", StringComparison.Ordinal) < 0 &&
                    combined.IndexOf("basin", StringComparison.Ordinal) < 0 &&
                    combined.IndexOf("wash", StringComparison.Ordinal) < 0) continue;
                // A blood sink is a distinct vanilla container and must never be silently
                // substituted for the ordinary water sink. WaterTank is also not a sink.
                if (combined.IndexOf("blood", StringComparison.Ordinal) >= 0 ||
                    combined.IndexOf("toxic", StringComparison.Ordinal) >= 0 ||
                    combined.IndexOf("watertank", StringComparison.Ordinal) >= 0 ||
                    combined.IndexOf("water_tank", StringComparison.Ordinal) >= 0 ||
                    combined.IndexOf("watermelon", StringComparison.Ordinal) >= 0) continue;
                found.Add(e.Source + "=" + DescribeSprite(e.Sprite));
                if (found.Count >= 12) break;
            }
            return "sinkFamily=[" + string.Join(" | ", found.ToArray()) + "]";
        }

        private static string BuildAmmoCaseTargetAudit()
        {
            List<string> strong = new List<string>();
            List<string> ammoOnly = new List<string>();
            for (int i = 0; i < _lootContainerRendererCatalog.Length; i++)
            {
                LootContainerRendererSnapshot e = _lootContainerRendererCatalog[i];
                if (e == null || e.Sprite == null) continue;
                string combined = ((e.ObjectName ?? string.Empty) + " " + (e.SpriteName ?? string.Empty) + " " + (e.Hierarchy ?? string.Empty)).ToLowerInvariant();
                if (combined.IndexOf("ammo", StringComparison.Ordinal) < 0 &&
                    combined.IndexOf("ammunition", StringComparison.Ordinal) < 0) continue;
                if (combined.IndexOf("workbench", StringComparison.Ordinal) >= 0 ||
                    combined.IndexOf("capacity", StringComparison.Ordinal) >= 0 ||
                    combined.IndexOf("shadow", StringComparison.Ordinal) >= 0 ||
                    combined.IndexOf("gib", StringComparison.Ordinal) >= 0 ||
                    combined.IndexOf("effect", StringComparison.Ordinal) >= 0 ||
                    combined.IndexOf("explosion", StringComparison.Ordinal) >= 0 ||
                    combined.IndexOf("_inv", StringComparison.Ordinal) >= 0 ||
                    combined.IndexOf("_floor", StringComparison.Ordinal) >= 0) continue;

                string described = e.Source + "=" + DescribeSprite(e.Sprite);
                bool containerLike =
                    combined.IndexOf("case", StringComparison.Ordinal) >= 0 ||
                    combined.IndexOf("box", StringComparison.Ordinal) >= 0 ||
                    combined.IndexOf("container", StringComparison.Ordinal) >= 0 ||
                    combined.IndexOf("crate", StringComparison.Ordinal) >= 0 ||
                    combined.IndexOf("chest", StringComparison.Ordinal) >= 0 ||
                    combined.IndexOf("locker", StringComparison.Ordinal) >= 0;
                if (containerLike) strong.Add(described);
                else ammoOnly.Add(described);
                if (strong.Count >= 12 && ammoOnly.Count >= 8) break;
            }
            if (strong.Count > 12) strong.RemoveRange(12, strong.Count - 12);
            if (ammoOnly.Count > 8) ammoOnly.RemoveRange(8, ammoOnly.Count - 8);
            return "ammoCaseFamily=[" + string.Join(" | ", strong.ToArray()) +
                "]; ammoWorldFamily=[" + string.Join(" | ", ammoOnly.ToArray()) + "]";
        }

        private static string BuildWeaponCaseTargetAudit(string containerId)
        {
            List<string> found = new List<string>();
            bool wantsBig = containerId.EndsWith("_big", StringComparison.OrdinalIgnoreCase);
            for (int i = 0; i < _lootContainerRendererCatalog.Length; i++)
            {
                LootContainerRendererSnapshot entry = _lootContainerRendererCatalog[i];
                if (entry == null || entry.Sprite == null) continue;
                string combined = ((entry.Source ?? string.Empty) + " " +
                    (entry.SpriteName ?? string.Empty)).ToLowerInvariant();
                if (combined.IndexOf("case", StringComparison.Ordinal) < 0 ||
                    combined.IndexOf(wantsBig ? "big" : "small", StringComparison.Ordinal) < 0 ||
                    combined.IndexOf("medical", StringComparison.Ordinal) >= 0) continue;
                found.Add(entry.Source + "=" + DescribeSprite(entry.Sprite));
                if (found.Count >= 16) break;
            }
            return "weaponCaseFamily=[" + string.Join(" | ", found.ToArray()) + "]";
        }

        private static bool LooksLikeContainerVisualAliasMember(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return name.IndexOf("id", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("name", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("visual", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("prefab", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("obstacle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("object", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("sprite", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("asset", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("view", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("type", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AddLootContainerVisualAlias(List<string> aliases, string value)
        {
            if (aliases == null || string.IsNullOrEmpty(value)) return;
            string trimmed = value.Trim();
            if (trimmed.Length < 3 || trimmed.Length > 128) return;
            for (int i = 0; i < aliases.Count; i++)
            {
                if (string.Equals(aliases[i], trimmed, StringComparison.OrdinalIgnoreCase))
                    return;
            }
            aliases.Add(trimmed);
        }

        private static string RemoveContainerVisualNoise(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            string result = value;
            string[] suffixes = new string[] { "_value", " value", "_record", " record" };
            for (int i = 0; i < suffixes.Length; i++)
            {
                if (result.EndsWith(suffixes[i], StringComparison.OrdinalIgnoreCase))
                {
                    result = result.Substring(0, result.Length - suffixes[i].Length);
                    break;
                }
            }
            return result;
        }

        private static void CollectLoadedContainerSpriteCandidates(
            List<string> aliases,
            Sprite[] loadedSprites,
            LootContainerRendererSnapshot[] loadedRenderers,
            List<LootContainerIconCandidate> output)
        {
            if (aliases == null || output == null) return;

            if (loadedSprites != null)
            {
                for (int i = 0; i < loadedSprites.Length; i++)
                {
                    Sprite sprite = loadedSprites[i];
                    if (sprite == null) continue;
                    int score = ScoreLoadedContainerVisualName(sprite.name, aliases);
                    if (score < 150) continue;
                    score += ScoreContainerVisualStateHint(sprite.name);
                    AddLootContainerIconCandidate(output, sprite,
                        "resources.Sprite[" + (sprite.name ?? string.Empty) + "]", score);
                }
            }

            if (loadedRenderers != null)
            {
                for (int i = 0; i < loadedRenderers.Length; i++)
                {
                    LootContainerRendererSnapshot renderer = loadedRenderers[i];
                    if (renderer == null || renderer.Sprite == null) continue;

                    int objectScore = ScoreLoadedContainerVisualName(renderer.ObjectName, aliases);
                    int spriteScore = ScoreLoadedContainerVisualName(renderer.SpriteName, aliases);
                    int hierarchyScore = ScoreLoadedContainerVisualName(renderer.Hierarchy, aliases);
                    int score = Math.Max(Math.Max(objectScore, spriteScore), hierarchyScore);
                    if (hierarchyScore >= 220) score += 25;
                    else if (hierarchyScore >= 180) score += 12;
                    if (objectScore >= 180 && spriteScore >= 150) score += 20;
                    if (score < 150) continue;
                    score += ScoreContainerVisualStateHint(renderer.ObjectName);
                    score += ScoreContainerVisualStateHint(renderer.SpriteName);
                    AddLootContainerIconCandidate(output, renderer.Sprite, renderer.Source, score);
                }
            }
        }

        private static bool TryResolveGenericContainerFamilyFallback(
            string containerId,
            LootContainerRendererSnapshot[] renderers,
            out Sprite icon,
            out string source,
            out string audit)
        {
            icon = null;
            source = string.Empty;
            audit = string.Empty;
            if (string.IsNullOrEmpty(containerId) || renderers == null) return false;

            string id = containerId.ToLowerInvariant();
            bool targetWood = id == "wooden_box";
            bool targetCommonBox = id == "common_box";
            bool targetCommonContainer = id == "common_container";
            if (!targetWood && !targetCommonBox && !targetCommonContainer) return false;

            LootContainerIconCandidate best = null;
            int bestScore = int.MinValue;
            int secondScore = int.MinValue;
            List<LootContainerIconCandidate> auditCandidates = ModderMode ? new List<LootContainerIconCandidate>() : null;

            for (int i = 0; i < renderers.Length; i++)
            {
                LootContainerRendererSnapshot entry = renderers[i];
                if (entry == null || entry.Sprite == null) continue;
                string combined = ((entry.ObjectName ?? string.Empty) + " " +
                    (entry.SpriteName ?? string.Empty) + " " +
                    (entry.Hierarchy ?? string.Empty)).ToLowerInvariant();

                if (combined.IndexOf("explosion", StringComparison.Ordinal) >= 0 ||
                    combined.IndexOf("effect", StringComparison.Ordinal) >= 0 ||
                    combined.IndexOf("shadow", StringComparison.Ordinal) >= 0 ||
                    combined.IndexOf("gib", StringComparison.Ordinal) >= 0)
                    continue;

                bool hasBox = combined.IndexOf("box", StringComparison.Ordinal) >= 0 ||
                    combined.IndexOf("crate", StringComparison.Ordinal) >= 0 ||
                    combined.IndexOf("chest", StringComparison.Ordinal) >= 0;
                bool hasContainer = combined.IndexOf("container", StringComparison.Ordinal) >= 0 ||
                    combined.IndexOf("cabinet", StringComparison.Ordinal) >= 0 ||
                    combined.IndexOf("case", StringComparison.Ordinal) >= 0;
                bool hasWood = combined.IndexOf("wood", StringComparison.Ordinal) >= 0;
                bool hasCommon = combined.IndexOf("common", StringComparison.Ordinal) >= 0;

                int score = 0;
                if (targetWood)
                {
                    if (!hasWood || !hasBox) continue;
                    score = 300;
                }
                else if (targetCommonBox)
                {
                    if (!hasCommon || !hasBox) continue;
                    score = 300;
                }
                else
                {
                    if (!hasCommon || !hasContainer) continue;
                    score = 300;
                }

                string spriteName = entry.SpriteName ?? string.Empty;
                if (spriteName.EndsWith("_0", StringComparison.OrdinalIgnoreCase)) score += 20;
                else if (spriteName.EndsWith("_1", StringComparison.OrdinalIgnoreCase)) score += 12;
                if ((entry.Source ?? string.Empty).IndexOf("SpriteRenderer[Obj]", StringComparison.OrdinalIgnoreCase) >= 0)
                    score += 15;

                LootContainerIconCandidate candidate = new LootContainerIconCandidate
                {
                    Sprite = entry.Sprite,
                    Source = entry.Source,
                    Score = score
                };
                if (auditCandidates != null) auditCandidates.Add(candidate);

                if (score > bestScore)
                {
                    secondScore = bestScore;
                    bestScore = score;
                    best = candidate;
                }
                else if (score > secondScore)
                {
                    secondScore = score;
                }
            }

            StringBuilder sb = null;
            if (auditCandidates != null)
            {
                auditCandidates.Sort(delegate(LootContainerIconCandidate a, LootContainerIconCandidate b)
                {
                    int score = b.Score.CompareTo(a.Score);
                    if (score != 0) return score;
                    return string.Compare(a.Source, b.Source, StringComparison.OrdinalIgnoreCase);
                });

                sb = new StringBuilder();
                sb.Append("familyCandidates=[");
                int describeCount = Math.Min(10, auditCandidates.Count);
                for (int i = 0; i < describeCount; i++)
                {
                    if (i > 0) sb.Append(" | ");
                    LootContainerIconCandidate c = auditCandidates[i];
                    sb.Append(c.Source).Append("=")
                        .Append(DescribeSprite(c.Sprite)).Append("/score=")
                        .Append(c.Score.ToString(CultureInfo.InvariantCulture));
                }
                if (auditCandidates.Count > describeCount)
                    sb.Append(" | ...+").Append((auditCandidates.Count - describeCount).ToString(CultureInfo.InvariantCulture));
                sb.Append("]");
            }

            // Auto-display only a genuinely distinctive family match. A tie at the
            // same top score remains unresolved and is audit-only.
            if (best != null && best.Sprite != null && bestScore >= 315 &&
                (secondScore < bestScore || secondScore < 315))
            {
                icon = best.Sprite;
                source = best.Source ?? string.Empty;
                if (ModderMode)
                    audit = "familyFallback=selected; score=" +
                        bestScore.ToString(CultureInfo.InvariantCulture) + "; " + sb.ToString();
                return true;
            }

            if (ModderMode)
                audit = "familyFallback=none; best=" +
                    (bestScore == int.MinValue ? "0" : bestScore.ToString(CultureInfo.InvariantCulture)) +
                    "; second=" +
                    (secondScore == int.MinValue ? "0" : secondScore.ToString(CultureInfo.InvariantCulture)) +
                    "; " + sb.ToString();
            return false;
        }

        private static string BuildGenericContainerNeighborhoodAudit(
            string containerId,
            LootContainerRendererSnapshot[] renderers)
        {
            if (string.IsNullOrEmpty(containerId) || renderers == null)
                return "neighborhood=[]";

            string id = containerId.ToLowerInvariant();
            bool wantsBox = id.IndexOf("box", StringComparison.Ordinal) >= 0;
            bool wantsContainer = id.IndexOf("container", StringComparison.Ordinal) >= 0;
            bool wantsWood = id.IndexOf("wood", StringComparison.Ordinal) >= 0;

            List<LootContainerIconCandidate> candidates = new List<LootContainerIconCandidate>();
            for (int i = 0; i < renderers.Length; i++)
            {
                LootContainerRendererSnapshot entry = renderers[i];
                if (entry == null || entry.Sprite == null) continue;

                string combined = ((entry.ObjectName ?? string.Empty) + " " +
                    (entry.SpriteName ?? string.Empty) + " " +
                    (entry.Hierarchy ?? string.Empty)).ToLowerInvariant();

                if (combined.IndexOf("explosion", StringComparison.Ordinal) >= 0 ||
                    combined.IndexOf("effect", StringComparison.Ordinal) >= 0 ||
                    combined.IndexOf("shadow", StringComparison.Ordinal) >= 0 ||
                    combined.IndexOf("gib", StringComparison.Ordinal) >= 0 ||
                    combined.IndexOf("ui", StringComparison.Ordinal) >= 0)
                    continue;

                bool hasBox = combined.IndexOf("box", StringComparison.Ordinal) >= 0 ||
                    combined.IndexOf("crate", StringComparison.Ordinal) >= 0 ||
                    combined.IndexOf("chest", StringComparison.Ordinal) >= 0;
                bool hasContainer = combined.IndexOf("container", StringComparison.Ordinal) >= 0 ||
                    combined.IndexOf("cabinet", StringComparison.Ordinal) >= 0 ||
                    combined.IndexOf("case", StringComparison.Ordinal) >= 0;
                bool hasWood = combined.IndexOf("wood", StringComparison.Ordinal) >= 0;

                if (wantsWood && (!hasWood || !hasBox)) continue;
                if (!wantsWood && wantsBox && !hasBox) continue;
                if (!wantsWood && wantsContainer && !hasContainer) continue;

                int score = 100;
                if (hasBox) score += 30;
                if (hasContainer) score += 20;
                if (hasWood) score += 80;
                if (combined.IndexOf("common", StringComparison.Ordinal) >= 0) score += 50;
                if ((entry.Source ?? string.Empty).IndexOf("SpriteRenderer[Obj]", StringComparison.OrdinalIgnoreCase) >= 0)
                    score += 20;
                if ((entry.SpriteName ?? string.Empty).EndsWith("_0", StringComparison.OrdinalIgnoreCase)) score += 15;
                else if ((entry.SpriteName ?? string.Empty).EndsWith("_1", StringComparison.OrdinalIgnoreCase)) score += 10;

                candidates.Add(new LootContainerIconCandidate
                {
                    Sprite = entry.Sprite,
                    Source = entry.Source,
                    Score = score
                });
            }

            candidates.Sort(delegate(LootContainerIconCandidate a, LootContainerIconCandidate b)
            {
                int score = b.Score.CompareTo(a.Score);
                if (score != 0) return score;
                return string.Compare(a.Source, b.Source, StringComparison.OrdinalIgnoreCase);
            });

            StringBuilder sb = new StringBuilder();
            sb.Append("neighborhood=[");
            int count = Math.Min(12, candidates.Count);
            for (int i = 0; i < count; i++)
            {
                if (i > 0) sb.Append(" | ");
                LootContainerIconCandidate c = candidates[i];
                sb.Append(c.Source).Append("=")
                    .Append(DescribeSprite(c.Sprite)).Append("/score=")
                    .Append(c.Score.ToString(CultureInfo.InvariantCulture));
            }
            if (candidates.Count > count)
                sb.Append(" | ...+").Append((candidates.Count - count).ToString(CultureInfo.InvariantCulture));
            sb.Append("]");
            return sb.ToString();
        }

        private static string BuildLootContainerRendererHierarchy(SpriteRenderer renderer)
        {
            if (renderer == null) return string.Empty;
            StringBuilder sb = new StringBuilder();
            Transform current = null;
            try { current = renderer.transform; }
            catch { current = null; }

            // v1.7.35-test5: several generic loot records (common_box/common_rack/
            // tool_case) do not expose a visual id and their SpriteRenderer child is
            // simply named "Obj". The prefab/root hierarchy often keeps the semantic
            // obstacle name, so include a shallow ancestry path in matching without
            // scanning any additional Unity assets.
            int depth = 0;
            while (current != null && depth < 5)
            {
                string name = string.Empty;
                try { name = current.gameObject != null ? current.gameObject.name : current.name; }
                catch { name = string.Empty; }
                if (!string.IsNullOrEmpty(name))
                {
                    if (sb.Length > 0) sb.Append('/');
                    sb.Append(name);
                }
                try { current = current.parent; }
                catch { current = null; }
                depth++;
            }
            return sb.ToString();
        }

        private static int ScoreLoadedContainerVisualName(string candidateName, List<string> aliases)
        {
            if (string.IsNullOrEmpty(candidateName) || aliases == null) return 0;
            string candidateKey = NormalizeContainerVisualName(candidateName);
            if (candidateKey.Length < 3) return 0;

            int best = 0;
            for (int i = 0; i < aliases.Count; i++)
            {
                string alias = aliases[i];
                if (string.IsNullOrEmpty(alias)) continue;
                string aliasKey = NormalizeContainerVisualName(alias);
                if (aliasKey.Length < 3) continue;

                int score = 0;
                if (string.Equals(candidateKey, aliasKey, StringComparison.Ordinal))
                    score = 260;
                else if (candidateKey.Length >= 7 && aliasKey.Length >= 7 &&
                    (candidateKey.IndexOf(aliasKey, StringComparison.Ordinal) >= 0 ||
                     aliasKey.IndexOf(candidateKey, StringComparison.Ordinal) >= 0))
                    score = 220;
                else
                    score = ScoreContainerVisualTokens(candidateName, alias);

                if (score > best) best = score;
            }
            return best;
        }

        private static int ScoreContainerVisualTokens(string candidateName, string alias)
        {
            List<string> candidateTokens = TokenizeContainerVisualName(candidateName);
            List<string> aliasTokens = TokenizeContainerVisualName(alias);
            if (candidateTokens.Count == 0 || aliasTokens.Count == 0) return 0;

            int matched = 0;
            int distinctiveMatched = 0;
            for (int i = 0; i < aliasTokens.Count; i++)
            {
                string a = aliasTokens[i];
                if (a.Length < 3) continue;
                bool found = false;
                for (int j = 0; j < candidateTokens.Count; j++)
                {
                    string c = candidateTokens[j];
                    if (ContainerVisualTokensMatch(a, c))
                    {
                        found = true;
                        break;
                    }
                }
                if (found)
                {
                    matched++;
                    if (!IsGenericContainerVisualToken(a)) distinctiveMatched++;
                }
            }

            if (matched >= 3 && distinctiveMatched >= 1) return 210;
            if (matched >= 2 && distinctiveMatched >= 1) return 195;
            if (matched >= 2) return 175;
            if (matched == 1 && distinctiveMatched == 1) return 155;
            return 0;
        }

        private static bool ContainerVisualTokensMatch(string a, string b)
        {
            if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) return true;
            if (a.Length >= 5 && b.Length >= 5)
            {
                int prefix = Math.Min(6, Math.Min(a.Length, b.Length));
                return string.Compare(a, 0, b, 0, prefix, StringComparison.OrdinalIgnoreCase) == 0;
            }
            return false;
        }

        private static bool IsGenericContainerVisualToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return true;
            string t = token.ToLowerInvariant();
            return t == "container" || t == "case" || t == "box" || t == "locker" ||
                t == "holder" || t == "rack" || t == "barrel" || t == "sink" ||
                t == "toilet" || t == "tank" || t == "stand" || t == "chest" ||
                t == "growbox" || t == "value" || t == "record" || t == "obstacle" ||
                t == "small" || t == "big" || t == "large" || t == "common";
        }

        private static List<string> TokenizeContainerVisualName(string value)
        {
            List<string> tokens = new List<string>();
            if (string.IsNullOrEmpty(value)) return tokens;
            StringBuilder current = new StringBuilder();
            char previous = '\0';
            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];
                bool isLetterOrDigit = char.IsLetterOrDigit(ch);
                bool camelBreak = isLetterOrDigit && char.IsUpper(ch) &&
                    current.Length > 0 && char.IsLetterOrDigit(previous) && char.IsLower(previous);
                if (!isLetterOrDigit || camelBreak)
                {
                    if (current.Length > 0)
                    {
                        tokens.Add(current.ToString().ToLowerInvariant());
                        current.Length = 0;
                    }
                    if (!isLetterOrDigit)
                    {
                        previous = ch;
                        continue;
                    }
                }
                current.Append(char.ToLowerInvariant(ch));
                previous = ch;
            }
            if (current.Length > 0) tokens.Add(current.ToString().ToLowerInvariant());
            return tokens;
        }

        private static string NormalizeContainerVisualName(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            StringBuilder sb = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];
                if (char.IsLetterOrDigit(ch))
                    sb.Append(char.ToLowerInvariant(ch));
            }
            string result = sb.ToString();
            if (result.EndsWith("value", StringComparison.Ordinal) && result.Length > 5)
                result = result.Substring(0, result.Length - 5);
            return result;
        }

        private static int ScoreContainerVisualStateHint(string value)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            int score = 0;
            if (value.IndexOf("closed", StringComparison.OrdinalIgnoreCase) >= 0) score += 18;
            if (value.IndexOf("default", StringComparison.OrdinalIgnoreCase) >= 0) score += 14;
            if (value.IndexOf("idle", StringComparison.OrdinalIgnoreCase) >= 0) score += 12;
            if (value.IndexOf("normal", StringComparison.OrdinalIgnoreCase) >= 0) score += 8;
            if (value.IndexOf("open", StringComparison.OrdinalIgnoreCase) >= 0) score -= 12;
            if (value.IndexOf("broken", StringComparison.OrdinalIgnoreCase) >= 0) score -= 18;
            if (value.IndexOf("destroy", StringComparison.OrdinalIgnoreCase) >= 0) score -= 18;
            if (value.IndexOf("damage", StringComparison.OrdinalIgnoreCase) >= 0) score -= 12;
            if (value.IndexOf("hover", StringComparison.OrdinalIgnoreCase) >= 0) score -= 20;
            return score;
        }

        private static void CollectLootContainerIconCandidates(
            object value,
            string path,
            int depth,
            HashSet<object> visited,
            List<LootContainerIconCandidate> output)
        {
            if (value == null || depth > 3 || output == null) return;

            Sprite directSprite = value as Sprite;
            if (directSprite != null)
            {
                AddLootContainerIconCandidate(output, directSprite, path, ScoreLootContainerSpritePath(path, 0));
                return;
            }

            SpriteRenderer renderer = value as SpriteRenderer;
            if (renderer != null)
            {
                if (renderer.sprite != null)
                    AddLootContainerIconCandidate(output, renderer.sprite, path + ".sprite",
                        ScoreLootContainerSpritePath(path, 185));
                return;
            }

            GameObject go = value as GameObject;
            if (go != null)
            {
                CollectLootContainerPrefabRenderers(go, path, output);
                return;
            }

            Component component = value as Component;
            if (component != null)
            {
                if (component.gameObject != null)
                    CollectLootContainerPrefabRenderers(component.gameObject, path + ".gameObject", output);
                return;
            }

            if (value is string || value.GetType().IsPrimitive || value is decimal || value is Enum)
                return;
            if (value is UnityEngine.Object)
                return;

            if (visited != null)
            {
                if (visited.Contains(value)) return;
                visited.Add(value);
            }

            Type type = value.GetType();
            FieldInfo[] fields;
            try { fields = type.GetFields(InstanceFlags); }
            catch { fields = new FieldInfo[0]; }
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (field == null) continue;
                object child;
                try { child = field.GetValue(value); }
                catch { continue; }
                if (!ShouldInspectLootContainerVisualMember(field.Name, child, depth)) continue;
                CollectLootContainerIconCandidates(child, path + "." + field.Name, depth + 1, visited, output);
            }

            PropertyInfo[] props;
            try { props = type.GetProperties(InstanceFlags); }
            catch { props = new PropertyInfo[0]; }
            for (int i = 0; i < props.Length; i++)
            {
                PropertyInfo prop = props[i];
                if (prop == null || !prop.CanRead || prop.GetIndexParameters().Length != 0) continue;
                object child;
                try { child = prop.GetValue(value, null); }
                catch { continue; }
                if (!ShouldInspectLootContainerVisualMember(prop.Name, child, depth)) continue;
                CollectLootContainerIconCandidates(child, path + "." + prop.Name, depth + 1, visited, output);
            }
        }

        private static bool ShouldInspectLootContainerVisualMember(string name, object value, int depth)
        {
            if (value == null) return false;
            if (value is Sprite || value is SpriteRenderer || value is GameObject || value is Component)
                return true;
            if (depth >= 2) return false;

            string n = name ?? string.Empty;
            return n.IndexOf("icon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("sprite", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("prefab", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("visual", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("view", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("renderer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("content", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("object", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("closed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("idle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("default", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("obstacle", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void CollectLootContainerPrefabRenderers(
            GameObject go,
            string path,
            List<LootContainerIconCandidate> output)
        {
            if (go == null) return;
            SpriteRenderer rootRenderer = null;
            try { rootRenderer = go.GetComponent<SpriteRenderer>(); }
            catch { rootRenderer = null; }
            if (rootRenderer != null && rootRenderer.sprite != null)
            {
                AddLootContainerIconCandidate(output, rootRenderer.sprite,
                    path + ".rootSpriteRenderer", 195);
            }

            SpriteRenderer[] renderers;
            try { renderers = go.GetComponentsInChildren<SpriteRenderer>(true); }
            catch { renderers = new SpriteRenderer[0]; }

            int nonNullCount = 0;
            SpriteRenderer only = null;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || renderers[i].sprite == null) continue;
                nonNullCount++;
                only = renderers[i];
            }
            if (nonNullCount == 1 && only != null && only.sprite != null)
            {
                AddLootContainerIconCandidate(output, only.sprite,
                    path + ".singleSpriteRenderer(" + only.gameObject.name + ")", 185);
            }
            else if (nonNullCount > 1)
            {
                // Multiple layered world sprites are useful audit evidence, but are not
                // considered proven standalone icons unless one is also the root renderer.
                for (int i = 0; i < renderers.Length && i < 12; i++)
                {
                    SpriteRenderer sr = renderers[i];
                    if (sr == null || sr.sprite == null) continue;
                    AddLootContainerIconCandidate(output, sr.sprite,
                        path + ".layer(" + sr.gameObject.name + ")", 120);
                }
            }
        }

        private static int ScoreLootContainerSpritePath(string path, int baseScore)
        {
            string p = path ?? string.Empty;
            int score = baseScore > 0 ? baseScore : 120;
            if (p.IndexOf("icon", StringComparison.OrdinalIgnoreCase) >= 0) score = Math.Max(score, 205);
            if (p.IndexOf("sprite", StringComparison.OrdinalIgnoreCase) >= 0) score = Math.Max(score, 185);
            if (p.IndexOf("closed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                p.IndexOf("idle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                p.IndexOf("default", StringComparison.OrdinalIgnoreCase) >= 0)
                score = Math.Max(score, 175);
            return score;
        }

        private static void AddLootContainerIconCandidate(
            List<LootContainerIconCandidate> output,
            Sprite sprite,
            string source,
            int score)
        {
            if (output == null || sprite == null) return;
            for (int i = 0; i < output.Count; i++)
            {
                LootContainerIconCandidate existing = output[i];
                if (existing != null && object.ReferenceEquals(existing.Sprite, sprite) &&
                    string.Equals(existing.Source, source, StringComparison.Ordinal))
                    return;
            }
            output.Add(new LootContainerIconCandidate
            {
                Sprite = sprite,
                Source = source ?? string.Empty,
                Score = score
            });
        }

        private static Sprite TryResolveLootContainerSmallIcon(string containerId)
        {
            if (string.IsNullOrEmpty(containerId)) return null;

            Sprite cached;
            if (LootContainerIconsById.TryGetValue(containerId, out cached))
                return cached;
            if (LootContainerIconMisses.Contains(containerId))
                return GetBrowserInterfaceIconSprite(BrowserInterfaceIconKind.Loot);

            EnsureLootContainerRecordCache();
            object record = null;
            LootContainerRecordsById.TryGetValue(containerId, out record);

            if (record == null)
            {
                Sprite exactMissingRecord = TryResolveMissingRecordLootContainerIcon(containerId);
                return exactMissingRecord ?? GetBrowserInterfaceIconSprite(BrowserInterfaceIconKind.Loot);
            }

            if (IsKnownAmbiguousLootContainerVisual(containerId))
            {
                LootContainerIconMisses.Add(containerId);
                if (ModderMode)
                    Debug.Log("[ItemIntelligence][ContainerIconPerf] lazy id=" + containerId +
                        "; selected=<none>; reason=runtime-audited-ambiguous; heuristicScan=SKIPPED.");
                return GetBrowserInterfaceIconSprite(BrowserInterfaceIconKind.Loot);
            }

            int resolveStarted = ModderMode ? Environment.TickCount : 0;
            Sprite icon;
            string source;
            string audit;

            // First inspect the canonical record itself. If a future game build exposes
            // a direct Sprite/GameObject/visual reference, resolve it without scanning
            // the global SpriteRenderer universe. The global catalog is now a fallback.
            if (TryResolveLootContainerVisual(
                record, containerId, null, null, out icon, out source, out audit))
            {
                LootContainerIconsById[containerId] = icon;
                LootContainerIconSourcesById[containerId] = source ?? string.Empty;
                if (ModderMode)
                {
                    int directElapsed = unchecked(Environment.TickCount - resolveStarted);
                    if (directElapsed < 0) directElapsed = 0;
                    Debug.Log("[ItemIntelligence][ContainerIconAudit] lazy id=" + containerId +
                        "; resolveMs=" + directElapsed.ToString(CultureInfo.InvariantCulture) +
                        "; directRecord=true; " + audit);
                }
                return icon;
            }

            EnsureLootContainerIconsResolved();

            if (TryResolveIndexedCanonicalContainerIcon(containerId, out icon, out source, out audit))
            {
                LootContainerIconsById[containerId] = icon;
                LootContainerIconSourcesById[containerId] = source ?? string.Empty;
                if (ModderMode)
                {
                    int elapsed = unchecked(Environment.TickCount - resolveStarted);
                    if (elapsed < 0) elapsed = 0;
                    Debug.Log("[ItemIntelligence][ContainerIconAudit] lazy id=" + containerId +
                        "; resolveMs=" + elapsed.ToString(CultureInfo.InvariantCulture) + "; " + audit);
                }
                return icon;
            }
            if (TryResolveLootContainerVisual(
                record,
                containerId,
                null,
                _lootContainerRendererCatalog,
                out icon,
                out source,
                out audit))
            {
                LootContainerIconsById[containerId] = icon;
                LootContainerIconSourcesById[containerId] = source ?? string.Empty;
                if (ModderMode)
                {
                    int elapsed = unchecked(Environment.TickCount - resolveStarted);
                    if (elapsed < 0) elapsed = 0;
                    Debug.Log("[ItemIntelligence][ContainerIconAudit] lazy id=" + containerId +
                        "; resolveMs=" + elapsed.ToString(CultureInfo.InvariantCulture) +
                        "; " + audit);
                }
                return icon;
            }

            string familyAudit;
            if (TryResolveGenericContainerFamilyFallback(
                containerId,
                _lootContainerRendererCatalog,
                out icon,
                out source,
                out familyAudit))
            {
                LootContainerIconsById[containerId] = icon;
                LootContainerIconSourcesById[containerId] = source ?? string.Empty;
                if (ModderMode)
                {
                    int elapsed = unchecked(Environment.TickCount - resolveStarted);
                    if (elapsed < 0) elapsed = 0;
                    Debug.Log("[ItemIntelligence][ContainerIconAudit] lazy id=" + containerId +
                        "; resolveMs=" + elapsed.ToString(CultureInfo.InvariantCulture) +
                        "; " + audit + "; " + familyAudit);
                }
                return icon;
            }

            LootContainerIconMisses.Add(containerId);
            if (ModderMode)
            {
                int missElapsed = unchecked(Environment.TickCount - resolveStarted);
                if (missElapsed < 0) missElapsed = 0;
                string neighborhoodAudit = BuildGenericContainerNeighborhoodAudit(
                    containerId, _lootContainerRendererCatalog);
                string targetedAudit = string.Equals(containerId, "water_sink", StringComparison.OrdinalIgnoreCase)
                    ? "; " + BuildWaterSinkTargetAudit()
                    : (string.Equals(containerId, "ammo_case", StringComparison.OrdinalIgnoreCase)
                        ? "; " + BuildAmmoCaseTargetAudit()
                        : (containerId.StartsWith("weapon_case_", StringComparison.OrdinalIgnoreCase)
                            ? "; " + BuildWeaponCaseTargetAudit(containerId)
                            : string.Empty));
                Debug.Log("[ItemIntelligence][ContainerIconAudit] lazy id=" + containerId +
                    "; resolveMs=" + missElapsed.ToString(CultureInfo.InvariantCulture) +
                    "; " + audit + "; " + familyAudit + "; " + neighborhoodAudit + targetedAudit);
            }
            return GetBrowserInterfaceIconSprite(BrowserInterfaceIconKind.Loot);
        }
    }
}
