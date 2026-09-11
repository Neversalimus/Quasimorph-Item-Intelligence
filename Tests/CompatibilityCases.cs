namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static bool _compatStaticChecked = true;
        private static string _compatAssemblySha256;
        private static readonly Dictionary<string, object> ItemRecordsById = new Dictionary<string, object>();
        private static void RunCompatibilityShieldStatic() { throw new Exception("unexpected game bootstrap in unit test"); }

        private static void RunCompatibilityCases()
        {
            string sha582 = "9D0C784A764D75EC6616BD3B5744C0E581BB1CE148FD175BD8CABA8195854006";
            string sha104 = "BE78036434737521BB43DABBD934B579A08DC3E8370B834AEE36E40932F56FE0";
            string shaHotfix = "A38C4D993C9BF60D0DDE0EDD348F201C97574F907808417A33C8A20F4772E9C1";
            string sha103 = "FE68E4355D4ED9CBAB7F8B1BA7717DBC1CC3FD749D0D11A644A9A3DB5EAB478F";
            string sha102 = "EFF608C5118735359CD07FEAD8A8219E1CFB557E3A5A57517DB4428F04834B8B";
            foreach (string sha in new string[] { sha582, sha582.ToLowerInvariant(), sha582.Substring(0, 63) + "7", sha104, sha104.ToLowerInvariant(), shaHotfix, sha103, sha102, "unknown", "", null })
            {
                _compatAssemblySha256 = sha;
                bool is104 = string.Equals(sha, sha104, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(sha, sha582, StringComparison.OrdinalIgnoreCase);
                bool known = is104 || sha == shaHotfix || sha == sha103 || sha == sha102;
                Check(IsCurrentLootModifiersAssembly() == known, "Loot modifier fingerprint scope");
                Check(IsCurrentContainerSaveEstimateAssembly() == known, "container fingerprint scope");
                Check(IsCurrentScavengerAssembly() == known, "Scavenger fingerprint scope");
                Check(IsCurrentSourceFamilyAssembly() == known, "source-family fingerprint scope");
                Check(IsCurrent103CargoSpawnAssembly() == (known && sha != sha102), "cargo API fingerprint scope");
                Check(IsCurrent103TradeAssembly() == (known && sha != sha102), "modern trade fingerprint scope");
                Check(IsLegacy102FeatureAssembly() == (sha == sha102), "legacy fingerprint scope");
                Check(IsAuditedFeatureAssembly() == (sha == sha102 || sha == sha103), "narrow audits must not promote global feature scope");
                Check(IsCurrent104SourceFamilyAssembly() == is104, "random-start policy uses only exact 1.0.4");
                if (!known) continue; // Unknown builds do not invoke source indexing.
                string[] keys = GetRandomStartingPoolKeys(IsCurrent104SourceFamilyAssembly());
                Check(keys.Length == 2, "two starting pools");
                Check(keys[0] == (is104 ? "RandomStart_rewardEquipment" : "General_rewardEquipment"), "random-start equipment pool matches game version");
                Check(keys[1] == (is104 ? "RandomStart_rewardConsumables" : "General_rewardConsumables"), "random-start consumable pool matches game version");
            }
        }

        private static ItemRecord Primary(string id, int tech, params string[] categories)
        {
            return new ItemRecord { Id = id, TechLevel = tech, Categories = new List<string>(categories) };
        }

        private static void RunContainerCases()
        {
            ItemRecordsById.Clear();
            ItemRecordsById["target"] = new CompositeItemRecord("target", Primary("target", 1));
            ItemRecordsById["quest"] = new CompositeItemRecord("quest", Primary("quest", 0));
            List<LootWeightedItem> entries = new List<LootWeightedItem> {
                new LootWeightedItem("target", 2, 1, true, true, true),
                new LootWeightedItem("other", 3, 2, true, true, true),
                new LootWeightedItem("quest", 5, 0, true, false, true) };
            double chance;
            Check(TryResolveContainerPerRollChance("target", 1, entries, 2, false, out chance), "base container pool available");
            Near(chance, .2, "base pool includes quest weight");
            Check(TryResolveContainerPerRollChance("target", 1, entries, 2, true, out chance), "Marauder container pool available");
            Near(chance, .4, "Marauder pool excludes quest weight");
            Check(TryResolveContainerPerRollChance("quest", 0, entries, 2, true, out chance), "quest bonus is known zero");
            Near(chance, 0, "quest cannot be a Marauder container reward");
            Check(TryResolveContainerPerRollChance("target", 1, entries, 0, false, out chance), "target above Tech is known zero");
            Near(chance, 0, "above-Tech chance");
            LootContainerWeightedPool pool = new LootContainerWeightedPool(entries, true);
            Check(TryAverageContainerChance("target", pool, new List<int> { 2 }, 2, 1.2, out chance), "combined container projection");
            Near(chance, .64672, "two base draws and 1.2 independent bonus draws");
            Check(TryAverageContainerChance("target", pool, new List<int> { 1, 2 }, 2, 0, out chance), "two Tech contexts");
            Near(chance, (24.0 / 49.0 + .36) / 2, "average nonlinear probabilities after each Tech context");
            Check(TryAverageContainerChance("target", pool, new List<int> { 2 }, 0, 0, out chance), "zero rolls available");
            Near(chance, 0, "zero-roll container chance");
            Check(TryAverageContainerChance("quest", pool, new List<int> { 2 }, 0, 1.2, out chance), "quest-only bonus projection available");
            Near(chance, 0, "quest-only bonus chance");
            Check(!TryAverageContainerChance("missing", pool, new List<int> { 2 }, 1, 0, out chance), "missing target data rejected");
            Check(!TryAverageContainerChance("target", pool, new List<int>(), 1, 0, out chance), "missing Tech contexts rejected");
            Check(!TryAverageContainerChance("target", pool, new List<int> { 2 }, -1, 0, out chance), "negative base count rejected");
            foreach (double invalid in new double[] { -1, double.NaN, double.PositiveInfinity })
                Check(!TryAverageContainerChance("target", pool, new List<int> { 2 }, 1, invalid, out chance), "invalid bonus expectation rejected");
            foreach (double invalid in new double[] { 0, -1, double.NaN, double.PositiveInfinity })
            {
                List<LootWeightedItem> bad = new List<LootWeightedItem>(entries);
                bad[1] = new LootWeightedItem("other", invalid, 2, true, true, true);
                Check(!TryResolveContainerPerRollChance("target", 1, bad, 2, false, out chance), "invalid eligible container weight rejects projection");
            }
            List<LootWeightedItem> unresolved = new List<LootWeightedItem>(entries);
            unresolved[1] = new LootWeightedItem("other", 3, 2, false, true, true);
            Check(!TryResolveContainerPerRollChance("target", 1, unresolved, 2, false, out chance), "unknown pool Tech rejected");
            unresolved[1] = new LootWeightedItem("other", 3, 2, true, true, false);
            Check(!TryResolveContainerPerRollChance("target", 1, unresolved, 2, true, out chance), "unknown bonus eligibility rejected");
        }

        private static void RunScavengerCases()
        {
            object[] components = { new TrashRecord(), new WeaponRecord(), new HelmetRecord(), new ArmorRecord(),
                new LeggingsRecord(), new BootsRecord(), new ConsumableRecord(), new FixationMedicineRecord(), new AmmoRecord(), new GrenadeRecord() };
            ScavengerRewardClass[] expected = { ScavengerRewardClass.Resources, ScavengerRewardClass.ArmorWeapons,
                ScavengerRewardClass.ArmorWeapons, ScavengerRewardClass.ArmorWeapons, ScavengerRewardClass.ArmorWeapons,
                ScavengerRewardClass.ArmorWeapons, ScavengerRewardClass.FoodMeds, ScavengerRewardClass.FoodMeds,
                ScavengerRewardClass.AmmoGrenades, ScavengerRewardClass.AmmoGrenades };
            ScavengerRewardClass[] classes = { ScavengerRewardClass.Resources, ScavengerRewardClass.ArmorWeapons,
                ScavengerRewardClass.FoodMeds, ScavengerRewardClass.AmmoGrenades };
            for (int i = 0; i < components.Length; i++)
            foreach (ScavengerRewardClass kind in classes)
                Check(MatchesScavengerRewardClass(new CompositeItemRecord("item", Primary("item", 1), components[i]), kind) == (kind == expected[i]), "Scavenger class predicate " + i + " / " + kind);
            CompositeItemRecord hybrid = new CompositeItemRecord("hybrid", Primary("hybrid", 1), new TrashRecord(), new AmmoRecord());
            Check(MatchesScavengerRewardClass(hybrid, ScavengerRewardClass.Resources) && MatchesScavengerRewardClass(hybrid, ScavengerRewardClass.AmmoGrenades), "independent reward classes may overlap");
            Check(!MatchesScavengerRewardClass(null, ScavengerRewardClass.Resources), "missing composite rejected");
            HashSet<string> whitelist;
            Check(TryBuildExactScavengerWhitelist(new string[] { "Common" }, new string[] { "Faction" }, out whitelist), "distinct station and faction categories");
            Check(IsExactScavengerCandidate(Primary("a", 2, "Common"), whitelist, 2, "AnCom"), "category candidate at Tech limit");
            Check(IsExactScavengerCandidate(Primary("a", 2, "AnCom"), whitelist, 2, "AnCom"), "faction alias candidate");
            Check(!IsExactScavengerCandidate(Primary("a", 3, "Common"), whitelist, 2, "AnCom"), "high-Tech Scavenger excluded");
            Check(IsExactScavengerCandidate(Primary("a", 99, "Common"), whitelist, -1, "AnCom"), "unlimited Tech sentinel");
            Check(!IsExactScavengerCandidate(Primary("a", 1, "common"), whitelist, 2, "AnCom"), "vanilla category matching is case-sensitive");
            Check(!IsExactScavengerCandidate(Primary("a", 1, "Other"), whitelist, 2, "AnCom"), "unmatched category excluded");
            Data.Items.Records.Clear();
            Data.Items.Records.Add(new CompositeItemRecord("one", Primary("target", 1, "Common"), new AmmoRecord()));
            Data.Items.Records.Add(new CompositeItemRecord("two", Primary("target", 2, "AnCom"), new AmmoRecord()));
            Data.Items.Records.Add(new CompositeItemRecord("other", Primary("other", 2, "Common"), new GrenadeRecord()));
            Data.Items.Records.Add(new CompositeItemRecord("target_custom", Primary("target", 1, "Common"), new AmmoRecord()));
            Data.Items.Records.Add(new CompositeItemRecord("high", Primary("target", 3, "Common"), new AmmoRecord()));
            Data.Items.Records.Add(new CompositeItemRecord("wrong_class", Primary("target", 1, "Common"), new TrashRecord()));
            Data.Items.Records.Add(new CompositeItemRecord("wrong_category", Primary("target", 1, "Other"), new AmmoRecord()));
            Data.Items.Records.Add(new BasePickupItemRecord { Id = "non_composite" });
            Data.Items.Records.Add(null);
            ScavengerPoolStats stats = CountExactScavengerPool(ScavengerRewardClass.AmmoGrenades, whitelist, 2, "AnCom", "target");
            Check(stats.TotalCandidates == 3 && stats.TargetCandidates == 2, "Scavenger K/N counts composites sharing the same primary ID");
            Near(CorpseBonusAtLeastOnceChance((double)stats.TargetCandidates / stats.TotalCandidates, 2), 8.0 / 9.0, "two Scavenger draws with target multiplicity");
            Check(!TryBuildExactScavengerWhitelist(new string[] { "Common" }, new string[] { "Common" }, out whitelist), "duplicate vanilla Dictionary.Add categories rejected");
            Check(!TryBuildExactScavengerWhitelist(new string[] { "Common", "Common" }, new string[0], out whitelist), "duplicate station category rejected");
            Check(!TryBuildExactScavengerWhitelist(new string[] { null }, new string[0], out whitelist), "null category rejected");
            Check(!TryBuildExactScavengerWhitelist(null, new string[0], out whitelist), "missing category table rejected");
            Check(!TryBuildExactScavengerWhitelist(new string[0], new string[0], out whitelist), "empty whitelist rejected");
            Data.Items.Records.Clear();
        }
    }
}
