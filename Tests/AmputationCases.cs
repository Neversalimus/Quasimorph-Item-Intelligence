namespace ItemIntelligence
{
    public static partial class ModMain
    {
        // Fixtures expose runtime record members and known IDs. The parser, indexer,
        // data-entry model, probability validation and source model are production code.
        private sealed class AmputationSlotFixture
        {
            public string Id { get; set; }
            public object AmputatedDrop { get; set; }
        }
        private static object GetMember(object value, string name)
        {
            if (value == null) return null;
            System.Reflection.PropertyInfo property = value.GetType().GetProperty(name);
            return property == null ? null : property.GetValue(value, null);
        }
        private static string GetStringMember(object value, string name) { return GetMember(value, name) as string; }
        private static string FirstNonEmpty(string first, string second) { return string.IsNullOrEmpty(first) ? second : first; }
        private static List<string> ResolveLootExternalItemIds(string id)
        {
            return id == "meat" || id == "metal" ? new List<string> { id } : new List<string>();
        }

        private static void RunAmputationCases()
        {
            Dictionary<string, double> weights;
            List<Tuple<float, string>> rows = new List<Tuple<float, string>> {
                Tuple.Create(1f, "meat"), Tuple.Create(2f, "meat"), Tuple.Create(3f, "metal") };
            Check(TryExtractAmputationDropWeights(rows, out weights), "vanilla weight/ID tuples accepted");
            Check(weights.Count == 2, "repeated outcomes consolidated");
            Near(weights["meat"], 3, "repeated amputation outcomes retain total weight");
            Near(weights["metal"], 3, "second outcome weight");
            Check(rows.Count == 3 && rows[0].Item1 == 1f, "vanilla amputation list remains unchanged");
            foreach (object unsupported in new object[] { null, "meat 1", new List<Tuple<float, string>>(),
                new Dictionary<string, float> { { "meat", 1f } }, new List<Tuple<string, float>> { Tuple.Create("meat", 1f) } })
            {
                Check(!TryExtractAmputationDropWeights(unsupported, out weights), "wrong amputation schema or empty data rejected");
                Check(weights.Count == 0, "unsupported schema returns no partial data");
            }
            foreach (float invalid in new float[] { 0, -1, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                rows = new List<Tuple<float, string>> { Tuple.Create(1f, "meat"), Tuple.Create(invalid, "metal") };
                Check(!TryExtractAmputationDropWeights(rows, out weights), "invalid amputation interval rejects whole pool");
                Check(weights.Count == 0, "invalid interval does not inflate remaining probabilities");
            }
            foreach (Tuple<float, string> invalid in new Tuple<float, string>[] { null, Tuple.Create(1f, ""), Tuple.Create(1f, (string)null) })
            {
                rows = new List<Tuple<float, string>> { Tuple.Create(1f, "meat"), invalid };
                Check(!TryExtractAmputationDropWeights(rows, out weights) && weights.Count == 0, "missing tuple or outcome rejected without partial pool");
            }
            rows = new List<Tuple<float, string>> { Tuple.Create(float.MaxValue, "meat"), Tuple.Create(float.MaxValue, "metal") };
            Check(!TryExtractAmputationDropWeights(rows, out weights) && weights.Count == 0, "float overflow in vanilla total rejected");
            rows = new List<Tuple<float, string>> { Tuple.Create(2f, "meat"), Tuple.Create(-1f, "meat"), Tuple.Create(1f, "metal") };
            Check(!TryExtractAmputationDropWeights(rows, out weights), "positive merged total cannot conceal a negative interval");
            rows = new List<Tuple<float, string>> { Tuple.Create(.25f, "meat"), Tuple.Create(.75f, "metal") };
            Check(TryExtractAmputationDropWeights(rows, out weights), "fractional amputation weights accepted");
            Near(weights["meat"] / (weights["meat"] + weights["metal"]), .25, "fractional base distribution");

            // Reproduces the reported amputation=0 through the actual indexing entry point.
            LootAmputationSourcesByItem.Clear();
            rows = new List<Tuple<float, string>> { Tuple.Create(1f, "meat"), Tuple.Create(3f, "metal"), Tuple.Create(4f, "unknown_item") };
            IndexLootAmputationSlot(new DataEntry("fallback_slot", new AmputationSlotFixture { Id = "arm", AmputatedDrop = rows }));
            Check(LootAmputationSourcesByItem.Count == 2, "tuple amputation index contains known outcomes");
            Near(LootAmputationSourcesByItem["meat"][0].ConditionalPercent, 12.5, "unknown items remain in the denominator");
            Near(LootAmputationSourcesByItem["metal"][0].ConditionalPercent, 37.5, "index converts weights to percent");
            Check(LootAmputationSourcesByItem["meat"][0].WoundSlotId == "arm", "wound record ID preserved");
            rows = new List<Tuple<float, string>> { Tuple.Create(1f, "meat"), Tuple.Create(2f, "meat"), Tuple.Create(3f, "metal") };
            IndexLootAmputationSlot(new DataEntry("leg", new AmputationSlotFixture { AmputatedDrop = rows }));
            Check(LootAmputationSourcesByItem["meat"].Count == 2, "one row per distinct wound slot");
            Near(LootAmputationSourcesByItem["meat"][1].ConditionalPercent, 50, "duplicate intervals combined before index probability");
            Check(LootAmputationSourcesByItem["meat"][1].WoundSlotId == "leg", "entry key used when record ID absent");
            int before = warnings;
            IndexLootAmputationSlot(new DataEntry("bad", new AmputationSlotFixture { AmputatedDrop = new List<Tuple<float, string>> { Tuple.Create(1f, "meat"), Tuple.Create(float.NaN, "metal") } }));
            Check(warnings == before + 1 && LootAmputationSourcesByItem["meat"].Count == 2, "invalid slot is diagnosed and omitted");
            IndexLootAmputationSlot(null);
            IndexLootAmputationSlot(new DataEntry("empty", null));
            Check(LootAmputationSourcesByItem.Count == 2, "missing slot cannot corrupt existing sources");
            LootAmputationSourcesByItem.Clear();
        }
    }
}
