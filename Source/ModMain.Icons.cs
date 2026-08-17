using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using MGSC;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ItemIntelligence
{
    /// <summary>
    /// Vanilla-first item icon discovery, scoring and cache owner.
    /// Extracted in v1.7.36-test10 without changing runtime behavior.
    /// </summary>
    public static partial class ModMain
    {
        private static readonly Dictionary<string, Sprite> ItemSmallIcons =
            new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> ItemSmallIconMisses =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Runtime evidence from real vanilla ItemSlot instances. This cache is filled
        // only from an unambiguous vanilla Image member; it never scans every Sprite.
        private static readonly Dictionary<string, Sprite> VanillaObservedItemIcons =
            new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private static int _iconMissingAuditCount;
        private static bool _iconFailureSchemaLogged;

        private static Sprite TryResolveItemSmallIcon(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;

            Sprite cached;
            if (ItemSmallIcons.TryGetValue(itemId, out cached) && cached != null)
                return cached;

            // If we have already observed the sprite on a real vanilla ItemSlot, that is
            // the strongest possible source: it is literally the sprite vanilla rendered.
            Sprite observed;
            if (VanillaObservedItemIcons.TryGetValue(itemId, out observed) && observed != null)
            {
                ItemSmallIconMisses.Remove(itemId);
                ItemSmallIcons[itemId] = observed;
                return observed;
            }

            if (ItemSmallIconMisses.Contains(itemId))
                return null;

            object record;
            if (!ItemRecordsById.TryGetValue(itemId, out record) || record == null)
                return null;

            string source;
            Sprite resolved = TryResolveCanonicalItemSmallIcon(record, out source);

            // Quasimorph 1.0 stores most item data in CompositeItemRecord. The root does
            // not expose SmallIcon/InventoryIcon even though one of its Records contains
            // the exact vanilla inventory sprite. Test1 proved that root-only lookup was
            // therefore too strict and removed almost every mini-icon. Walk only the
            // explicit composite record chain and accept only sprites that look like
            // vanilla inventory assets (normally *_inv). This restores coverage without
            // returning to the old "first Sprite anywhere in the graph" behavior.
            if (resolved == null)
            {
                string compositeSource;
                resolved = TryResolveCompositeInventoryIcon(record, itemId, out compositeSource);
                if (resolved != null) source = compositeSource;
            }

            // Dynamic/runtime variants sometimes point at a stable base item. Only retry
            // the same conservative resolver on that base record; never return to the old
            // arbitrary object-graph Sprite scan.
            if (resolved == null)
            {
                string baseId = ResolveStaticRelationItemId(itemId);
                if (!string.IsNullOrEmpty(baseId) &&
                    !string.Equals(baseId, itemId, StringComparison.OrdinalIgnoreCase))
                {
                    object baseRecord;
                    if (ItemRecordsById.TryGetValue(baseId, out baseRecord) && baseRecord != null)
                    {
                        string baseSource;
                        resolved = TryResolveCanonicalItemSmallIcon(baseRecord, out baseSource);
                        if (resolved == null)
                        {
                            string compositeBaseSource;
                            resolved = TryResolveCompositeInventoryIcon(baseRecord, baseId, out compositeBaseSource);
                            if (resolved != null) baseSource = compositeBaseSource;
                        }
                        if (resolved != null) source = "base:" + baseId + "/" + baseSource;
                    }
                }
            }

            if (resolved != null)
            {
                ItemSmallIcons[itemId] = resolved;
                return resolved;
            }

            ItemSmallIconMisses.Add(itemId);
            LogMissingCompositeIconCandidates(itemId, record);
            LogStrictIconSchemaOnce(itemId, record);
            return null;
        }

        private static Sprite TryResolveCanonicalItemSmallIcon(object record, out string source)
        {
            source = string.Empty;
            if (record == null) return null;

            // Order mirrors the semantic intent of vanilla UI data. The key safety change
            // from <=1.7.33 is that we no longer walk the whole item graph and accept the
            // first Sprite/BigIcon/PreviewIcon we happen to encounter.
            // Prefer an explicit vanilla-style resolver on the root record. If the record
            // carries atlas/composition metadata, this is the path most likely to apply it.
            Sprite methodSprite = TryInvokeExactSmallIconResolver(record);
            if (methodSprite != null)
            {
                source = "record.small-icon-resolver";
                return methodSprite;
            }

            string[] members = new string[] { "SmallIcon", "InventoryIcon" };
            for (int i = 0; i < members.Length; i++)
            {
                object token = GetMember(record, members[i]);
                if (token == null) continue;

                Sprite sprite = ResolveCanonicalSmallIconToken(token, 0);
                if (sprite != null)
                {
                    source = "record." + members[i];
                    return sprite;
                }
            }

            // Some records expose only Icon. Keep it as a final root-only compatibility
            // fallback, but never inspect PreviewIcon, BigIcon or arbitrary nested Sprites.
            object icon = GetMember(record, "Icon");
            if (icon != null)
            {
                Sprite sprite = ResolveCanonicalSmallIconToken(icon, 0);
                if (sprite != null && ScoreVanillaInventorySprite(sprite, string.Empty) >= 80)
                {
                    source = "record.Icon-fallback/inventory-shaped";
                    return sprite;
                }
            }

            return null;
        }

        private static Sprite TryResolveCompositeInventoryIcon(object root, string itemId, out string source)
        {
            source = string.Empty;
            if (root == null) return null;

            Queue<GraphScanNode> queue = new Queue<GraphScanNode>();
            HashSet<object> seen = new HashSet<object>(ReferenceComparer.Instance);
            queue.Enqueue(new GraphScanNode(root, 0));

            Sprite best = null;
            string bestSource = string.Empty;
            int bestScore = 0;
            int inspected = 0;

            while (queue.Count > 0 && inspected < 48)
            {
                GraphScanNode current = queue.Dequeue();
                object node = current.Value;
                if (node == null || node is string) continue;
                Type type = node.GetType();
                if (IsSimple(type)) continue;
                if (seen.Contains(node)) continue;
                seen.Add(node);
                inspected++;

                Sprite direct = node as Sprite;
                if (direct != null)
                {
                    ConsiderInventorySpriteCandidate(direct, itemId, "composite.direct", ref best, ref bestSource, ref bestScore);
                    continue;
                }

                // Explicit resolver methods are safe to try, but their result is accepted
                // only when it has vanilla inventory-sprite characteristics.
                Sprite viaExact = TryInvokeExactSmallIconResolver(node);
                if (viaExact != null)
                    ConsiderInventorySpriteCandidate(viaExact, itemId, "composite." + type.Name + ".small-icon-resolver", ref best, ref bestSource, ref bestScore);

                // Inspect only icon/sprite-named members. Unlike the pre-1.7.34 resolver,
                // unrelated record fields can never become icon candidates.
                try
                {
                    List<MemberInfo> members = GetReadableMembers(type);
                    for (int i = 0; i < members.Count; i++)
                    {
                        MemberInfo member = members[i];
                        string name = member.Name ?? string.Empty;
                        if (name.IndexOf("Icon", StringComparison.OrdinalIgnoreCase) < 0 &&
                            name.IndexOf("Sprite", StringComparison.OrdinalIgnoreCase) < 0)
                            continue;

                        object token = GetMemberValue(node, member);
                        if (token == null || object.ReferenceEquals(token, node)) continue;
                        Sprite candidate = ResolveIconToken(token, 0);
                        if (candidate != null)
                            ConsiderInventorySpriteCandidate(candidate, itemId,
                                "composite." + type.Name + "." + name,
                                ref best, ref bestSource, ref bestScore);
                    }
                }
                catch { }

                if (current.Depth >= 3) continue;

                // CompositeItemRecord's canonical child collection.
                object records = GetMember(node, "Records");
                IEnumerable enumerableRecords = records as IEnumerable;
                if (enumerableRecords != null && !(records is string))
                {
                    int n = 0;
                    foreach (object child in enumerableRecords)
                    {
                        if (++n > 32) break;
                        if (child != null) queue.Enqueue(new GraphScanNode(child, current.Depth + 1));
                    }
                }

                // A few runtime wrappers use one of these canonical record links.
                string[] links = new string[]
                {
                    "Record", "ItemRecord", "PrimaryRecord", "ContentRecord",
                    "Descriptor", "ContentDescriptor"
                };
                for (int i = 0; i < links.Length; i++)
                {
                    object child = GetMember(node, links[i]);
                    if (child != null && !object.ReferenceEquals(child, node))
                        queue.Enqueue(new GraphScanNode(child, current.Depth + 1));
                }
            }

            if (best != null)
            {
                source = bestSource + "/score=" + bestScore.ToString(CultureInfo.InvariantCulture);
                return best;
            }

            return null;
        }

        private static void ConsiderInventorySpriteCandidate(
            Sprite candidate,
            string itemId,
            string candidateSource,
            ref Sprite best,
            ref string bestSource,
            ref int bestScore)
        {
            if (candidate == null) return;
            int score = ScoreVanillaInventorySprite(candidate, itemId);

            // Test2 proved that *_inv is the dominant vanilla convention, but a small
            // minority of perfectly valid resource/component icons do not follow that
            // naming scheme. Do not fall back to arbitrary graph sprites. Instead allow
            // a non-*_inv candidate only when it comes from a semantic Icon member on a
            // descriptor that belongs to the CompositeItemRecord itself. This preserves
            // the safety gain of test2 while covering items such as common crafting
            // components whose icon asset name is legacy/non-standard.
            int trustedScore = ScoreTrustedDescriptorIcon(candidate, candidateSource);
            if (score < 80)
            {
                if (trustedScore <= 0) return;
                score = trustedScore;
            }
            else if (trustedScore > score)
            {
                score = trustedScore;
            }

            if (score <= bestScore) return;
            best = candidate;
            bestSource = candidateSource ?? string.Empty;
            bestScore = score;
        }

        private static int ScoreTrustedDescriptorIcon(Sprite sprite, string candidateSource)
        {
            if (sprite == null || string.IsNullOrEmpty(candidateSource)) return 0;

            string lowerSource = candidateSource.ToLowerInvariant();
            if (!lowerSource.StartsWith("composite.", StringComparison.Ordinal)) return 0;
            if (lowerSource.IndexOf("descriptor.", StringComparison.Ordinal) < 0) return 0;

            // Only semantic icon members are trusted. Preview/BigIcon remain excluded by
            // the composite walker and are explicitly rejected here as defense in depth.
            if (lowerSource.IndexOf("previewicon", StringComparison.Ordinal) >= 0 ||
                lowerSource.IndexOf("bigicon", StringComparison.Ordinal) >= 0) return 0;

            int lastDot = lowerSource.LastIndexOf('.');
            string member = lastDot >= 0 ? lowerSource.Substring(lastDot + 1) : lowerSource;
            int slash = member.IndexOf('/');
            if (slash >= 0) member = member.Substring(0, slash);
            if (member != "icon" && member != "_icon" && member != "smallicon" &&
                member != "inventoryicon" && member != "sprite" && member != "_sprite") return 0;

            string spriteName = string.Empty;
            string textureName = string.Empty;
            int width = 0;
            int height = 0;
            try
            {
                spriteName = sprite.name ?? string.Empty;
                textureName = sprite.texture == null ? string.Empty : (sprite.texture.name ?? string.Empty);
                UnityEngine.Rect r = sprite.rect;
                width = (int)r.width;
                height = (int)r.height;
            }
            catch { return 0; }

            string asset = (spriteName + "|" + textureName).ToLowerInvariant();
            string[] rejected = new string[]
            {
                "slothitem", "emptycenterhover", "damageditemback", "cnd_filled",
                "itemmodedicon", "sandclock"
            };
            for (int i = 0; i < rejected.Length; i++)
                if (asset.IndexOf(rejected[i], StringComparison.Ordinal) >= 0) return 0;

            // Inventory sprites in the current build are tiny. This blocks portraits,
            // previews and other presentation art even when they happen to sit on an
            // Icon-named descriptor member.
            if (width < 6 || height < 6 || width > 64 || height > 64) return 0;

            if (lowerSource.IndexOf("itemcontentdescriptor.", StringComparison.Ordinal) >= 0) return 125;
            if (member == "smallicon" || member == "inventoryicon") return 120;
            return 105;
        }

        private static int ScoreVanillaInventorySprite(Sprite sprite, string itemId)
        {
            if (sprite == null) return 0;

            string spriteName = string.Empty;
            string textureName = string.Empty;
            int width = 0;
            int height = 0;
            try
            {
                spriteName = sprite.name ?? string.Empty;
                textureName = sprite.texture == null ? string.Empty : (sprite.texture.name ?? string.Empty);
                UnityEngine.Rect rect = sprite.rect;
                width = (int)rect.width;
                height = (int)rect.height;
            }
            catch { return 0; }

            string combined = spriteName + "|" + textureName;
            string lower = combined.ToLowerInvariant();

            // Known ItemSlot decorations from the runtime audit must never be treated as
            // an item picture even though they are small sprites.
            string[] rejected = new string[]
            {
                "slothitem", "emptycenterhover", "damageditemback", "cnd_filled",
                "itemmodedicon", "sandclock"
            };
            for (int i = 0; i < rejected.Length; i++)
                if (lower.IndexOf(rejected[i], StringComparison.Ordinal) >= 0) return 0;

            int score = 0;
            if (lower.IndexOf("_inv", StringComparison.Ordinal) >= 0) score += 100;
            if (lower.EndsWith("inv", StringComparison.Ordinal)) score += 20;
            if (lower.IndexOf("inventory", StringComparison.Ordinal) >= 0) score += 90;

            if (!string.IsNullOrEmpty(itemId))
            {
                string id = itemId.ToLowerInvariant();
                if (lower.IndexOf(id + "_inv", StringComparison.Ordinal) >= 0) score += 60;
                else if (lower.IndexOf(id, StringComparison.Ordinal) >= 0) score += 15;
            }

            // Vanilla ItemSlot examples are roughly 22-24 px. Keep this only as a weak
            // tiebreaker; sprite naming remains the proof of inventory intent.
            if (width >= 8 && height >= 8 && width <= 64 && height <= 64) score += 10;
            if (width <= 2 || height <= 2) score -= 100;

            return score;
        }

        private static Sprite ResolveCanonicalSmallIconToken(object token, int depth)
        {
            if (token == null || depth > 3) return null;

            Sprite direct = token as Sprite;
            if (direct != null) return direct;

            Sprite exact = TryInvokeExactSmallIconResolver(token);
            if (exact != null) return exact;

            string tag = token as string;
            if (!string.IsNullOrEmpty(tag))
            {
                Sprite custom = ResolveSpriteThroughCustomResources(tag);
                if (custom != null) return custom;

                try
                {
                    UnityEngine.Object loaded = Resources.Load(tag, typeof(Sprite));
                    Sprite resourceSprite = loaded as Sprite;
                    if (resourceSprite != null) return resourceSprite;
                }
                catch { }
                return null;
            }

            // This is deliberately narrow. These names are only inspected inside an
            // already-semantic SmallIcon/InventoryIcon token, not on arbitrary item data.
            string[] nestedNames = new string[] { "Sprite", "sprite", "SmallIcon", "Value", "Tag", "Path" };
            for (int i = 0; i < nestedNames.Length; i++)
            {
                object nested = GetMember(token, nestedNames[i]);
                if (nested == null || object.ReferenceEquals(nested, token)) continue;

                Sprite nestedSprite = ResolveCanonicalSmallIconToken(nested, depth + 1);
                if (nestedSprite != null) return nestedSprite;
            }

            return null;
        }

        private static Sprite TryInvokeExactSmallIconResolver(object target)
        {
            if (target == null) return null;

            EnsureCustomResourcesFromState();

            try
            {
                MethodInfo[] methods = target.GetType().GetMethods(InstanceFlags | StaticFlags);
                string[] allowed = new string[] { "ResolveSmallIcon", "GetSmallIcon" };
                for (int a = 0; a < allowed.Length; a++)
                {
                    for (int i = 0; i < methods.Length; i++)
                    {
                        MethodInfo method = methods[i];
                        if (!string.Equals(method.Name, allowed[a], StringComparison.OrdinalIgnoreCase))
                            continue;

                        object[] args = BuildResolverArguments(method.GetParameters());
                        if (args == null) continue;

                        try
                        {
                            object raw = method.Invoke(method.IsStatic ? null : target, args);
                            Sprite sprite = raw as Sprite;
                            if (sprite != null) return sprite;

                            // Do not recursively chase arbitrary objects returned by an icon
                            // resolver. Exact vanilla-style methods are accepted only when
                            // they actually resolve to a Sprite; this prevents A->B->A cycles.
                        }
                        catch { }
                    }
                }
            }
            catch { }

            return null;
        }

        private static void CaptureVanillaItemSlotIcon(string itemId, object slot)
        {
            if (string.IsNullOrEmpty(itemId) || slot == null) return;

            Sprite captured = null;
            // Prefer explicit ItemSlot image members. These are evidence from the rendered
            // vanilla UI, not guesses from item data. Keep the list narrow on purpose.
            string[] exactImageMembers = new string[]
            {
                "_itemIcon", "ItemIcon", "_itemImage", "ItemImage",
                "_icon", "Icon"
            };

            for (int i = 0; i < exactImageMembers.Length; i++)
            {
                object raw = GetMember(slot, exactImageMembers[i]);
                Image image = raw as Image;
                if (image != null && image.sprite != null)
                {
                    captured = image.sprite;
                    break;
                }
            }

            // If the exact-name pass did not hit, inspect only Image-typed members and use
            // a conservative name score. Do not use child order or "first Image wins".
            if (captured == null)
            {
                try
                {
                    List<MemberInfo> members = GetReadableMembers(slot.GetType());
                    int bestScore = 0;
                    for (int i = 0; i < members.Count; i++)
                    {
                        MemberInfo member = members[i];
                        object raw = GetMemberValue(slot, member);
                        Image image = raw as Image;
                        if (image == null || image.sprite == null) continue;

                        string name = member.Name ?? string.Empty;
                        string lower = name.ToLowerInvariant();
                        int score = 0;
                        if (lower.IndexOf("item", StringComparison.Ordinal) >= 0) score += 60;
                        if (lower.IndexOf("icon", StringComparison.Ordinal) >= 0) score += 50;
                        if (lower.IndexOf("image", StringComparison.Ordinal) >= 0) score += 20;
                        if (lower.IndexOf("background", StringComparison.Ordinal) >= 0) score -= 100;
                        if (lower.IndexOf("frame", StringComparison.Ordinal) >= 0) score -= 100;
                        if (lower.IndexOf("rarity", StringComparison.Ordinal) >= 0) score -= 100;
                        if (lower.IndexOf("durability", StringComparison.Ordinal) >= 0) score -= 100;
                        if (lower.IndexOf("ammo", StringComparison.Ordinal) >= 0) score -= 70;
                        if (lower.IndexOf("marker", StringComparison.Ordinal) >= 0) score -= 100;
                        if (lower.IndexOf("selection", StringComparison.Ordinal) >= 0) score -= 100;

                        if (score >= 70 && score > bestScore)
                        {
                            bestScore = score;
                            captured = image.sprite;
                        }
                    }
                }
                catch { }
            }

            if (captured != null)
            {
                VanillaObservedItemIcons[itemId] = captured;
                // Replace any previous data-derived cache entry immediately. A real vanilla
                // slot always outranks a resolver guess for the same item id.
                ItemSmallIcons[itemId] = captured;
            }

        }

        private static void LogMissingCompositeIconCandidates(string itemId, object root)
        {
            // Deep graph inspection is developer diagnostics, not part of normal icon
            // resolution. Keep the strict miss cache behavior identical in stable mode.
            if (!ModderMode || root == null || _iconMissingAuditCount >= 12) return;
            _iconMissingAuditCount++;

            try
            {
                Queue<GraphScanNode> queue = new Queue<GraphScanNode>();
                HashSet<object> seen = new HashSet<object>(ReferenceComparer.Instance);
                List<string> candidates = new List<string>();
                queue.Enqueue(new GraphScanNode(root, 0));
                int inspected = 0;

                while (queue.Count > 0 && inspected < 48 && candidates.Count < 18)
                {
                    GraphScanNode current = queue.Dequeue();
                    object node = current.Value;
                    if (node == null || node is string) continue;
                    Type type = node.GetType();
                    if (IsSimple(type) || seen.Contains(node)) continue;
                    seen.Add(node);
                    inspected++;

                    try
                    {
                        List<MemberInfo> members = GetReadableMembers(type);
                        for (int i = 0; i < members.Count && candidates.Count < 18; i++)
                        {
                            MemberInfo memberInfo = members[i];
                            string name = memberInfo.Name ?? string.Empty;
                            if (name.IndexOf("Icon", StringComparison.OrdinalIgnoreCase) < 0 &&
                                name.IndexOf("Sprite", StringComparison.OrdinalIgnoreCase) < 0) continue;
                            object token = GetMemberValue(node, memberInfo);
                            if (token == null || object.ReferenceEquals(token, node)) continue;
                            Sprite sprite = ResolveIconToken(token, 0);
                            if (sprite == null) continue;
                            string src = "composite." + type.Name + "." + name;
                            candidates.Add(src + "=" + DescribeSprite(sprite) +
                                "/nameScore=" + ScoreVanillaInventorySprite(sprite, itemId).ToString(CultureInfo.InvariantCulture) +
                                "/trustedScore=" + ScoreTrustedDescriptorIcon(sprite, src).ToString(CultureInfo.InvariantCulture));
                        }
                    }
                    catch { }

                    if (current.Depth >= 3) continue;
                    object records = GetMember(node, "Records");
                    IEnumerable enumerable = records as IEnumerable;
                    if (enumerable != null && !(records is string))
                    {
                        int n = 0;
                        foreach (object child in enumerable)
                        {
                            if (++n > 32) break;
                            if (child != null) queue.Enqueue(new GraphScanNode(child, current.Depth + 1));
                        }
                    }
                    string[] links = new string[]
                    {
                        "Record", "ItemRecord", "PrimaryRecord", "ContentRecord",
                        "Descriptor", "ContentDescriptor"
                    };
                    for (int i = 0; i < links.Length; i++)
                    {
                        object child = GetMember(node, links[i]);
                        if (child != null && !object.ReferenceEquals(child, node))
                            queue.Enqueue(new GraphScanNode(child, current.Depth + 1));
                    }
                }

                Debug.LogWarning("[ItemIntelligence][IconMissingAudit] item=" + itemId +
                    "; root=" + root.GetType().FullName +
                    "; candidates=[" + string.Join(" | ", candidates.ToArray()) + "].");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ItemIntelligence][IconMissingAudit] audit skipped for " + itemId + ": " + ex.Message);
            }
        }

        private static string DescribeSprite(Sprite sprite)
        {
            if (sprite == null) return "<null>";
            try
            {
                string textureName = sprite.texture == null ? "<null>" : sprite.texture.name;
                UnityEngine.Rect r = sprite.rect;
                return (sprite.name ?? "<unnamed>") +
                    "{tex=" + textureName +
                    ",rect=" + ((int)r.x).ToString(CultureInfo.InvariantCulture) + "," +
                    ((int)r.y).ToString(CultureInfo.InvariantCulture) + "," +
                    ((int)r.width).ToString(CultureInfo.InvariantCulture) + "x" +
                    ((int)r.height).ToString(CultureInfo.InvariantCulture) + "}";
            }
            catch
            {
                try { return sprite.name ?? "<unnamed>"; }
                catch { return "<destroyed>"; }
            }
        }

        private static void LogStrictIconSchemaOnce(string itemId, object record)
        {
            if (_iconFailureSchemaLogged) return;
            _iconFailureSchemaLogged = true;

            try
            {
                List<string> parts = new List<string>();
                parts.Add("root=" + record.GetType().FullName);
                string[] names = new string[] { "SmallIcon", "InventoryIcon", "Icon", "PreviewIcon", "BigIcon" };
                for (int i = 0; i < names.Length; i++)
                {
                    object token = GetMember(record, names[i]);
                    if (token != null)
                        parts.Add(names[i] + "=" + token.GetType().FullName + ":" + ConvertToStableString(token));
                }

                MethodInfo[] methods = record.GetType().GetMethods(InstanceFlags | StaticFlags);
                List<string> resolvers = new List<string>();
                for (int i = 0; i < methods.Length; i++)
                {
                    string name = methods[i].Name ?? string.Empty;
                    if (name.IndexOf("Icon", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    ParameterInfo[] ps = methods[i].GetParameters();
                    List<string> args = new List<string>();
                    for (int p = 0; p < ps.Length; p++) args.Add(ps[p].ParameterType.FullName);
                    resolvers.Add(name + "(" + string.Join(",", args.ToArray()) + ")->" + methods[i].ReturnType.FullName);
                }
                if (resolvers.Count > 0) parts.Add("iconMethods=[" + string.Join(" | ", resolvers.ToArray()) + "]");

                Debug.LogWarning("[ItemIntelligence][IconResolver] strict resolver had no icon for " + itemId + ": " +
                    string.Join("; ", parts.ToArray()) + ".");
            }
            catch { }
        }

        private static Sprite ResolveIconToken(object token, int depth)
        {
            if (token == null || depth > 4) return null;

            Sprite direct = token as Sprite;
            if (direct != null) return direct;

            string tag = token as string;
            if (!string.IsNullOrEmpty(tag))
            {
                Sprite custom = ResolveSpriteThroughCustomResources(tag);
                if (custom != null) return custom;

                try
                {
                    UnityEngine.Object loaded = Resources.Load(tag, typeof(Sprite));
                    Sprite resourceSprite = loaded as Sprite;
                    if (resourceSprite != null) return resourceSprite;
                }
                catch { }
                return null;
            }

            Sprite viaResolver = TryInvokeItemIconResolver(token);
            if (viaResolver != null) return viaResolver;

            string[] nestedNames = new string[] { "Sprite", "sprite", "SmallIcon", "Icon", "Value", "Tag", "Path", "Id" };
            for (int i = 0; i < nestedNames.Length; i++)
            {
                object nested = GetMember(token, nestedNames[i]);
                if (nested == null || object.ReferenceEquals(nested, token)) continue;

                Sprite nestedSprite = ResolveIconToken(nested, depth + 1);
                if (nestedSprite != null) return nestedSprite;
            }

            return null;
        }

        private static Sprite TryInvokeItemIconResolver(object target)
        {
            if (target == null) return null;

            EnsureCustomResourcesFromState();

            try
            {
                MethodInfo[] methods = target.GetType().GetMethods(InstanceFlags | StaticFlags);
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    string name = method.Name ?? string.Empty;
                    if (name.IndexOf("ResolveSmallIcon", StringComparison.OrdinalIgnoreCase) < 0 &&
                        !string.Equals(name, "ResolveIcon", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(name, "GetSmallIcon", StringComparison.OrdinalIgnoreCase))
                        continue;

                    object[] args = BuildResolverArguments(method.GetParameters());
                    if (args == null) continue;

                    try
                    {
                        object raw = method.Invoke(method.IsStatic ? null : target, args);
                        Sprite sprite = raw as Sprite;
                        if (sprite != null) return sprite;
                        if (raw != null && !object.ReferenceEquals(raw, target))
                        {
                            sprite = ResolveIconToken(raw, 1);
                            if (sprite != null) return sprite;
                        }
                    }
                    catch { }
                }
            }
            catch { }

            return null;
        }

        private static object[] BuildResolverArguments(ParameterInfo[] parameters)
        {
            if (parameters == null || parameters.Length == 0) return new object[0];
            object[] args = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterInfo parameter = parameters[i];
                Type type = parameter.ParameterType;

                if (_customResources != null && type.IsAssignableFrom(_customResources.GetType()))
                {
                    args[i] = _customResources;
                    continue;
                }

                object stateService = ResolveStateModule(type);
                if (stateService != null)
                {
                    args[i] = stateService;
                    continue;
                }

                if (parameter.IsOptional)
                {
                    args[i] = parameter.DefaultValue;
                    continue;
                }

                if (type == typeof(bool)) { args[i] = false; continue; }
                if (type == typeof(int)) { args[i] = 0; continue; }

                return null;
            }

            return args;
        }

        private static void EnsureCustomResourcesFromState()
        {
            if (_customResources != null || _customResourcesResolutionAttempted) return;
            _customResourcesResolutionAttempted = true;

            Type resourcesType = AccessTools.TypeByName("MGSC.CustomResources");
            if (resourcesType == null) return;

            _customResources = ResolveStateModule(resourcesType);
            if (_customResources != null) return;

            if (typeof(UnityEngine.Object).IsAssignableFrom(resourcesType))
            {
                try
                {
                    UnityEngine.Object[] objects = Resources.FindObjectsOfTypeAll(resourcesType);
                    if (objects != null && objects.Length > 0)
                        _customResources = objects[0];
                }
                catch { }
            }

            if (_customResources != null) return;

            string[] singletonNames = new string[] { "Instance", "Current", "Default", "Shared" };
            for (int i = 0; i < singletonNames.Length && _customResources == null; i++)
            {
                try { _customResources = GetStaticMember(resourcesType, singletonNames[i]); }
                catch { }
            }

            if (_customResources != null) return;

            try
            {
                ConstructorInfo ctor = resourcesType.GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, Type.EmptyTypes, null);
                if (ctor != null && !resourcesType.IsAbstract)
                    _customResources = ctor.Invoke(new object[0]);
            }
            catch { }
        }

        private static Sprite ResolveSpriteThroughCustomResources(object token)
        {
            if (token == null) return null;

            Sprite direct = token as Sprite;
            if (direct != null) return direct;

            string tag = token as string;
            if (string.IsNullOrEmpty(tag))
            {
                tag = FirstNonEmpty(
                    GetStringMember(token, "Tag"),
                    GetStringMember(token, "Id"),
                    GetStringMember(token, "Name"),
                    GetStringMember(token, "Path"));
            }
            if (string.IsNullOrEmpty(tag)) return null;

            EnsureCustomResourcesFromState();

            Type resourcesType = _customResources != null
                ? _customResources.GetType()
                : AccessTools.TypeByName("MGSC.CustomResources");
            if (resourcesType == null) return null;

            try
            {
                MethodInfo[] methods = resourcesType.GetMethods(InstanceFlags | StaticFlags);
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    string name = method.Name ?? string.Empty;
                    if (name.IndexOf("Sprite", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (!method.IsStatic && _customResources == null) continue;

                    ParameterInfo[] parameters = method.GetParameters();
                    object[] args = new object[parameters.Length];
                    bool supported = true;

                    for (int p = 0; p < parameters.Length; p++)
                    {
                        Type pt = parameters[p].ParameterType;
                        if (pt == typeof(string)) args[p] = tag;
                        else if (pt == typeof(bool)) args[p] = false;
                        else if (pt == typeof(int)) args[p] = 0;
                        else if (parameters[p].IsOptional) args[p] = parameters[p].DefaultValue;
                        else { supported = false; break; }
                    }

                    if (!supported) continue;

                    try
                    {
                        object raw = method.Invoke(method.IsStatic ? null : _customResources, args);
                        Sprite sprite = raw as Sprite;
                        if (sprite != null) return sprite;
                        if (raw != null && !(raw is string))
                        {
                            sprite = ResolveIconToken(raw, 1);
                            if (sprite != null) return sprite;
                        }
                    }
                    catch { }
                }
            }
            catch { }

            return null;
        }

        private static Sprite ResolveSpriteDeep(object value, int depth)
        {
            return ResolveIconToken(value, depth);
        }

        private static void LogIconSchemaOnce(string itemId, object record, List<object> graph)
        {
            if (_iconFailureSchemaLogged) return;
            _iconFailureSchemaLogged = true;

            try
            {
                List<string> details = new List<string>();
                for (int i = 0; i < graph.Count && i < 12; i++)
                {
                    object node = graph[i];
                    if (node == null) continue;
                    Type type = node.GetType();
                    object small = GetMember(node, "SmallIcon");
                    string line = type.FullName;
                    if (small != null)
                        line += "[SmallIcon=" + small.GetType().FullName + "]";

                    MethodInfo[] methods = type.GetMethods(InstanceFlags | StaticFlags);
                    for (int m = 0; m < methods.Length; m++)
                    {
                        string methodName = methods[m].Name ?? string.Empty;
                        if (methodName.IndexOf("ResolveSmallIcon", StringComparison.OrdinalIgnoreCase) < 0)
                            continue;

                        ParameterInfo[] parameters = methods[m].GetParameters();
                        List<string> args = new List<string>();
                        for (int a = 0; a < parameters.Length; a++)
                            args.Add(parameters[a].ParameterType.FullName);
                        line += "[ResolveSmallIcon(" + string.Join(",", args.ToArray()) + ")]";
                    }

                    details.Add(line);
                }

                Debug.LogWarning("[ItemIntelligence] Icon resolver diagnostic " + itemId +
                    ": root=" + record.GetType().FullName +
                    ", graph=" + string.Join(" -> ", details.ToArray()) + ".");
            }
            catch { }
        }
    }
}
