namespace ItemIntelligence
{
    public static class ItemDecayCases
    {
        private static int assertions;
        private static void Check(bool value, string message)
        {
            assertions++;
            if (!value) throw new System.Exception("Item spoilage regression: " + message);
        }

        public static int Run()
        {
            assertions = 0;
            var index = new ItemDecayIndex();
            ItemDecayRelation result;
            Check(index.Count == 0 && index.GetSourceCount(null) == 0 && !index.TryGetResult(null, out result), "empty index");
            Check(index.Add("fresh", "rotten", 48f), "fresh item accepted");
            Check(index.Add("cooked", "rotten", 96f), "second source accepted");
            Check(index.TryGetResult("FRESH", out result) && result.SourceItemId == "fresh" &&
                result.ResultItemId == "rotten" && result.ShelfLifeHours == 48f, "result and lifetime retained");
            Check(index.GetSourceCount("ROTTEN") == 2 && index.GetSources("rotten").Length == 2, "both reverse sources retained");
            Check(!index.Add("Fresh", "other", 24f) && index.GetSources("other").Length == 0, "duplicate cannot break reverse index");
            Check(index.Add("kit", "fresh", 504f), "first stage of a chain accepted");
            Check(index.GetSourceCount("rotten") == 2 && index.GetSources("fresh")[0].SourceItemId == "kit",
                "chain keeps only immediate sources");
            Check(index.Add("vanishes", null, 12f) && index.TryGetResult("vanishes", out result) && result.ResultItemId == "",
                "empty result means disappearance");
            Check(index.GetSourceCount("") == 0, "disappearance is not an item source");
            Check(index.Add("immediate", "rotten", 0f), "zero lifetime retained");
            foreach (float invalid in new[] { -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                Check(!index.Add("invalid", "rotten", invalid), "invalid lifetime rejected: " + invalid);
            Check(!index.Add(null, "rotten", 1f) && !index.Add(" ", "rotten", 1f), "blank source rejected");
            Check(!index.Add("same", "SAME", 1f), "self transformation rejected");
            ItemDecayRelation[] snapshot = index.GetSources("rotten");
            snapshot[0] = null;
            Check(index.GetSources("rotten")[0] != null, "source snapshot cannot mutate index");
            index.Clear();
            Check(index.Count == 0 && index.GetSourceCount("rotten") == 0 && !index.TryGetResult("fresh", out result),
                "reset clears both directions");
            Check(index.Add("fresh", "new_result", 72f) && index.GetSourceCount("rotten") == 0,
                "new data generation has no stale sources");
            return assertions;
        }
    }
}
