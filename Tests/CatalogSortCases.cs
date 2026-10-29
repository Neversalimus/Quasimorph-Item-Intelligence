namespace ItemIntelligence
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    public static class CatalogSortCases
    {
        private static int assertions;
        private static void Check(bool value, string message)
        {
            assertions++;
            if (!value) throw new Exception("Catalog sort: " + message);
        }
        private static List<string> Items() { return new List<string> { "c", "a", "b" }; }
        private static string Order(List<string> items) { return string.Join(",", items); }

        public static int Run()
        {
            var cache = new CatalogSortCache();
            int comparisons = 0;
            Comparison<string> compare = delegate(string a, string b) { comparisons++; return string.CompareOrdinal(a, b); };
            var items = Items();
            Check(!cache.Sort(items, compare, 1, 0, false, "en", true) && Order(items) == "a,b,c", "first request sorts");
            comparisons = 0;
            items = Items();
            Check(cache.Sort(items, compare, 1, 0, false, "en", true) && comparisons == 0 && Order(items) == "a,b,c",
                "unchanged request reuses ordering without comparisons");
            items.Clear(); items.Add("unrelated");
            items = Items();
            Check(cache.Sort(items, compare, 1, 0, false, "en", true) && Order(items) == "a,b,c", "caller cannot mutate cached order");
            items = new List<string> { "c", "a", "d" };
            Check(!cache.Sort(items, compare, 1, 0, false, "en", true) && Order(items) == "a,c,d",
                "same-size membership change invalidates ordering");
            items = new List<string> { "a", "d", "c" };
            Check(!cache.Sort(items, compare, 1, 0, false, "en", true), "source-order change invalidates ordering");
            items = new List<string> { "a", "c" };
            Check(!cache.Sort(items, compare, 1, 0, false, "en", true) && Order(items) == "a,c", "membership count change invalidates ordering");
            foreach (int key in new int[] { 0, 1, 2, 3 })
            {
                cache.Reset();
                cache.Sort(Items(), compare, 1, 0, false, "en", true);
                comparisons = 0;
                Check(!cache.Sort(Items(), compare, key == 0 ? 2 : 1, key == 1 ? 2 : 0,
                    key == 2, key == 3 ? "tr" : "en", true) && comparisons > 0,
                    key == 0 ? "name revision invalidates ordering" : "sort mode, direction and culture invalidate ordering");
            }
            cache.Reset();
            cache.Sort(Items(), compare, 1, 0, false, "en", true);
            Check(!cache.Sort(Items(), compare, 1, 0, false, "en", false), "live sorting bypasses cache");
            Check(!cache.Sort(Items(), compare, 1, 0, false, "en", true), "bypass cannot leave reusable stale ordering");
            cache.Reset();
            Check(!cache.Sort(Items(), compare, 1, 0, false, "en", true), "session reset discards ordering");
            bool failed = false;
            try { cache.Sort(Items(), delegate { throw new Exception("sort failure"); }, 2, 0, false, "en", true); }
            catch (InvalidOperationException) { failed = true; }
            Check(failed && !cache.Sort(Items(), compare, 2, 0, false, "en", true), "failed comparison cannot publish a partial snapshot");

            var names = new Dictionary<string, string> {
                {"a", "Istanbul"}, {"b", "İstanbul"}, {"c", "istanbul"}, {"d", "ıstanbul"},
                {"e", "ёж"}, {"f", "Ель"}, {"g", "Ärztlich"}, {"h", "日本語"}, {"i", "한국어"},
                {"j", "-same"}, {"k", "same"}, {"l", "same"}
            };
            var input = new List<string>(names.Keys); input.Reverse();
            foreach (string cultureName in new string[] { "en-US", "ru-RU", "tr-TR", "de-DE", "ja-JP", "ko-KR" })
            foreach (bool descending in new bool[] { false, true })
            {
                var culture = CultureInfo.GetCultureInfo(cultureName);
                Comparison<string> localized = delegate(string a, string b) {
                    int result = culture.CompareInfo.Compare(names[a], names[b], CompareOptions.IgnoreCase);
                    if (result == 0) result = string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
                    return descending ? -result : result;
                };
                var expected = new List<string>(input); expected.Sort(localized);
                var actual = new List<string>(input);
                Check(!cache.Sort(actual, localized, 3, 0, descending, cultureName, true) && Order(actual) == Order(expected),
                    "localized ordering matches uncached comparator");
                actual = new List<string>(input);
                Check(cache.Sort(actual, localized, 3, 0, descending, cultureName, true) && Order(actual) == Order(expected),
                    "cached localized ordering preserves ties and direction");
            }
            return assertions;
        }
    }
}
