namespace ItemIntelligence
{
    public static class ShipCargoMonitorCases
    {
        private static int assertions;
        private static void Check(bool condition, string message)
        {
            assertions++;
            if (!condition) throw new Exception("Ship cargo monitor: " + message);
        }

        public static int Run()
        {
            bool rejected = false;
            try { new ShipCargoMonitor(null); }
            catch (ArgumentNullException) { rejected = true; }
            Check(rejected, "a live reader is required");

            int reads = 0;
            int? stored = 7;
            string requested = null;
            var monitor = new ShipCargoMonitor(delegate(string id) { reads++; requested = id; return stored; });
            int? value;
            Check(!monitor.TryRefresh("medical", 100f, out value) && reads == 0 && !value.HasValue,
                "inactive monitor does no reads");
            Check(monitor.Begin("medical", 10f) == 7 && reads == 1 && requested == "medical",
                "inspection immediately reads the selected item");
            Check(monitor.ItemId == "medical" && monitor.Count == 7, "snapshot belongs to the selected item");
            Check(!monitor.TryRefresh("medical", 10.49f, out value) && reads == 1,
                "before the deadline no work is performed");
            Check(!monitor.TryRefresh("Medical", 11f, out value) && reads == 1,
                "another item cannot poll or delay the selected item");
            Check(!monitor.TryRefresh("medical", 10.5f, out value) && reads == 2 && value == 7,
                "unchanged stock does not request rendering");
            Check(!monitor.IsDue("medical", 10.99f), "unchanged reads still advance the deadline");
            stored = 0;
            Check(monitor.TryRefresh("medical", 11f, out value) && value == 0,
                "spending the last item produces a known zero");
            stored = null;
            Check(monitor.TryRefresh("medical", 11.5f, out value) && !value.HasValue && !monitor.Count.HasValue,
                "lost save data clears the previous stock");
            int beforeUnknown = reads;
            Check(!monitor.TryRefresh("medical", 12f, out value) && reads == beforeUnknown + 1,
                "unchanged unknown data does not request rendering");
            stored = 12;
            Check(monitor.TryRefresh("medical", 100f, out value) && value == 12,
                "a long pause reads current stock once");
            int afterPause = reads;
            Check(!monitor.TryRefresh("medical", 100f, out value) && reads == afterPause,
                "a long pause never causes catch-up reads");
            stored = 4;
            Check(monitor.Begin("ammo", 100f) == 4 && requested == "ammo",
                "switching items bypasses the old deadline and reads immediately");
            Check(!monitor.IsDue("medical", 1000f), "previous item cannot poll the replacement snapshot");
            monitor.Reset();
            Check(monitor.ItemId == null && !monitor.Count.HasValue, "reset releases selected item and count");
            int afterReset = reads;
            Check(!monitor.TryRefresh("ammo", 1000f, out value) && reads == afterReset && !value.HasValue,
                "closed inspection retains no readable snapshot");
            stored = 88;
            Check(monitor.Begin("ammo", 1f) == 88, "new session ignores the old clock and save snapshot");
            Check(!monitor.IsDue("ammo", 1.49f) && monitor.IsDue("ammo", 1.5f),
                "new session starts its own polling interval");

            var other = new ShipCargoMonitor(delegate(string id) { return 99; });
            other.Begin("food", 1f);
            Check(monitor.ItemId == "ammo" && monitor.Count == 88, "instances do not share selection or stock");
            monitor.Reset();
            Check(other.ItemId == "food" && other.Count == 99, "reset cannot affect another owner");
            return assertions;
        }
    }
}
