// Minimal record shapes for deterministic pool tests, not a simulation of Unity.
// Game algorithm compatibility is established separately from the supplied game IL.
namespace MGSC
{
    internal class BasePickupItemRecord { public string Id; }
    internal class ItemRecord : BasePickupItemRecord
    {
        public int TechLevel;
        public List<string> Categories;
    }
    internal class CompositeItemRecord : BasePickupItemRecord
    {
        public ItemRecord PrimaryRecord;
        private readonly object[] components;
        public CompositeItemRecord(string id, ItemRecord primary, params object[] records)
        {
            Id = id;
            PrimaryRecord = primary;
            components = records;
        }
        public T GetRecord<T>() where T : class
        {
            foreach (object value in components) if (value is T) return value as T;
            return null;
        }
    }
    internal sealed class TrashRecord { }
    internal sealed class WeaponRecord { }
    internal sealed class HelmetRecord { }
    internal sealed class ArmorRecord { }
    internal sealed class LeggingsRecord { }
    internal sealed class BootsRecord { }
    internal sealed class ConsumableRecord { }
    internal sealed class FixationMedicineRecord { }
    internal sealed class AmmoRecord { }
    internal sealed class GrenadeRecord { }
    internal sealed class ItemTable
    {
        public readonly List<BasePickupItemRecord> Records = new List<BasePickupItemRecord>();
    }
    internal static class Data { public static readonly ItemTable Items = new ItemTable(); }
}
