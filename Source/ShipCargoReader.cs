using System;
using System.Collections.Generic;
using MGSC;

namespace ItemIntelligence
{
    internal static class ShipCargoReader
    {
        internal static bool TryGetCount(MagnumCargo cargo, string itemId, out int count)
        {
            count = 0;
            if (cargo == null || cargo.ShipCargo == null || string.IsNullOrEmpty(itemId)) return false;

            try
            {
                // Match the storage scope of GetAvailableCargoStorages without its
                // temporary list. CountPlayerItems also includes clone inventories,
                // so it cannot answer how much loot remains aboard the ship.
                long total = 0;
                List<ItemStorage> storages = cargo.ShipCargo;
                for (int i = 0; i < storages.Count; i++)
                {
                    ItemStorage storage = storages[i];
                    if (storage == null) return false;
                    if (!ContainsStorage(storages, storage, i) && !TryAddStorage(storage, itemId, ref total))
                        return false;
                }

                ItemStorage fridge = cargo.FridgeStorage;
                if (fridge != null && !ContainsStorage(storages, fridge, storages.Count) &&
                    !TryAddStorage(fridge, itemId, ref total)) return false;

                ItemStorage recycling = cargo.RecyclingStorage;
                if (!cargo.RecyclingInProgress && recycling != null &&
                    !ReferenceEquals(recycling, fridge) && !ContainsStorage(storages, recycling, storages.Count) &&
                    !TryAddStorage(recycling, itemId, ref total)) return false;

                count = (int)total;
                return true;
            }
            catch
            {
                // Missing/broken save data is unknown, never an empty warehouse.
                return false;
            }
        }

        private static bool ContainsStorage(List<ItemStorage> storages, ItemStorage target, int end)
        {
            for (int i = 0; i < end; i++)
                if (ReferenceEquals(storages[i], target)) return true;
            return false;
        }

        private static bool TryAddStorage(ItemStorage storage, string itemId, ref long total)
        {
            // The native counter sums stack quantities and compares exact item IDs.
            // In particular, upgraded equipment must not be merged with its base item.
            int stored = storage.CountItems(itemId);
            if (stored < 0) return false;
            total += stored;
            return total <= int.MaxValue;
        }
    }
}
