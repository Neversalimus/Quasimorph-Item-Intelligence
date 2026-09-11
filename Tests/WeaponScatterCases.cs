namespace MGSC
{
    public class WeaponRecord { public bool IsMelee; public float BonusScatterAngle; }
    public class BasePickupItem
    {
        public string Id;
        public WeaponRecord Weapon;
        public T Record<T>() where T : class { return Weapon as T; }
    }
    public class ItemsCollection { public T GetSimpleRecord<T>(string id, bool quiet) where T : class { return null; } }
    public static class Data { public static ItemsCollection Items = new ItemsCollection(); }
    public class CreatureData
    {
        public float Perk = 0.8f, Augment = -0.25f;
        public bool ThrowAugment;
        public int AugmentCalls;
        public WeaponRecord LastWeapon;
        public float GetScatterAngleMult(WeaponRecord record) { LastWeapon = record; return Perk; }
#if MODERN
        public float GetAugmentScatterAngleMult()
        {
            AugmentCalls++;
            if (ThrowAugment) throw new InvalidOperationException("fixture: unavailable effects");
            return Augment;
        }
#elif INVALID
        public double GetAugmentScatterAngleMult() { return Augment; }
#endif
    }
    public class Mercenary { public CreatureData CreatureData; }
    public class Player { public Mercenary Mercenary; }
    public class Creatures { public Player Player; }
}
namespace UnityEngine
{
    public static class Mathf
    {
        public const float Epsilon = float.Epsilon;
        public static float Abs(float value) { return Math.Abs(value); }
        public static float Max(float a, float b) { return Math.Max(a, b); }
    }
    public static class Debug { public static void Log(string value) { } }
}
namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private class WeaponModeStaticStats { public float? ScatterAngle; public int AmmoPerShot = 1, WeaponCastsCount = 1; }
        private static readonly Dictionary<string, string> WeaponModeItemIdByKey = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> WeaponModeRawIdByKey = new Dictionary<string, string>();
        private static BasePickupItem _browserPreviewLiveItem;
        private static readonly Creatures FixtureCreatures = new Creatures();
        private static bool ModderMode = true;
        private static void ResetWeaponModeDamagePerApCache() { }
        private static string ResolveStaticRelationItemId(string id) { return id; }
        private static object ResolveStateModule(Type type) { return FixtureCreatures; }
        private static object GetMember(object obj, string name) { return ((Creatures)obj).Player; }
        private static CultureInfo GetUiNumberCulture() { return CultureInfo.InvariantCulture; }
        private static int checks;
        private static void AssertScatter(bool pass, string label)
        { checks++; if (!pass) throw new Exception("Scatter regression: " + label); }
        private static void NearScatter(float actual, float expected, string label)
        { AssertScatter(!float.IsNaN(actual) && Math.Abs(actual - expected) < 0.00001f, label); }
        public static int RunScatterCases()
        {
            ResetWeaponModeScatterCache();
            WeaponRecord weapon = new WeaponRecord { BonusScatterAngle = 2f };
            _browserPreviewLiveItem = new BasePickupItem { Id = "target", Weapon = weapon };
            WeaponModeItemIdByKey["mode"] = "target";
            CreatureData data = new CreatureData();
            FixtureCreatures.Player = new Player { Mercenary = new Mercenary { CreatureData = data } };
            WeaponModeStaticStats stats = new WeaponModeStaticStats { ScatterAngle = 10f };
            float scatter;
#if INVALID
            AssertScatter(!TryCalculateVanillaFiremodeScatter("mode", stats, out scatter), "changed return type hides value");
            AssertScatter(!TryCalculateVanillaFiremodeScatter("mode", stats, out scatter), "cached invalid API stays unavailable");
#else
            AssertScatter(TryCalculateVanillaFiremodeScatter("mode", stats, out scatter), "hover scatter available");
#if MODERN
            NearScatter(scatter, 6.6f, "augmentation term is additive to perks, not multiplied");
            AssertScatter(data.AugmentCalls == 1, "uses current vanilla augmentation getter");
            data.Augment = 0.5f;
            AssertScatter(TryCalculateVanillaFiremodeScatter("mode", stats, out scatter), "live effect change available");
            NearScatter(scatter, 15.6f, "effect value is not cached across hovers");
            data.Augment = -2f;
            AssertScatter(!TryCalculateVanillaFiremodeScatter("mode", stats, out scatter), "vanilla omits zero scatter row");
            NearScatter(scatter, 0f, "negative combined multiplier is clamped");
            data.Augment = float.NaN;
            AssertScatter(!TryCalculateVanillaFiremodeScatter("mode", stats, out scatter), "invalid augmentation value is not neutralized");
            data.Augment = -0.25f;
            data.ThrowAugment = true;
            AssertScatter(!TryCalculateVanillaFiremodeScatter("mode", stats, out scatter), "unavailable effect getter hides value");
            data.ThrowAugment = false;
#else
            NearScatter(scatter, 9.6f, "older DLL uses its original perk-only formula");
            AssertScatter(data.AugmentCalls == 0, "absent API is not required on older DLL");
#endif
            AssertScatter(object.ReferenceEquals(data.LastWeapon, weapon), "multiplier uses inspected weapon");
            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                data.Perk = invalid;
                AssertScatter(!TryCalculateVanillaFiremodeScatter("mode", stats, out scatter), "invalid perk multiplier rejected");
                data.Perk = 1f; weapon.BonusScatterAngle = invalid;
                AssertScatter(!TryCalculateVanillaFiremodeScatter("mode", stats, out scatter), "invalid weapon bonus rejected");
                weapon.BonusScatterAngle = 2f; stats.ScatterAngle = invalid;
                AssertScatter(!TryCalculateVanillaFiremodeScatter("mode", stats, out scatter), "invalid mode scatter rejected");
                stats.ScatterAngle = 10f;
            }
            data.Perk = float.MaxValue;
            AssertScatter(!TryCalculateVanillaFiremodeScatter("mode", stats, out scatter), "float overflow rejected before display");
            FixtureCreatures.Player = null;
            AssertScatter(TryCalculateVanillaFiremodeScatter("mode", stats, out scatter), "catalog without mercenary uses neutral context");
            NearScatter(scatter, 12f, "neutral mode plus weapon bonus");
            weapon.IsMelee = true;
            AssertScatter(!TryCalculateVanillaFiremodeScatter("mode", stats, out scatter), "melee omits firearm scatter");
            weapon.IsMelee = false; stats.ScatterAngle = null;
            AssertScatter(!TryCalculateVanillaFiremodeScatter("mode", stats, out scatter), "missing stat stays unavailable");
            AssertScatter(!TryCalculateVanillaFiremodeScatter("mode", null, out scatter), "missing mode stays unavailable");
            AssertScatter(!TryCalculateVanillaFiremodeScatter("absent", new WeaponModeStaticStats { ScatterAngle = 10f }, out scatter), "missing weapon stays unavailable");
#endif
            return checks;
        }
    }
}
