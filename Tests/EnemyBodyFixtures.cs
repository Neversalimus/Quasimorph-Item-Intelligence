namespace MGSC
{
    public enum AugmentationClass { None, Quasi, Human }
    public class BasePickupItemRecord { public string Id; }
    public class ItemRecord : BasePickupItemRecord
    { public int TechLevel; public List<string> Categories = new List<string>(); }
    public class ImplantRecord : ItemRecord
    { public string SlotType; public List<string> NatureTypes; public AugmentationClass AugmentationClass; }
    public class AugmentationRecord : ItemRecord
    { public List<string> WoundSlotIds; public AugmentationClass AugmentationClass; }
    public class CompositeItemRecord : BasePickupItemRecord
    {
        public ItemRecord PrimaryRecord;
        public T GetRecord<T>() where T : class { return PrimaryRecord as T; }
    }
    public class WoundSlotRecord : BasePickupItemRecord
    { public string SlotType, NatureType; public int ImplantSocketsDefault, ImplantSocketsMin, ImplantSocketsMax; }
    public class BodyTypeRecord : BasePickupItemRecord { public List<string> WoundSlots; }
    public struct IntRange { public int Min, Max; }
    public class MobClassRecord
    {
        public List<string> BodyTypes = new List<string>();
        public List<string> GrantedAugmentations = new List<string>();
        public List<string> GrantedImplants = new List<string>();
        public Dictionary<AugmentationClass, float> AugmentationClasses = new Dictionary<AugmentationClass, float>();
        public Dictionary<AugmentationClass, float> ImplantClasses = new Dictionary<AugmentationClass, float>();
        public Dictionary<string, float> ItemCategoriesWhitelist;
        public IntRange AugCount, ImplantCount;
    }
    public class Records<T> where T : BasePickupItemRecord
    {
        public List<T> Values = new List<T>();
        public List<T> RecordsList { get { return Values; } }
        public T GetRecord(string id, bool ignoreLog) { return Values.Find(x => x.Id == id); }
    }
    public class ItemRecords
    {
        public List<BasePickupItemRecord> Records = new List<BasePickupItemRecord>();
        public T GetSimpleRecord<T>(string id, bool ignoreLog) where T : class
        {
            foreach (BasePickupItemRecord record in Records)
            {
                CompositeItemRecord composite = record as CompositeItemRecord;
                if (composite == null) continue;
                if (record.Id == id || composite.PrimaryRecord.Id == id) return composite.GetRecord<T>();
            }
            return null;
        }
    }
    public static class Data
    {
        public static Records<WoundSlotRecord> WoundSlots = new Records<WoundSlotRecord>();
        public static Records<BodyTypeRecord> BodyTypes = new Records<BodyTypeRecord>();
        public static ItemRecords Items = new ItemRecords();
    }
}
namespace UnityEngine { public static class Debug { public static void Log(string message) {} } }
namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static bool _compatStaticChecked = true;
        private static string _compatAssemblySha256 = "BE78036434737521BB43DABBD934B579A08DC3E8370B834AEE36E40932F56FE0";
        private static void RunCompatibilityShieldStatic() {}
        private static string ConvertToStableString(object value) { return value == null ? "" : value.ToString(); }
        private static bool TryToDoubleSafe(object value, out double number)
        { try { number = Convert.ToDouble(value); return true; } catch { number = 0; return false; } }
        private static HashSet<string> KnownItemIds = new HashSet<string>(StringComparer.Ordinal);
        private static Dictionary<string, List<LootEnemySource>> LootEnemySourcesByItem =
            new Dictionary<string, List<LootEnemySource>>(StringComparer.Ordinal);
        private static List<string> ResolveLootExternalItemIds(string id) { return new List<string> { id }; }
        private static string Ui(string key) { return key; }
        private static string FormatLootPercent(float value) { return value.ToString("0.###", CultureInfo.InvariantCulture) + "%"; }
        private static double CorpseBonusAtLeastOnceChance(double value, double count) { return 0.0; }
    }
}
