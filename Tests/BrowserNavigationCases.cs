namespace ItemIntelligence
{
    public static class BrowserNavigationCases
    {
        private static int assertions;
        private static void Check(bool condition, string message)
        {
            assertions++;
            if (!condition) throw new Exception("Browser navigation: " + message);
        }

        private static bool Back(BrowserNavigationSessionState navigation, Func<string, bool> lookup, out string itemId)
        {
            BrowserNavigationSnapshot snapshot;
            bool found = navigation.TryBack(lookup, out snapshot);
            itemId = found ? snapshot.ItemId : null;
            if (found) navigation.Restore(snapshot);
            return found;
        }

        public static int Run()
        {
            foreach (int invalid in new int[] { 0, -1 })
            {
                bool rejected = false;
                try { new BrowserNavigationSessionState(invalid, 64); }
                catch (ArgumentOutOfRangeException) { rejected = true; }
                Check(rejected, "tab storage requires a positive size");
                rejected = false;
                try { new BrowserNavigationSessionState(7, invalid); }
                catch (ArgumentOutOfRangeException) { rejected = true; }
                Check(rejected, "history requires a positive bound");
            }

            var navigation = new BrowserNavigationSessionState(7, 64);
            navigation.Begin(-100);
            Check(navigation.Tab == 0 && navigation.ScrollOffset == 0 && navigation.HistoryCount == 0,
                "fresh inspection starts at a valid tab with empty history");
            navigation.SetScrollOffset(-1);
            Check(navigation.ScrollOffset == 0, "negative scroll cannot enter session state");
            for (int tab = 0; tab < 7; tab++)
            {
                navigation.SwitchTab(tab);
                navigation.SetScrollOffset(tab + 10);
            }
            for (int tab = 0; tab < 7; tab++)
            {
                navigation.SwitchTab(tab);
                Check(navigation.ScrollOffset == tab + 10, "switching tabs restores each saved offset");
                Check(!navigation.SwitchTab(tab) && navigation.ScrollOffset == tab + 10,
                    "selecting the same tab preserves scroll");
            }
            Check(navigation.SwitchTab(int.MinValue) && navigation.Tab == 0, "tab selection clamps its lower bound");
            Check(navigation.SwitchTab(int.MaxValue) && navigation.Tab == 6, "tab selection clamps its upper bound");
            navigation.Push(null);
            navigation.Push("");
            Check(navigation.HistoryCount == 0, "empty item IDs do not enter history");
            navigation.Push("previous");
            navigation.StartItem(3);
            Check(navigation.Tab == 3 && navigation.ScrollOffset == 0 && navigation.HistoryCount == 1,
                "new item resets its current tab without discarding back history");
            navigation.SwitchTab(1);
            Check(navigation.ScrollOffset == 11, "new item preserves other tabs' offsets");
            navigation.SetScrollOffset(int.MaxValue);
            navigation.ClampScrollOffset(20, 6);
            Check(navigation.ScrollOffset == 14, "shrinking rows clamps to the final visible page");
            navigation.SwitchTab(2);
            navigation.SwitchTab(1);
            Check(navigation.ScrollOffset == 14, "clamping also updates saved tab scroll");
            navigation.ClampScrollOffset(2, 6);
            Check(navigation.ScrollOffset == 0, "shorter pages have no scroll");

            navigation.Begin(0);
            navigation.SetScrollOffset(4);
            navigation.Push("original");
            navigation.StartItem(2);
            navigation.SetScrollOffset(9);
            BrowserNavigationSnapshot snapshot;
            Check(navigation.TryBack(delegate(string id) { return true; }, out snapshot) && snapshot.ItemId == "original" &&
                navigation.Tab == 2 && navigation.ScrollOffset == 9,
                "selecting a back target leaves the current UI location intact until restore");
            navigation.Restore(snapshot);
            Check(navigation.Tab == 0 && navigation.ScrollOffset == 4, "restore applies the selected immutable snapshot");
            navigation.Restore(new BrowserNavigationSnapshot("invalid", int.MaxValue, int.MinValue));
            Check(navigation.Tab == 6 && navigation.ScrollOffset == 0, "restore validates tab and scroll bounds");

            navigation.Begin(0);
            Check(navigation.HistoryCount == 0, "new inspection clears history");
            for (int tab = 0; tab < 7; tab++)
            {
                navigation.SwitchTab(tab);
                Check(navigation.ScrollOffset == 0, "new inspection clears per-tab offsets");
            }
            for (int i = 0; i < 65; i++)
            {
                navigation.StartItem(i % 7);
                navigation.SetScrollOffset(i);
                navigation.Push("item" + i);
            }
            Check(navigation.HistoryCount == 64, "history remains bounded at the configured limit");
            string itemId;
            for (int i = 64; i >= 1; i--)
            {
                Check(Back(navigation, delegate(string id) { return true; }, out itemId) &&
                    itemId == "item" + i && navigation.Tab == i % 7 && navigation.ScrollOffset == i,
                    "history restores bounded newest-first snapshots");
            }
            Check(!Back(navigation, delegate(string id) { return true; }, out itemId) && itemId == null,
                "oldest overflow entry cannot return");

            navigation.Begin(0);
            navigation.SwitchTab(1);
            navigation.SetScrollOffset(8);
            navigation.Push("known");
            navigation.StartItem(2);
            navigation.Push("removed");
            navigation.StartItem(3);
            navigation.Push("also_removed");
            navigation.StartItem(6);
            var inspected = new List<string>();
            Check(Back(navigation, delegate(string id) { inspected.Add(id); return id == "known"; }, out itemId) &&
                itemId == "known" && navigation.Tab == 1 && navigation.ScrollOffset == 8,
                "back skips items missing from the current game data");
            Check(string.Join(",", inspected) == "also_removed,removed,known" && navigation.HistoryCount == 0,
                "each stale entry is discarded exactly once");
            navigation.SwitchTab(0);
            navigation.SwitchTab(1);
            Check(navigation.ScrollOffset == 8, "back restores the tab's saved scroll as well as current scroll");
            navigation.Push("gone");
            Check(!Back(navigation, delegate(string id) { return false; }, out itemId) &&
                navigation.Tab == 1 && navigation.ScrollOffset == 8 && itemId == null,
                "exhausting stale history leaves the current page intact");

            var other = new BrowserNavigationSessionState(7, 64);
            other.Begin(4);
            other.SetScrollOffset(99);
            other.Push("other_save");
            navigation.Push("previous_save");
            navigation.Close();
            Check(navigation.HistoryCount == 0 && navigation.ScrollOffset == 0 && navigation.Tab == 1,
                "closing releases history and current scroll while retaining the last tab");
            Check(other.HistoryCount == 1 && other.ScrollOffset == 99 && other.Tab == 4,
                "session owners share no mutable navigation state");
            navigation.Begin(3);
            navigation.SwitchTab(1);
            Check(navigation.ScrollOffset == 0 && navigation.HistoryCount == 0,
                "reopening cannot restore the closed session's scroll or back entries");
            bool missingLookup = false;
            try { Back(navigation, null, out itemId); }
            catch (ArgumentNullException) { missingLookup = true; }
            Check(missingLookup, "back requires the current game-data lookup");
            return assertions;
        }
    }
}
