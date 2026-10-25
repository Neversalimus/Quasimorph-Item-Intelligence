namespace MGSC
{
    public enum WeaponClass { Default, GrenadeLauncher }
    public sealed class WeaponRecord
    {
        public string Id, DefaultAmmoId, DefaultGrenadeId;
        public bool IsMelee;
        public WeaponClass WeaponClass;
        public List<string> Firemodes = new List<string>(), OverrideAmmo = new List<string>(), AllowedGrenadeIds = new List<string>();
        public DmgInfo Damage;
    }
    public struct DmgInfo { public string damage; }
    public sealed class AmmoRecord { public string DmgType, StatusEffectId; public List<string> Traits = new List<string>(); }
    public sealed class GrenadeRecord { public string Explosion; }
    public sealed class AugmentationRecord { public List<string> WoundSlotIds = new List<string>(); }
    public sealed class WoundSlotRecord { public string BareHandWeapon; }
    public struct ExplosionParameters { public bool GainDmgToCreature; public string DamageType; }
    public sealed class ExplosionRecord { public ExplosionParameters Parameters; }
    public sealed class Records<T> where T : class
    {
        public readonly Dictionary<string, T> Values = new Dictionary<string, T>();
        public T GetRecord(string id, bool optional) { T value; return Values.TryGetValue(id, out value) ? value : null; }
    }
    public sealed class Items
    {
        private readonly Dictionary<Type, Dictionary<string, object>> records = new Dictionary<Type, Dictionary<string, object>>();
        public void Add<T>(string id, T value) { Dictionary<string, object> rows; if (!records.TryGetValue(typeof(T), out rows)) records[typeof(T)] = rows = new Dictionary<string, object>(); rows[id] = value; }
        public T GetSimpleRecord<T>(string id, bool optional) where T : class { Dictionary<string, object> rows; object value; return records.TryGetValue(typeof(T), out rows) && rows.TryGetValue(id, out value) ? value as T : null; }
    }
    public static class Data
    {
        public static Items Items = new Items();
        public static Records<WoundSlotRecord> WoundSlots = new Records<WoundSlotRecord>();
        public static Records<ExplosionRecord> Explosions = new Records<ExplosionRecord>();
    }
}

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static int checks;
        private static bool ShowAmmoRelations = true, _compatAmmo = true, _ammoWarmupComplete = true;
        private static readonly HashSet<string> KnownItemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, WeaponInfo> WeaponsByItem = new Dictionary<string, WeaponInfo>();
        private static readonly Dictionary<string, List<string>> CompatibleWeaponsByAmmo = new Dictionary<string, List<string>>();
        private static readonly Dictionary<string, string> WeaponModeRawIdByKey = new Dictionary<string, string>(), WeaponModeItemIdByKey = new Dictionary<string, string>();
        private static readonly List<string> AugmentationIds = new List<string>();
        private static readonly List<BrowserLine> BrowserLines = new List<BrowserLine>();
        private sealed class WeaponModeStaticStats { }
        private sealed class BrowserLine
        {
            public string Id;
            public static BrowserLine Section(string id) { return new BrowserLine { Id = id }; }
        }
        private static BrowserLine ItemWithProductionChip(string id, string right) { return new BrowserLine { Id = id }; }
        private static string ResolveStaticRelationItemId(string id) { return id; }
        private static object GetMember(object o, string name) { if (o == null) return null; var type = o.GetType(); var p = type.GetProperty(name); if (p != null) return p.GetValue(o, null); var f = type.GetField(name); return f == null ? null : f.GetValue(o); }
        private static string GetStringMember(object o, string name) { return Convert.ToString(GetMember(o, name)) ?? ""; }
        private static string GetItemIdDeep(object o, int depth) { return o as string ?? ""; }
        private static string FirstNonEmpty(params string[] values) { return values.FirstOrDefault(v => !string.IsNullOrEmpty(v)) ?? ""; }
        private static string Ui(string key) { return key; }
        private static string NormalizeGameText(string text) { return text; }
        private static string LocalizeCandidates(string[] keys, string fallback)
        {
            if (keys.Contains("trait.wounding")) return "致伤";
            if (keys.Contains("tooltip.statuseffect_poisonEffect")) return "中毒";
            return fallback;
        }
        private static WeaponRecord ResolveWeaponModeWeaponRecord(string key)
        {
            string id;
            return WeaponModeItemIdByKey.TryGetValue(key, out id) ? Data.Items.GetSimpleRecord<WeaponRecord>(id, true) : null;
        }
        private static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
        private static List<string> Split(string value) { return (value ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList(); }
        public static void ResetFixture()
        {
            Data.Items = new Items(); Data.WoundSlots = new Records<WoundSlotRecord>(); Data.Explosions = new Records<ExplosionRecord>();
            KnownItemIds.Clear(); WeaponsByItem.Clear(); CompatibleWeaponsByAmmo.Clear(); WeaponModeRawIdByKey.Clear(); WeaponModeItemIdByKey.Clear();
            ResetArmWeaponLinks(); ResetWeaponModeDamageTypeCache(); AugmentationIds.Clear(); BrowserLines.Clear(); checks = 0;
        }
        public static void AddAmmo(string id, string damage, string traits, string status)
        {
            KnownItemIds.Add(id); Data.Items.Add(id, new AmmoRecord { DmgType = damage, Traits = Split(traits), StatusEffectId = status });
        }
        public static void AddWeapon(string id, bool melee, string modes, string overrides, string ammo, string grenade, string allowed, bool launcher)
        {
            var weapon = new WeaponRecord { Id = id, IsMelee = melee, Firemodes = Split(modes), OverrideAmmo = Split(overrides), DefaultAmmoId = ammo, DefaultGrenadeId = grenade, AllowedGrenadeIds = Split(allowed), WeaponClass = launcher ? WeaponClass.GrenadeLauncher : WeaponClass.Default };
            KnownItemIds.Add(id); Data.Items.Add(id, weapon); WeaponsByItem[id] = new WeaponInfo(id, null, null, null);
            foreach (string mode in weapon.Firemodes) { string key = id + ":" + mode; WeaponModeRawIdByKey[key] = mode; WeaponModeItemIdByKey[key] = id; }
        }
        public static void AddGrenade(string id, string explosion) { KnownItemIds.Add(id); Data.Items.Add(id, new GrenadeRecord { Explosion = explosion }); }
        public static void AddExplosion(string id, string damage, bool dealsDamage) { Data.Explosions.Values[id] = new ExplosionRecord { Parameters = new ExplosionParameters { DamageType = damage, GainDmgToCreature = dealsDamage } }; }
        public static void AddSlot(string id, string weapon) { Data.WoundSlots.Values[id] = new WoundSlotRecord { BareHandWeapon = weapon }; }
        public static void AddAugmentation(string id, string slots) { KnownItemIds.Add(id); Data.Items.Add(id, new AugmentationRecord { WoundSlotIds = Split(slots) }); AugmentationIds.Add(id); }
        private static void AssertMode(string id, string mode, string expectedAmmo, string expectedDamage, string expectedTrait)
        {
            string key = id + ":" + mode;
            var weapon = Data.Items.GetSimpleRecord<WeaponRecord>(id, true);
            Check(ResolveWeaponModeAmmoId(key, weapon) == expectedAmmo, id + " / " + mode + " ammo");
            string label, value;
            Check(TryResolveWeaponModeDamageType(key, out label, out value) && value == Ui("ui.damage_type." + expectedDamage), id + " / " + mode + " damage type");
            var rows = new List<KeyValuePair<string, string>>(); AppendWeaponModePayloadEffectRows(key, rows);
            Check(expectedTrait == "" ? rows.Any(r => r.Value == Ui("ui.mode_no_effects")) : rows.Any(r => r.Key == expectedTrait), id + " / " + mode + " effects");
        }
        public static int RunWeaponKnowledgeCases()
        {
            ResetFixture();
            AddAmmo("implicted_heavy_axe", "lacer", "", ""); AddAmmo("implicted_axe_brutal", "lacer", "wounding", "");
            AddAmmo("implicted_sword_brutal", "lacer", "wounding", ""); AddAmmo("implicted_combat_knife", "pierce", "", "");
            foreach (string id in new[] { "prison_axe_1", "venus_axe_1", "venus_poleaxe_1" })
            {
                AddWeapon(id, true, "chop_strong_1 chop_precise_1", "none implicted_axe_brutal", "implicted_heavy_axe", "", "", false);
                AssertMode(id, "chop_strong_1", "implicted_heavy_axe", "lacer", ""); AssertMode(id, "chop_precise_1", "implicted_axe_brutal", "lacer", "致伤");
            }
            AddWeapon("scrivnus_blade_1", true, "slash_2 stab_2", "none implicted_combat_knife", "implicted_sword_brutal", "", "", false);
            AssertMode("scrivnus_blade_1", "slash_2", "implicted_sword_brutal", "lacer", "致伤"); AssertMode("scrivnus_blade_1", "stab_2", "implicted_combat_knife", "pierce", "");
            AddAmmo("poison", "poison", "toxic", "poisonEffect"); AddWeapon("poison_blade", true, "claw_1", "", "poison", "", "", false);
            var effects = new List<KeyValuePair<string, string>>(); AppendWeaponModePayloadEffectRows("poison_blade:claw_1", effects);
            Check(effects.Any(r => r.Key == "中毒"), "status uses the game's tooltip localization key");
            AddWeapon("grenade_launcher_1", false, "launcher_1", "", "rifle_basic_ammo", "frag_grenade", "frag_grenade thermal_grenade", true);
            AddWeapon("nature_mars_grenade_launcher", false, "launcher_1", "", "rifle_basic_ammo", "urparp_grenade", "", true);
            AddWeapon("rifle", false, "rifle_1", "", "rifle_basic_ammo", "", "", false);
            AddAmmo("rifle_basic_ammo", "pierce", "wounding", ""); AddGrenade("frag_grenade", "frag_explosion"); AddGrenade("thermal_grenade", "fire_explosion"); AddGrenade("urparp_grenade", "urparp_explosion");
            AddExplosion("frag_explosion", "explosion", true); AddExplosion("urparp_explosion", "plasma", true);
            foreach (var info in WeaponsByItem.Values) { info.CompatibleAmmo.Add("rifle_basic_ammo"); AddToList(CompatibleWeaponsByAmmo, "rifle_basic_ammo", info.ItemId); }
            CorrectExplicitGrenadeLauncherRelations(); CorrectExplicitGrenadeLauncherRelations();
            Check(WeaponsByItem["grenade_launcher_1"].CompatibleAmmo.Count == 2, "Shaker's exact grenade allowlist");
            Check(!CompatibleWeaponsByAmmo["rifle_basic_ammo"].Contains("grenade_launcher_1") && CompatibleWeaponsByAmmo["rifle_basic_ammo"].Contains("rifle"), "both relation directions repaired, rifle preserved");
            Check(CompatibleWeaponsByAmmo["frag_grenade"].Count == 1, "rebuild does not duplicate reverse links");
            string label, value;
            Check(TryResolveWeaponModeDamageType("grenade_launcher_1:launcher_1", out label, out value) && value == "ui.damage_type.explosion", "Shaker type comes from grenade explosion");
            Check(TryResolveWeaponModeDamageType("nature_mars_grenade_launcher:launcher_1", out label, out value) && value == "ui.damage_type.plasma", "second launcher uses its own grenade");
            effects.Clear(); AppendWeaponModePayloadEffectRows("grenade_launcher_1:launcher_1", effects); Check(effects.Count == 0, "launcher cannot inherit rifle traits");
            AddWeapon("empty_launcher", false, "launcher_1", "", "rifle_basic_ammo", "", "", true); WeaponsByItem["empty_launcher"].CompatibleAmmo.Add("rifle_basic_ammo");
            CorrectExplicitGrenadeLauncherRelations(); Check(WeaponsByItem["empty_launcher"].CompatibleAmmo.Count == 0, "unresolved launcher fails closed");
            AddWeapon("nature_cyborg_fist", true, "punch_1", "", "implicted_combat_knife", "", "", false);
            AddSlot("CyborgArm", "nature_cyborg_fist"); AddSlot("CyborgShoulder", ""); AddSlot("CyborgArmDuplicate", "nature_cyborg_fist"); AddSlot("BadArm", "missing_weapon");
            AddAugmentation("cyborg_wrist", "CyborgArm CyborgShoulder CyborgArmDuplicate BadArm MissingSlot"); AddAugmentation("cyborg_hand", "CyborgShoulder CyborgArm");
            foreach (string id in AugmentationIds) IndexArmWeaponLinks(id);
            Check(ArmWeaponsByAugmentation["cyborg_wrist"].SequenceEqual(new[] { "nature_cyborg_fist" }), "slot-based link rejects invalid ids and duplicates");
            Check(AugmentationsByArmWeapon["nature_cyborg_fist"].Count == 2, "one installed weapon can have multiple augmentation items");
            AppendOverviewArmWeaponLinks("nature_cyborg_fist"); Check(BrowserLines.Select(r => r.Id).Contains("cyborg_wrist") && BrowserLines.Select(r => r.Id).Contains("cyborg_hand"), "reverse links are clickable item rows");
            ResetArmWeaponLinks(); Check(ArmWeaponsByAugmentation.Count == 0 && AugmentationsByArmWeapon.Count == 0, "session reset removes both indexes");
            return checks;
        }
        public static string RunAuditedTableCases()
        {
            int modeCount = 0;
            foreach (var info in WeaponsByItem.Values)
            {
                var record = Data.Items.GetSimpleRecord<WeaponRecord>(info.ItemId, true);
                for (int i = 0; i < record.Firemodes.Count; i++)
                {
                    string mode = record.Firemodes[i], key = info.ItemId + ":" + mode;
                    string expected = i < record.OverrideAmmo.Count && record.OverrideAmmo[i] != "none" ? record.OverrideAmmo[i] : record.DefaultAmmoId;
                    if (IsGrenadeLauncherRecord(record)) { Check(ResolveWeaponModeAmmoId(key, record) == "", key + " launcher placeholder rejected"); continue; }
                    Check(ResolveWeaponModeAmmoId(key, record) == expected, key + " exact slot mapping");
                    var ammo = Data.Items.GetSimpleRecord<AmmoRecord>(expected, true);
                    if (ammo == null) continue;
                    string label, value;
                    Check(TryResolveWeaponModeDamageType(key, out label, out value) && value == ResolveWeaponModeDamageTypeDisplay(ammo.DmgType), key + " damage element");
                    var rows = new List<KeyValuePair<string, string>>(); AppendWeaponModePayloadEffectRows(key, rows);
                    foreach (string trait in ammo.Traits) Check(rows.Any(r => r.Key == LocalizeWeaponModePayloadTrait(trait)), key + " trait " + trait);
                    modeCount++;
                }
            }
            foreach (string id in AugmentationIds) IndexArmWeaponLinks(id);
            foreach (string id in AugmentationIds)
            {
                var augmentation = Data.Items.GetSimpleRecord<AugmentationRecord>(id, true);
                var expected = new HashSet<string>();
                foreach (string slotId in augmentation.WoundSlotIds)
                {
                    var slot = Data.WoundSlots.GetRecord(slotId, true);
                    if (slot != null && !string.IsNullOrEmpty(slot.BareHandWeapon) &&
                        Data.Items.GetSimpleRecord<WeaponRecord>(slot.BareHandWeapon, true) != null)
                        expected.Add(slot.BareHandWeapon);
                }
                List<string> actual;
                ArmWeaponsByAugmentation.TryGetValue(id, out actual);
                Check(expected.SetEquals(actual ?? new List<string>()), id + " all exact installed-slot weapons");
            }
            foreach (var pair in ArmWeaponsByAugmentation) foreach (string weapon in pair.Value)
                Check(AugmentationsByArmWeapon[weapon].Contains(pair.Key), pair.Key + " bidirectional slot link");
            CorrectExplicitGrenadeLauncherRelations();
            foreach (var info in WeaponsByItem.Values)
            {
                var record = Data.Items.GetSimpleRecord<WeaponRecord>(info.ItemId, true);
                if (!IsGrenadeLauncherRecord(record)) continue;
                Check(!info.CompatibleAmmo.Contains(record.DefaultAmmoId), info.ItemId + " no rifle placeholder");
                foreach (string grenade in info.CompatibleAmmo) Check(Data.Items.GetSimpleRecord<GrenadeRecord>(grenade, true) != null, info.ItemId + " actual grenade item");
            }
            return WeaponsByItem.Count + " weapons, " + modeCount + " ammo-backed modes, " + ArmWeaponsByAugmentation.Count + " linked augmentations, " + checks + " assertions";
        }
    }
}
