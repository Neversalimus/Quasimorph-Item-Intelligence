namespace MGSC
{
    using System.Collections.Generic;
    public enum WeaponClass { None, Axe = 5, Pistol = 20, AssaultRifle = 23, GrenadeLauncher = 31 }
    public interface IArmorRecord { }
    public class ArmorRecord : IArmorRecord { }
    public class HelmetRecord : IArmorRecord { }
    public class LeggingsRecord : IArmorRecord { }
    public class BootsRecord : IArmorRecord { }
    public class ArmorSetRecord { public List<string> Items { get; set; } }
    public class WeaponRecord
    {
        public WeaponClass WeaponClass { get; set; }
        public string RequiredAmmo { get; set; }
        public string DefaultAmmoId { get; set; }
        public string DefaultGrenadeId { get; set; }
        public List<string> Traits { get; set; }
    }
    public class ThrowingWeaponRecord { public List<string> Traits { get; set; } }
    public class CompositeItemRecord
    {
        private readonly object part;
        public CompositeItemRecord(object value) { part = value; }
        public T GetRecord<T>() where T : class { return part as T; }
    }
    public class DatadiskRecord { public int UnlockType { get; set; } public List<string> UnlockIds { get; set; } }
    public class Sets { public readonly List<ArmorSetRecord> Records = new List<ArmorSetRecord>(); }
    public static class Data { public static Sets ArmorSets = new Sets(); }
}
namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static readonly HashSet<string> KnownItemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, object> ItemRecordsById = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, int> BrowserCatalogCategoryByItem = new Dictionary<string, int>();
        private static readonly Dictionary<string, int> levels = new Dictionary<string, int>();
        private static readonly Dictionary<string, bool> learned = new Dictionary<string, bool>();
        private static string language = "en";
        private static int _browserCatalogCategory, checks;
        private static object GetMember(object obj, string key)
        {
            if (obj == null) return null;
            var property = obj.GetType().GetProperty(key);
            return property == null ? null : property.GetValue(obj, null);
        }
        private static string GetStringMember(object obj, string key) { return GetMember(obj, key) as string; }
        private static string FirstNonEmpty(params string[] values) { foreach (string value in values) if (!string.IsNullOrEmpty(value)) return value; return string.Empty; }
        private static string GetItemId(object obj) { return obj as string; }
        private static string ConvertToStableString(object obj) { return Convert.ToString(obj, CultureInfo.InvariantCulture); }
        private static string NormalizeGameText(string text) { return text ?? string.Empty; }
        private static string LocalizeItem(string id) { return id; }
        private static string LocalizeCandidates(string[] keys, string fallback)
        {
            if (keys[0] == "weapon.class.AssaultRifle") return language == "zh" ? "突击步枪" : "Assault Rifle";
            return fallback;
        }
        private static string LocalizeWeaponModePayloadTrait(string id)
        {
            if (id == "suppressor") return language == "zh" ? "消音器" : "Suppressor";
            if (id == "laser_sight") return language == "zh" ? "激光瞄准器" : "Laser Sight";
            return id;
        }
        private static string Ui(string key) { return key; }
        private static CultureInfo GetUiNumberCulture() { return CultureInfo.InvariantCulture; }
        private static string GetUiPercentSuffix() { return "%"; }
        private static int GetExactItemTechLevel(string id) { int level; return levels.TryGetValue(id, out level) ? level : 0; }
        private static bool TryGetExactItemTechLevel(string id, out int level) { return levels.TryGetValue(id, out level); }
        private static string ResolveStaticRelationItemId(string id) { return id; }
        private static bool? IsProductionItemUnlocked(string id) { bool value; return learned.TryGetValue(id, out value) ? (bool?)value : null; }
        private static void Check(bool condition, string label) { checks++; if (!condition) throw new Exception("Item knowledge: " + label); }
        private static void Put(string id, object record) { KnownItemIds.Add(id); ItemRecordsById[id] = record; }
        private static void Chip(string id, int type, params string[] pool)
        {
            AnalyzeDatadiskGraph(id, new List<object> { new DatadiskRecord { UnlockType = type, UnlockIds = new List<string>(pool) } });
        }
        private static void Chance(string chip, string item, int hits, int total)
        {
            int actualHits, actualTotal; float percent;
            Check(TryGetDatadiskUnlockChance(chip, item, out actualHits, out actualTotal, out percent) &&
                actualHits == hits && actualTotal == total && Math.Abs(percent - 100f * hits / total) < 0.001f, "original chip pool probability " + item);
        }
        private static bool Search(string item, string query)
        {
            int score;
            return BrowserAdvancedSearchMatches(item, NormalizeBrowserSearchText("Model Orion"), NormalizeBrowserSearchText(item), ParseBrowserAdvancedSearchQuery(query), out score);
        }
        public static int[] RunItemKnowledgeCases()
        {
            Put("chest", new CompositeItemRecord(new ArmorRecord())); Put("helmet", new HelmetRecord());
            Put("legs", new LeggingsRecord()); Put("boots", new BootsRecord());
            Put("unrelated", new ArmorRecord()); Put("standalone", new ArmorRecord()); Put("ammo", new object()); Put("grenade", new object());
            Data.ArmorSets.Records.Add(new ArmorSetRecord { Items = new List<string> { "chest", "helmet", "legs", "boots", "boots", "missing" } });
            Data.ArmorSets.Records.Add(new ArmorSetRecord { Items = new List<string> { "helmet", "unrelated" } });
            _chipUnlockChanceContractVerified = true;
            Chip("armor_chip", 0, "chest", "chest", "standalone");
            foreach (string id in new string[] { "chest", "helmet", "legs", "boots" })
            {
                Check(GetPrimaryDatadiskForItem(id) == "armor_chip", "all set pieces link to the chip: " + id);
                Chance("armor_chip", id, 2, 3);
            }
            Check(!DatadisksByUnlockedItem.ContainsKey("missing"), "unknown set members excluded");
            Check(GetDatadiskUnlockOutputs("chest").Count == 4, "set members deduplicated");
            Check(!GetDatadiskUnlockOutputs("helmet").Contains("unrelated"), "first matching set follows vanilla");
            learned["boots"] = true; learned["helmet"] = false;
            Check(GetDatadiskStatus("boots", "armor_chip") == 1 && GetDatadiskStatus("helmet", "armor_chip") == -1, "each recipe keeps its own learned state");
            Check(GetDatadiskStatus("legs", "armor_chip") == 2, "unknown learned state stays neutral");
            Put("gun", new CompositeItemRecord(new WeaponRecord { WeaponClass = WeaponClass.Pistol, RequiredAmmo = "Bullets", DefaultAmmoId = "ammo" }));
            Put("launcher", new WeaponRecord { WeaponClass = WeaponClass.GrenadeLauncher, DefaultGrenadeId = "grenade", DefaultAmmoId = "ammo" });
            Put("implicit", new WeaponRecord { WeaponClass = WeaponClass.Axe, DefaultAmmoId = "ammo" });
            Chip("gun_chip", 0, "gun", "ammo", "launcher", "implicit", "unknown");
            Chance("gun_chip", "ammo", 2, 5); Chance("gun_chip", "grenade", 1, 5);
            Chance("gun_chip", "gun", 1, 5); Chance("gun_chip", "launcher", 1, 5);
            Check(GetDatadiskUnlockOutputs("implicit").Count == 1, "implicit payload is not a separate recipe unlock");
            Check(!GetDatadiskUnlockOutputs("launcher").Contains("ammo"), "launcher unlocks its default grenade only");
            Check(!DatadisksByUnlockedItem.ContainsKey("unknown"), "unknown raw entry keeps weight without a false link");
            Chip("mercenary", 1, "chest"); Check(!ItemsUnlockedByDatadisk.ContainsKey("mercenary"), "non-production chips excluded");
            _chipUnlockChanceContractVerified = false;
            int h, t; float p;
            Check(!TryGetDatadiskUnlockChance("armor_chip", "boots", out h, out t, out p), "unverified probability fails closed");
            Data.ArmorSets.Records.Clear();
            Data.ArmorSets.Records.Add(new ArmorSetRecord { Items = new List<string> { "chest", "unrelated" } });
            ResetDatadiskUnlockExpansion();
            Check(GetDatadiskUnlockOutputs("chest").Count == 2 && !GetDatadiskUnlockOutputs("chest").Contains("boots"), "reset drops stale armor expansion");
            int chipChecks = checks;

            Put("odd_model", new CompositeItemRecord(new WeaponRecord { WeaponClass = WeaponClass.AssaultRifle, Traits = new List<string> { "suppressor", "laser_sight" } }));
            Put("axe_model", new WeaponRecord { WeaponClass = WeaponClass.Axe });
            Put("not_a_weapon", new object()); levels["odd_model"] = 6;
            Put("throwing_knife", new ThrowingWeaponRecord { Traits = new List<string> { "suppressor" } });
            BrowserCatalogCategoryByItem["odd_model"] = 1;
            foreach (string id in new string[] { "odd_model", "axe_model", "not_a_weapon" }) IndexBrowserAdvancedSearchMetadata(id);
            IndexBrowserAdvancedSearchMetadata("throwing_knife");
            Check(Search("throwing_knife", "weapon:ThrowingWeapon perk:suppressor"), "throwing weapon classes and traits are indexed");
            Check(Search("odd_model", "Assault Rifle"), "plain class search does not need an item-ID keyword");
            Check(Search("odd_model", "suppressor") && Search("odd_model", "laser sight"), "plain translated perk search");
            Check(Search("odd_model", "Orion suppressor"), "plain name and perk combine");
            Check(Search("odd_model", "weapon:AssaultRifle perk:suppressor tech:>=5"), "class perk and tech combine");
            Check(Search("odd_model", "class:\"Assault Rifle\" trait:\"Laser Sight\""), "quoted aliases parse correctly");
            Check(Search("odd_model", "type:WeaponRecord") || Search("odd_model", "type:CompositeItemRecord"), "record type search remains available");
            Check(Search("odd_model", "type:AssaultRifle"), "type also accepts weapon classes");
            Check(Search("odd_model", "id:odd name:Orion cat:weapon"), "existing field filters remain available");
            Check(!Search("odd_model", "weapon:Pistol") && !Search("odd_model", "perk:cleave"), "unrelated classes and perks excluded");
            Check(!Search("not_a_weapon", "perk:suppressor") && !Search("axe_model", "perk:suppressor"), "perk metadata cannot leak to other items");
            Check(!Search("odd_model", "perk:suppressor perk:cleave"), "all requested perks are required");
            Check(!Search("odd_model", "name:suppressor") && !Search("odd_model", "id:suppressor"), "name and ID operators keep their scope");
            Check(!Search("odd_model", "weapon:AssaultRifle tech:no"), "invalid numeric filter fails closed");
            language = "zh"; ResetBrowserAdvancedSearchIndexState();
            Check(BrowserWeaponPerkTerms.Count == 0 && BrowserWeaponClassTerms.Count == 0, "language reset clears weapon search metadata");
            IndexBrowserAdvancedSearchMetadata("odd_model");
            IndexBrowserAdvancedSearchMetadata("axe_model");
            Check(Search("odd_model", "突击步枪") && Search("odd_model", "消音器"), "Chinese class and perk names are searchable");
            Check(Search("odd_model", "weapon:突击步枪 perk:消音器") && Search("odd_model", "突击步枪 激光瞄准器"), "Chinese filters combine");
            Check(Search("odd_model", "perk:laser_sight"), "stable perk IDs remain available in another language");
            _browserCatalogCategory = 1; _browserCatalogWeaponClass = "AssaultRifle";
            Check(BrowserCatalogItemMatchesWeaponClass("odd_model") && !BrowserCatalogItemMatchesWeaponClass("axe_model"), "catalog class comes from records");
            Check(!BrowserCatalogItemMatchesWeaponClass("not_a_weapon"), "missing weapon class cannot pass the filter");
            _browserCatalogCategory = 2; Check(BrowserCatalogItemMatchesWeaponClass("not_a_weapon"), "weapon filter does not leak to other categories");
            _browserCatalogCategory = 0; Check(BrowserCatalogItemMatchesWeaponClass("not_a_weapon"), "all-items category remains unfiltered");
            _browserCatalogCategory = 1; _browserCatalogWeaponClass = string.Empty;
            Check(BrowserCatalogItemMatchesWeaponClass("axe_model"), "all weapon types clears the filter");
            return new int[] { chipChecks, checks - chipChecks };
        }
    }
}
